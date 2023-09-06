using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeCryptoComApi : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.crypto.com/v2";
		public override string BaseUrlWebSocket { get; set; } = "wss://stream.crypto.com/v2";

		public ExchangeCryptoComApi()
		{
			NonceStyle = NonceStyle.UnixMilliseconds;
			NonceOffset = TimeSpan.FromSeconds(0.1);
			// WebSocketOrderBookType = not implemented
			MarketSymbolSeparator = "_";
			MarketSymbolIsUppercase = true;
			// ExchangeGlobalCurrencyReplacements[] not implemented
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			var instruments = await MakeJsonRequestAsync<JToken>("public/get-instruments");
			var markets = new List<ExchangeMarket>();
			foreach (JToken instrument in instruments["instruments"])
			{
				markets.Add(
						new ExchangeMarket
						{
							MarketSymbol = instrument["instrument_name"].ToStringUpperInvariant(),
							QuoteCurrency = instrument["quote_currency"].ToStringInvariant(),
							BaseCurrency = instrument["base_currency"].ToStringInvariant(),
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
				marketSymbols = new string[] { "" };
			}
			var ws = await ConnectPublicWebSocketAsync(
					"/market",
					async (_socket, msg) =>
					{
						/*{
						{{
							"code": 0,
							"method": "subscribe",
							"result": {
								"instrument_name": "YFI_BTC",
								"subscription": "trade.YFI_BTC",
								"channel": "trade",
								"data": [
									{
										"dataTime": 1645139769555,
										"d": 2258312914797956554,
										"s": "BUY",
										"p": 0.5541,
										"q": 1E-06,
										"t": 1645139769539,
										"i": "YFI_BTC"
									}
								]
							}
						}}
						} */
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (
											token["method"].ToStringInvariant() == "ERROR"
											|| token["method"].ToStringInvariant() == "unknown"
									)
						{
							throw new APIException(
												token["code"].ToStringInvariant()
														+ ": "
														+ token["message"].ToStringInvariant()
										);
						}
						else if (token["method"].ToStringInvariant() == "public/heartbeat")
						{ /* For websocket connections, the system will send a heartbeat message to the client every 30 seconds.
				   * The client must respond back with the public/respond-heartbeat method, using the same matching id, within 5 seconds, or the connection will break. */
							var hrResponse = new
							{
								id = token["id"].ConvertInvariant<long>(),
								method = "public/respond-heartbeat",
							};
							await _socket.SendMessageAsync(hrResponse);

							if (
												token["message"].ToStringInvariant()
												== "server did not receive any client heartbeat, going to disconnect soon"
										)
								Logger.Warn(
													token["code"].ToStringInvariant()
															+ ": "
															+ token["message"].ToStringInvariant()
											);
						}
						else if (
											token["method"].ToStringInvariant() == "subscribe"
											&& token["result"] != null
									)
						{
							var result = token["result"];
							var dataArray = result["data"].ToArray();
							for (int i = 0; i < dataArray.Length; i++)
							{
								JToken data = dataArray[i];
								var trade = data.ParseTrade(
													"q",
													"p",
													"s",
													"t",
													TimestampType.UnixMilliseconds,
													"d"
											);
								string marketSymbol = data["i"].ToStringInvariant();
								if (dataArray.Length == 100) // initial snapshot contains 100 trades
								{
									trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
									if (i == dataArray.Length - 1)
										trade.Flags |= ExchangeTradeFlags.IsLastFromSnapshot;
								}
								await callback(
													new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
											);
							}
						}
					},
					async (_socket) =>
					{ /* We recommend adding a 1-second sleep after establishing the websocket connection, and before requests are sent.
				 * This will avoid occurrences of rate-limit (`TOO_MANY_REQUESTS`) errors, as the websocket rate limits are pro-rated based on the calendar-second that the websocket connection was opened.
				 */
						await Task.Delay(1000);

						/*
						{
								"id": 11,
								"method": "subscribe",
								"params": {
								"channels": ["trade.ETH_CRO"]
								},
								"nonce": 1587523073344
						}
						 */
						var subscribeRequest = new
						{
							// + consider using id field in the future to differentiate between requests
							//id = new Random().Next(),
							method = "subscribe",
							@params = new
							{
								channels = marketSymbols
															.Select(s => string.IsNullOrWhiteSpace(s) ? "trade" : $"trade.{s}")
															.ToArray(),
							},
							nonce = await GenerateNonceAsync(),
						};
						await _socket.SendMessageAsync(subscribeRequest);
					}
			);
			ws.KeepAlive = new TimeSpan(0); // cryptocom throws bad request empty content msgs w/ keepalives
			return ws;
		}
	}

	public partial class ExchangeName
	{
		public const string CryptoCom = "CryptoCom";
	}
}
