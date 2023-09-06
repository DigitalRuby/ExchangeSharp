using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeBtcTurkAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.btcturk.com";
		public override string BaseUrlWebSocket { get; set; } = "wss://ws-feed-pro.btcturk.com";

		public ExchangeBtcTurkAPI()
		{
			NonceStyle = NonceStyle.UnixMilliseconds;
			NonceOffset = TimeSpan.FromSeconds(0.1);
			// WebSocketOrderBookType = not implemented
			MarketSymbolSeparator = "";
			MarketSymbolIsUppercase = true;
			// ExchangeGlobalCurrencyReplacements[] not implemented
		}

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{ /*{
			  "data": {
				"timeZone": "UTC",
				"serverTime": 1645091654418,
				"symbols": [
				  {
					"id": 1,
					"name": "BTCTRY",
					"nameNormalized": "BTC_TRY",
					"status": "TRADING",
					"numerator": "BTC",
					"denominator": "TRY",
					"numeratorScale": 8,
					"denominatorScale": 2,
					"hasFraction": false,
					"filters": [
					  {
						"filterType": "PRICE_FILTER",
						"minPrice": "0.0000000000001",
						"maxPrice": "10000000",
						"tickSize": "10",
						"minExchangeValue": "99.91",
						"minAmount": null,
						"maxAmount": null
					  }
					],
					"orderMethods": [
					  "MARKET",
					  "LIMIT",
					  "STOP_MARKET",
					  "STOP_LIMIT"
					],
					"displayFormat": "#,###",
					"commissionFromNumerator": false,
					"order": 1000,
					"priceRounding": false,
					"isNew": false,
					"marketPriceWarningThresholdPercentage": 0.2500000000000000,
					"maximumOrderAmount": null,
					"maximumLimitOrderPrice": 5895000.0000000000000000,
					"minimumLimitOrderPrice": 58950.0000000000000000
				  }
			}*/
			var instruments = await MakeJsonRequestAsync<JToken>("api/v2/server/exchangeinfo");
			var markets = new List<ExchangeMarket>();
			foreach (JToken instrument in instruments["symbols"])
			{
				markets.Add(
						new ExchangeMarket
						{
							MarketSymbol = instrument["name"].ToStringUpperInvariant(),
							QuoteCurrency = instrument["denominator"].ToStringInvariant(),
							BaseCurrency = instrument["numerator"].ToStringInvariant(),
						}
				);
			}
			return markets;
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsMetadataAsync())
						.Select(m => m.MarketSymbol)
						.ToArray();
			}
			return await ConnectPublicWebSocketAsync(
					"/market",
					async (_socket, msg) =>
					{ /* {[
					  991,
					  {
						"type": 991,
						"current": "5.1.0",
						"min": "2.3.0"
					  }
					]}*/
						/* {[
									451,
									{
										"type": 451,
										"pairId": 0,
										"symbol": "BTCTRY",
										"id": 0,
										"method": 0,
										"userId": 0,
										"orderType": 0,
										"price": "0",
										"amount": "0",
										"numLeft": "0.00",
										"denomLeft": "0",
										"newOrderClientId": null
									}
								]}] */
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						if (token[0].ToObject<int>() == 991)
						{
							// no need to do anything with this
						}
						//else if (token["method"].ToStringInvariant() == "ERROR" || token["method"].ToStringInvariant() == "unknown")
						//{
						//	throw new APIException(token["code"].ToStringInvariant() + ": " + token["message"].ToStringInvariant());
						//}
						else if (token[0].ToObject<int>() == 451)
						{
							// channel 451 OrderInsert. Ignore.
						}
						else if (token[0].ToObject<int>() == 100)
						{ /* 
				   {[
					  100,
					  {
						"ok": true,
						"message": "join|trade:BTCTRY",
						"type": 100
					  }
					]}
				   */
							// successfully joined
						}
						else if (token[0].ToObject<int>() == 421)
						{ /*
					 {[
						421,
						{
						"symbol": "BTCTRY",
						"items": [
							{
							"D": "1651204111661",
							"I": "100163785789620803",
							"A": "0.0072834700",
							"P": "586484.0000000000",
							"S": 0
							},
							{
							"D": "1651202811844",
							"I": "100163785789620737",
							"A": "0.0004123600",
							"P": "585778.0000000000",
							"S": 1
							}
						],
						"channel": "trade",
						"event": "BTCTRY",
						"type": 421
						}
					]} */
							var data = token[1];
							var dataArray = data["items"].ToArray();
							for (int i = 0; i < dataArray.Length; i++)
							{
								var trade = dataArray[i].ParseTrade(
													"A",
													"P",
													"S",
													"D",
													TimestampType.UnixMilliseconds,
													"I",
													"0"
											);
								string marketSymbol = data["symbol"].ToStringInvariant();
								trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
								if (i == dataArray.Length - 1)
									trade.Flags |= ExchangeTradeFlags.IsLastFromSnapshot;
								await callback(
													new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
											);
							}
						}
						else if (token[0].ToObject<int>() == 422)
						{ /* {[
					  422,
					  {
						"D": "1651204593830",
						"I": "100163785789620825",
						"A": "0.0008777700",
						"P": "586950.0000000000",
						"PS": "BTCTRY",
						"S": 1,
						"channel": "trade",
						"event": "BTCTRY",
						"type": 422
					  }
					]} */
							var data = token[1];
							var trade = data.ParseTrade(
												"A",
												"P",
												"S",
												"D",
												TimestampType.UnixMilliseconds,
												"I",
												"0"
										);
							string marketSymbol = data["PS"].ToStringInvariant();
							await callback(
												new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
										);
						}
						else
							Logger.Warn($"Unexpected channel {token[0].ToObject<int>()}");
					},
					async (_socket) =>
					{ /*
			    [
					151,
					{
						"type": 151,
						"channel": 'CHANNEL_NAME_HERE',
						"event": 'PAIR_NAME_HERE',
						"join": True
					}
				]
			   */
						foreach (var marketSymbol in marketSymbols)
						{
							var subscribeRequest = new List<object>();
							subscribeRequest.Add(151);
							subscribeRequest.Add(
												new
												{
													type = 151,
													channel = "trade",
													@event = marketSymbol,
													join = true, // If true, it means that you want to subscribe, if false, you can unsubscribe.
												}
										);
							await _socket.SendMessageAsync(subscribeRequest.ToArray());
						}
					}
			);
		}
	}

	public partial class ExchangeName
	{
		public const string BtcTurk = "BtcTurk";
	}
}
