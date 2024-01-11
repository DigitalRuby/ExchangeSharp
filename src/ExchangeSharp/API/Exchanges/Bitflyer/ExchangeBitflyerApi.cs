using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SocketIOClient;

namespace ExchangeSharp
{
	public sealed partial class ExchangeBitflyerApi : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.bitflyer.com";
		public override string BaseUrlWebSocket { get; set; } =
				"https://io.lightstream.bitflyer.com";

		public ExchangeBitflyerApi()
		{
			//NonceStyle = new guid
			//NonceOffset not needed
			// WebSocketOrderBookType = not implemented
			MarketSymbolSeparator = "_";
			MarketSymbolIsUppercase = true;
			// ExchangeGlobalCurrencyReplacements[] not implemented
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			/*
			 [
						{
							"product_code": "BTC_JPY",
							"market_type": "Spot"
						},
						{
							"product_code": "XRP_JPY",
							"market_type": "Spot"
						},
						{
							"product_code": "ETH_JPY",
							"market_type": "Spot"
						},
						{
							"product_code": "XLM_JPY",
							"market_type": "Spot"
						},
						{
							"product_code": "MONA_JPY",
							"market_type": "Spot"
						},
						{
							"product_code": "ETH_BTC",
							"market_type": "Spot"
						},
						{
							"product_code": "BCH_BTC",
							"market_type": "Spot"
						},
						{
							"product_code": "FX_BTC_JPY",
							"market_type": "FX"
						},
						{
							"product_code": "BTCJPY12MAR2021",
							"alias": "BTCJPY_MAT1WK",
							"market_type": "Futures"
						},
						{
							"product_code": "BTCJPY19MAR2021",
							"alias": "BTCJPY_MAT2WK",
							"market_type": "Futures"
						},
						{
							"product_code": "BTCJPY26MAR2021",
							"alias": "BTCJPY_MAT3M",
							"market_type": "Futures"
						}
					]
			 */
			JToken instruments = await MakeJsonRequestAsync<JToken>("v1/getmarkets");
			var markets = new List<ExchangeMarket>();
			foreach (JToken instrument in instruments)
			{
				markets.Add(
						new ExchangeMarket
						{
							MarketSymbol = instrument["product_code"].ToStringUpperInvariant(),
							AltMarketSymbol = instrument["alias"].ToStringInvariant(),
							AltMarketSymbol2 = instrument["market_type"].ToStringInvariant(),
						}
				);
			}
			return markets.Select(m => m.MarketSymbol);
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			var client = new SocketIOWrapper(BaseUrlWebSocket);

			foreach (var marketSymbol in marketSymbols)
			{ // {product_code} can be obtained from the market list. It cannot be an alias.
				// BTC/JPY (Spot): lightning_executions_BTC_JPY
				client.socketIO.On(
						$"lightning_executions_{marketSymbol}",
						response =>
						{ /* [[ {
						"id": 39361,
						"side": "SELL",
						"price": 35100,
						"size": 0.01,
						"exec_date": "2015-07-07T10:44:33.547Z",
						"buy_child_order_acceptance_id": "JRF20150707-014356-184990",
						"sell_child_order_acceptance_id": "JRF20150707-104433-186048"
					} ]] */
							var token = JToken.Parse(response.ToStringInvariant());
							foreach (var tradeToken in token[0])
							{
								var trade = tradeToken.ParseTradeBitflyer(
														"size",
														"price",
														"side",
														"exec_date",
														TimestampType.Iso8601UTC,
														"id"
												);

								// If it is executed during an Itayose, it will be an empty string.
								if (string.IsNullOrWhiteSpace(tradeToken["side"].ToStringInvariant()))
									trade.Flags |= ExchangeTradeFlags.HasNoSide;

								callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade))
														.Wait();
							}
						}
				);
			}

			client.socketIO.OnConnected += async (sender, e) =>
			{
				foreach (var marketSymbol in marketSymbols)
				{ // {product_code} can be obtained from the market list. It cannot be an alias.
					// BTC/JPY (Spot): lightning_executions_BTC_JPY
					await client.socketIO.EmitAsync(
										"subscribe",
										$"lightning_executions_{marketSymbol}"
								);
				}
			};
			await client.socketIO.ConnectAsync();
			return client;
		}
	}

	class SocketIOWrapper : IWebSocket
	{
		public SocketIOClient.SocketIO socketIO;

		public SocketIOWrapper(string url)
		{
			socketIO = new SocketIOClient.SocketIO(url);
			socketIO.Options.Transport = SocketIOClient.Transport.TransportProtocol.WebSocket;
			socketIO.OnConnected += (s, e) => Connected?.Invoke(this);
			socketIO.OnDisconnected += (s, e) => Disconnected?.Invoke(this);
		}

		public TimeSpan ConnectInterval
		{
			get => throw new NotSupportedException();
			set => socketIO.Options.Reconnection = value > TimeSpan.Zero;
		}

		public TimeSpan KeepAlive
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public event WebSocketConnectionDelegate Connected;
		public event WebSocketConnectionDelegate Disconnected;

		public Task<bool> SendMessageAsync(object message) => throw new NotImplementedException();

		public void Dispose() => socketIO.Dispose();
	}

	public partial class ExchangeName
	{
		public const string Bitflyer = "Bitflyer";
	}
}
