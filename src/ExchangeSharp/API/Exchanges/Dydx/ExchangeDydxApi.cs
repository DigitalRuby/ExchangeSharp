using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeDydxApi : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.dydx.exchange";
		public override string BaseUrlWebSocket { get; set; } = "wss://api.dydx.exchange/v3/ws";

		public ExchangeDydxApi()
		{
			NonceStyle = NonceStyle.Iso8601;
			NonceOffset = TimeSpan.FromSeconds(0.1);
			// WebSocketOrderBookType = not implemented
			MarketSymbolSeparator = "-";
			MarketSymbolIsUppercase = true;
			// ExchangeGlobalCurrencyReplacements[] not implemented
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			/*{
					"markets": {
					"LINK-USD": {
					"market": "LINK-USD",
					"status": "ONLINE",
					"baseAsset": "LINK",
					"quoteAsset": "USD",
					"stepSize": "0.1",
					"tickSize": "0.01",
					"indexPrice": "12",
					"oraclePrice": "101",
					"priceChange24H": "0",
					"nextFundingRate": "0.0000125000",
					"nextFundingAt": "2021-03-01T18:00:00.000Z",
					"minOrderSize": "1",
					"type": "PERPETUAL",
					"initialMarginFraction": "0.10",
					"maintenanceMarginFraction": "0.05",
					"baselinePositionSize": "1000",
					"incrementalPositionSize": "1000",
					"incrementalInitialMarginFraction": "0.2",
					"volume24H": "0",
					"trades24H": "0",
					"openInterest": "0",
					"maxPositionSize": "10000",
					"assetResolution": "10000000",
					"syntheticAssetId": "0x4c494e4b2d37000000000000000000",
					},
					...
			}*/
			var instruments = await MakeJsonRequestAsync<JToken>("v3/markets");
			var markets = new List<ExchangeMarket>();
			foreach (JToken instrument in instruments["markets"])
			{
				markets.Add(
						new ExchangeMarket
						{
							MarketSymbol = instrument.ElementAt(0)["market"].ToStringInvariant(),
							QuoteCurrency = instrument.ElementAt(0)["quoteAsset"].ToStringInvariant(),
							BaseCurrency = instrument.ElementAt(0)["baseAsset"].ToStringInvariant(),
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
			return await ConnectPublicWebSocketAsync(
					"",
					async (_socket, msg) =>
					{
						/*Example initial response:
						{
							"type": "subscribed",
							"id": "BTC-USD",
							"connection_id": "e2a6c717-6f77-4c1c-ac22-72ce2b7ed77d",
							"channel": "v3_trades",
							"message_id": 1,
							"contents": {
								"trades": [
									{
										"side": "BUY",
										"size": "100",
										"price": "4000",
										"createdAt": "2020-10-29T00:26:30.759Z"
									},
									{
										"side": "BUY",
										"size": "100",
										"price": "4000",
										"createdAt": "2020-11-02T19:45:42.886Z"
									},
									{
										"side": "BUY",
										"size": "100",
										"price": "4000",
										"createdAt": "2020-10-29T00:26:57.382Z"
									}
								]
							}
						}*/
						/* Example subsequent response
						{
								"type": "channel_data",
								"id": "BTC-USD",
								"connection_id": "e2a6c717-6f77-4c1c-ac22-72ce2b7ed77d",
								"channel": "v3_trades",
								"message_id": 2,
								"contents": {
								"trades": [
										{
										"side": "BUY",
										"size": "100",
										"price": "4000",
										"createdAt": "2020-11-29T00:26:30.759Z"
										},
										{
										"side": "SELL",
										"size": "100",
										"price": "4000",
										"createdAt": "2020-11-29T14:00:03.382Z"
										}
								]
								}
						} */
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (token["type"].ToStringInvariant() == "error")
						{
							throw new APIException(token["message"].ToStringInvariant());
						}
						else if (token["channel"].ToStringInvariant() == "v3_trades")
						{
							var tradesArray = token["contents"]["trades"].ToArray();
							for (int i = 0; i < tradesArray.Length; i++)
							{
								var trade = tradesArray[i].ParseTrade(
													"size",
													"price",
													"side",
													"createdAt",
													TimestampType.Iso8601UTC,
													null
											);
								string marketSymbol = token["id"].ToStringInvariant();
								if (
													token["type"].ToStringInvariant() == "subscribed"
													|| token["message_id"].ToObject<int>() == 1
											)
								{
									trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
									if (i == tradesArray.Length - 1)
										trade.Flags |= ExchangeTradeFlags.IsLastFromSnapshot;
								}
								await callback(
													new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
											);
							}
						}
					},
					async (_socket) =>
					{
						foreach (var marketSymbol in marketSymbols)
						{
							var subscribeRequest = new
							{
								type = "subscribe",
								channel = "v3_trades",
								id = marketSymbol,
							};
							await _socket.SendMessageAsync(subscribeRequest);
						}
					}
			);
		}
	}

	public partial class ExchangeName
	{
		public const string Dydx = "Dydx";
	}
}
