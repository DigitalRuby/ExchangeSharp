/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Newtonsoft.Json.Linq;

	public sealed partial class ExchangePoloniexAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.poloniex.com";
		public override string BaseUrlWebSocket { get; set; } = "wss://ws.poloniex.com/ws";

		private ExchangePoloniexAPI()
		{
			RequestContentType = "application/json";
			MarketSymbolSeparator = "_";
			WebSocketOrderBookType = WebSocketOrderBookType.DeltasOnly;
			RateLimit = new RateGate(10, TimeSpan.FromSeconds(1));
		}

		/// <summary>
		/// Number of fields Poloniex provides for withdrawals since specifying
		/// extra content in the API request won't be rejected and may cause withdraweal to get stuck.
		/// </summary>
		public static IReadOnlyDictionary<string, int> WithdrawalFieldCount { get; set; }

		private async Task<JToken> MakePrivateAPIRequestAsync(
				string command,
				IReadOnlyList<object> parameters = null
		)
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload["command"] = command;
			if (parameters != null && parameters.Count % 2 == 0)
			{
				for (int i = 0; i < parameters.Count;)
				{
					payload[parameters[i++].ToStringInvariant()] = parameters[i++];
				}
			}

			return await MakeJsonRequestAsync<JToken>("/tradingApi", null, payload);
		}

		/// <inheritdoc />
		protected override Task OnInitializeAsync()
		{
			// load withdrawal field counts
			var fieldCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

			using var resourceStream =
					typeof(ExchangePoloniexAPI).Assembly.GetManifestResourceStream(
							"ExchangeSharp.Properties.Resources.PoloniexWithdrawalFields.csv"
					);
			Debug.Assert(resourceStream != null, nameof(resourceStream) + " != null");
			using var sr = new StreamReader(resourceStream);

			sr.ReadLine(); // eat the header
			string line;
			while ((line = sr.ReadLine()) != null)
			{
				string[] split = line.Split(',');
				if (split.Length == 2)
				{
					fieldCount[split[0]] = split[1].ConvertInvariant<int>();
				}
			}
			WithdrawalFieldCount = fieldCount;

			return Task.CompletedTask;
		}

		public ExchangeOrderResult ParsePlacedOrder(JToken result, ExchangeOrderRequest request)
		{
			ExchangeOrderResult order = new ExchangeOrderResult
			{
				OrderId = result["orderNumber"].ToStringInvariant(),
				Amount = request.Amount,
				Price = request.Price
			};

			JToken trades = result["resultingTrades"];

			if (trades == null)
			{
				order.Result = ExchangeAPIOrderResult.Canceled;
			}
			else if (trades != null && trades.Count() == 0)
			{
				order.Result = ExchangeAPIOrderResult.Open;
			}
			else if (trades != null && trades.Children().Count() != 0)
			{
				ParseOrderTrades(trades, order);
			}

			return order;
		}

		public static ExchangeOrderResult ParseOrder(JToken result, string marketSymbol = null)
		{
			var order = new ExchangeOrderResult
			{
				Amount = result["quantity"].ConvertInvariant<decimal>(),
				AmountFilled = result["filledQuantity"].ConvertInvariant<decimal>(),
				IsBuy = result["side"].ToStringLowerInvariant() != "sell",
				OrderDate = result["createTime"]
							.ConvertInvariant<long>()
							.UnixTimeStampToDateTimeMilliseconds(),
				OrderId = result["id"].ToStringInvariant(),
				Price = result["price"].ConvertInvariant<decimal>(),
				AveragePrice = result["avgPrice"].ConvertInvariant<decimal>(),
				Result = ParseOrderStatus(result["state"].ToStringInvariant()),
				MarketSymbol = result["symbol"].ToStringInvariant(),
				ClientOrderId = result["clientOrderId"].ToStringInvariant()
			};

			// fee is a percentage taken from the traded amount rounded to 8 decimals
			order.Fees = CalculateFees(
					order.Amount,
					order.Price.Value,
					order.IsBuy,
					result["fee"].ConvertInvariant<decimal>()
			);

			return order;
		}

		public void ParseOrderTrades(IEnumerable<JToken> trades, ExchangeOrderResult order)
		{
			bool orderMetadataSet = false;
			foreach (JToken trade in trades)
			{
				if (!orderMetadataSet)
				{
					order.IsBuy = trade["type"].ToStringLowerInvariant() != "sell";
					string parsedSymbol = trade["currencyPair"].ToStringInvariant();
					if (string.IsNullOrWhiteSpace(parsedSymbol) && trade.Parent != null)
					{
						parsedSymbol = trade.Parent.Path;
					}

					if (order.MarketSymbol == "all" || !string.IsNullOrWhiteSpace(parsedSymbol))
					{
						order.MarketSymbol = parsedSymbol;
					}

					if (!string.IsNullOrWhiteSpace(order.MarketSymbol))
					{
						order.FeesCurrency = ParseFeesCurrency(order.IsBuy, order.MarketSymbol);
					}

					orderMetadataSet = true;
				}

				decimal tradeAmt = trade["amount"].ConvertInvariant<decimal>();
				decimal tradeRate = trade["rate"].ConvertInvariant<decimal>();

				order.AveragePrice =
						(
								order.AveragePrice.GetValueOrDefault(decimal.Zero)
										* order.AmountFilled.GetValueOrDefault(decimal.Zero)
								+ tradeAmt * tradeRate
						) / (order.AmountFilled.GetValueOrDefault(decimal.Zero) + tradeAmt);
				if (order.Amount == 0m)
				{
					order.Amount += tradeAmt;
				}

				order.AmountFilled = order.AmountFilled.GetValueOrDefault(decimal.Zero);
				order.AmountFilled += tradeAmt;

				if (order.OrderDate == DateTime.MinValue)
				{
					order.OrderDate = trade["date"].ToDateTimeInvariant();
				}

				// fee is a percentage taken from the traded amount rounded to 8 decimals
				order.Fees = order.Fees.GetValueOrDefault(decimal.Zero);
				order.Fees += CalculateFees(
						tradeAmt,
						tradeRate,
						order.IsBuy,
						trade["fee"].ConvertInvariant<decimal>()
				);
			}

			if (order.AmountFilled.GetValueOrDefault(decimal.Zero) >= order.Amount)
			{
				order.Result = ExchangeAPIOrderResult.Filled;
			}
			else
			{
				order.Result = ExchangeAPIOrderResult.FilledPartially;
			}

			// Poloniex does not provide a way to get the original price
			order.AveragePrice = order.AveragePrice?.Normalize();
			order.Price = order.AveragePrice;
		}

		private static decimal CalculateFees(
				decimal tradeAmt,
				decimal tradeRate,
				bool isBuy,
				decimal fee
		)
		{
			decimal amount = isBuy ? tradeAmt * fee : tradeAmt * tradeRate * fee;
			return Math.Round(amount, 8, MidpointRounding.AwayFromZero);
		}

		private static IEnumerable<ExchangeOrderResult> ParseCompletedOrderDetails(
				JToken tradeHistory
		) =>
				tradeHistory.Select(
						o =>
								new ExchangeOrderResult
								{
									OrderId = o["orderId"].ToStringInvariant(),
									MarketSymbol = o["symbol"].ToStringInvariant(),
									Amount = o["quantity"].ConvertInvariant<decimal>(),
									IsBuy = o["side"].ToStringLowerInvariant() != "sell",
									OrderDate = o["createTime"]
												.ConvertInvariant<long>()
												.UnixTimeStampToDateTimeMilliseconds(),
									Price = o["price"].ConvertInvariant<decimal>(),
									Result = ExchangeAPIOrderResult.Filled,
									ClientOrderId = o["clientOrderId"].ToStringInvariant(),
									Fees = o["feeAmount"].ConvertInvariant<decimal>(),
									FeesCurrency = o["feeCurrency"].ToStringInvariant()
								}
				);

		private async Task<ExchangeTicker> ParseTickerWebSocketAsync(string symbol, JToken token)
		{
			// {
			// 	"symbol": "ETH_USDT",
			// 	"dailyChange": "0.9428",
			// 	"high": "507",
			// 	"amount": "20",
			// 	"quantity": "3",
			// 	"tradeCount": 11,
			// 	"low": "16",
			// 	"closeTime": 1634062351868,
			// 	"startTime": 1633996800000,
			// 	"close": "204",
			// 	"open": "105",
			// 	"ts": 1648052794867,
			// 	"markPrice": "205",
			// }

			return await this.ParseTickerAsync(
					token,
					symbol,
					askKey: null,
					bidKey: null,
					lastKey: "close",
					baseVolumeKey: "quantity",
					quoteVolumeKey: "amount",
					timestampKey: "ts",
					TimestampType.UnixMilliseconds
			);
		}

		public override string PeriodSecondsToString(int seconds)
		{
			var allowedPeriods = new[]
			{
								"MINUTE_1",
								"MINUTE_5",
								"MINUTE_10",
								"MINUTE_15",
								"MINUTE_30",
								"HOUR_1",
								"HOUR_2",
								"HOUR_4",
								"HOUR_6",
								"HOUR_12",
								"DAY_1",
								"DAY_3",
								"WEEK_1",
								"MONTH_1"
						};
			var period = CryptoUtility.SecondsToPeriodStringLongReverse(seconds);
			var periodIsvalid = allowedPeriods.Any(x => x == period);
			if (!periodIsvalid)
				throw new ArgumentOutOfRangeException(
						nameof(period),
						$"{period} is not valid period on Poloniex"
				);

			return period;
		}

		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				payload.Remove("nonce");
				var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
				var sig = string.Empty;
				switch (request.Method)
				{
					case "GET":
					case "DELETE":
						{
							payload["signTimestamp"] = timestamp;
							var form = payload.GetFormForPayload();
							sig =
									$"{request.Method}\n"
									+ $"{request.RequestUri.PathAndQuery}\n"
									+ $"{form}";

							await request.WriteToRequestAsync(form);
							break;
						}
					case "POST":
						{
							var pl =
									$"requestBody={payload.GetJsonForPayload()}&signTimestamp={timestamp}";
							sig =
									$"{request.Method}\n"
									+ $"{request.RequestUri.PathAndQuery}\n"
									+ $"{pl}";
							await request.WritePayloadJsonToRequestAsync(payload);
							break;
						}
				}

				request.AddHeader("key", PublicApiKey.ToUnsecureString());
				request.AddHeader(
						"signature",
						CryptoUtility.SHA256SignBase64(sig, PrivateApiKey.ToUnsecureBytesUTF8())
				);
				request.AddHeader("signTimestamp", timestamp.ToStringInvariant());
			}
		}

		protected override async Task<
				IReadOnlyDictionary<string, ExchangeCurrency>
		> OnGetCurrenciesAsync()
		{
			//https://api.poloniex.com/v2/currencies
			// [
			// {
			//  "id": 1,
			//  "coin": "1CR",
			//  "delisted": true,
			//  "tradeEnable": false,
			//  "name": "1CRedit",
			//  "networkList": [
			//  {
			//   "id": 1,
			//   "coin": "1CR",
			//   "name": "1CRedit",
			//   "currencyType": "address",
			//   "blockchain": "1CR",
			//   "withdrawalEnable": false,
			//   "depositEnable": false,
			//   "depositAddress": null,
			//   "withdrawMin": null,
			//   "decimals": 8,
			//   "withdrawFee": "0.01000000",
			//   "minConfirm": 10000
			//  }
			//  ]
			// }
			// ]

			var currencies = new Dictionary<string, ExchangeCurrency>();
			var result = await MakeJsonRequestAsync<JToken>("/v2/currencies");
			foreach (var c in result)
			{
				var currency = new ExchangeCurrency
				{
					Name = c["coin"].ToStringInvariant(),
					FullName = c["name"].ToStringInvariant(),
					TxFee = c["networkList"][0]["withdrawFee"].ConvertInvariant<decimal>(),
					DepositEnabled = c["networkList"][0]["depositEnable"].ConvertInvariant<bool>(),
					WithdrawalEnabled = c["networkList"][0][
								"withdrawalEnable"
						].ConvertInvariant<bool>(),
					MinWithdrawalSize = (
								c["networkList"][0]["withdrawMin"] ?? 0
						).ConvertInvariant<decimal>(),
					BaseAddress = (
								c["networkList"][0]["depositAddress"] ?? string.Empty
						).ToStringInvariant(),
					MinConfirmations = (
								c["networkList"][0]["minConfirm"] ?? 0
						).ConvertInvariant<int>()
				};
				currencies[currency.Name] = currency;
			}

			return currencies;
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await GetMarketSymbolsMetadataAsync())
					.Where(x => x.IsActive.Value)
					.Select(x => x.MarketSymbol);
		}

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			//https://api.poloniex.com/markets
			// [
			// {
			// 	"symbol": "BTC_USDT",
			// 	"baseCurrencyName": "BTC",
			// 	"quoteCurrencyName": "USDT",
			// 	"displayName": "BTC/USDT",
			// 	"state": "NORMAL",
			// 	"visibleStartTime": 1659018819512,
			// 	"tradableStartTime": 1659018819512,
			// 	"symbolTradeLimit": {
			// 		"symbol": "BTC_USDT",
			// 		"priceScale": 2,
			// 		"quantityScale": 6, - base
			// 		"amountScale": 2, - quote
			// 		"minQuantity": "0.000001" - base,
			// 		"minAmount": "1", - quote
			// 		"highestBid": "0",
			// 		"lowestAsk": "0"
			// 	},
			// 	"crossMargin": {
			// 		"supportCrossMargin": true,
			// 		"maxLeverage": 3
			// 	}
			// ]

			var symbols = await MakeJsonRequestAsync<JToken>("/markets");

			return symbols.Select(
					symbol =>
							new ExchangeMarket
							{
								MarketSymbol = symbol["symbol"].ToStringInvariant(),
								IsActive = ParsePairState(symbol["state"].ToStringInvariant()),
								BaseCurrency = symbol["baseCurrencyName"].ToStringInvariant(),
								QuoteCurrency = symbol["quoteCurrencyName"].ToStringInvariant(),
								MinTradeSize = symbol["symbolTradeLimit"]["minQuantity"].Value<decimal>(),
								MaxTradeSize = decimal.MaxValue,
								MinTradeSizeInQuoteCurrency = symbol["symbolTradeLimit"][
											"minAmount"
									].Value<decimal>(),
								MinPrice = CryptoUtility.PrecisionToStepSize(
											symbol["symbolTradeLimit"]["priceScale"].Value<decimal>()
									),
								MaxPrice = decimal.MaxValue,
								PriceStepSize = CryptoUtility.PrecisionToStepSize(
											symbol["symbolTradeLimit"]["priceScale"].Value<decimal>()
									),
								QuantityStepSize = CryptoUtility.PrecisionToStepSize(
											symbol["symbolTradeLimit"]["quantityScale"].Value<decimal>()
									),
								MarginEnabled = symbol["crossMargin"]["supportCrossMargin"].Value<bool>()
							}
			);
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var tickers = await GetTickersAsync();
			foreach (var kv in tickers)
			{
				if (kv.Key == marketSymbol)
				{
					return kv.Value;
				}
			}

			return null;
		}

		protected override async Task<
				IEnumerable<KeyValuePair<string, ExchangeTicker>>
		> OnGetTickersAsync()
		{
			//https://api.poloniex.com/markets/ticker24h
			// [ {
			//  "symbol" : "BTS_BTC",
			//  "open" : "0.0000005026",
			//  "low" : "0.0000004851",
			//  "high" : "0.0000005799",
			//  "close" : "0.0000004851",
			//  "quantity" : "34444",
			//  "amount" : "0.0179936481",
			//  "tradeCount" : 48,
			//  "startTime" : 1676918100000,
			//  "closeTime" : 1677004501011,
			//  "displayName" : "BTS/BTC",
			//  "dailyChange" : "-0.0348",
			//  "bid" : "0.0000004852",
			//  "bidQuantity" : "725",
			//  "ask" : "0.0000004962",
			//  "askQuantity" : "238",
			//  "ts" : 1677004503839,
			//  "markPrice" : "0.000000501"
			// }]
			var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
			var tickerResponse = await MakeJsonRequestAsync<JToken>("/markets/ticker24h");
			foreach (var instrument in tickerResponse)
			{
				var symbol = instrument["symbol"].ToStringInvariant();
				var ticker = await this.ParseTickerAsync(
						instrument,
						symbol,
						askKey: "ask",
						bidKey: "bid",
						baseVolumeKey: "quantity",
						lastKey: "close",
						quoteVolumeKey: "amount",
						timestampKey: "ts",
						timestampType: TimestampType.UnixMilliseconds
				);
				tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));
			}

			return tickers;
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			//https://api.poloniex.com/markets/{symbol}/orderBook?scale={scale}&limit={limit}
			// {
			//  "time" : 1677005825632,
			//  "scale" : "0.01",
			//  "asks" : [ "24702.89", "0.046082", "24702.90", "0.001681", "24703.09", "0.002037", "24710.10", "0.143572", "24712.18", "0.00118", "24713.68", "0.606951", "24724.80", "0.133", "24728.93", "0.7", "24728.94", "0.4", "24737.10", "0.135203" ],
			//  "bids" : [ "24700.03", "1.006472", "24700.02", "0.001208", "24698.71", "0.607319", "24697.99", "0.001973", "24688.50", "0.133", "24679.41", "0.4", "24679.40", "0.135", "24678.55", "0.3", "24667.00", "0.262", "24661.39", "0.14" ],
			//  "ts" : 1677005825637
			// }
			var response = await MakeJsonRequestAsync<JToken>(
					$"/markets/{marketSymbol}/orderBook?limit={maxCount}"
			);
			return response.ParseOrderBookFromJTokenArray();
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(
				string marketSymbol,
				int? limit = null
		)
		{
			//https://api.poloniex.com/markets/{symbol}/trades?limit={limit}
			// Returns a list of recent trades, request param limit is optional, its default value is 500, and max value is 1000.
			// [
			// {
			// 	"id": "194",
			// 	"price": "1.9",
			// 	"quantity": "110",
			// 	"amount": "209.00",
			// 	"takerSide": "SELL",
			// 	"ts": 1648690080545,
			// 	"createTime": 1648634905695
			// }
			// ]

			limit = (limit == null || limit < 1 || limit > 1000) ? 1000 : limit;

			var tradesResponse = await MakeJsonRequestAsync<JToken>(
					$"/markets/{marketSymbol}/trades?limit={limit}"
			);

			var trades = tradesResponse
					.Select(
							t =>
									t.ParseTrade(
											amountKey: "amount",
											priceKey: "price",
											typeKey: "takerSide",
											timestampKey: "ts",
											TimestampType.UnixMilliseconds,
											idKey: "id",
											typeKeyIsBuyValue: "BUY"
									)
					)
					.ToList();

			return trades;
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string marketSymbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			//https://api.poloniex.com/markets/{symbol}/candles?interval={interval}&limit={limit}&startTime={startTime}&endTime={endTime}
			// [
			// [
			// "45218",
			// "47590.82",
			// "47009.11",
			// "45516.6",
			// "13337805.8",
			// "286.639111",
			// "0",
			// "0",
			// 0,
			// 0,
			// "46531.7",
			// "DAY_1",
			// 1648684800000,
			// 1648771199999
			//  ]
			//  ]
			limit = (limit == null || limit < 1 || limit > 500) ? 500 : limit;
			var period = PeriodSecondsToString(periodSeconds);
			var url = $"/markets/{marketSymbol}/candles?interval={period}&limit={limit}";
			if (startDate != null)
			{
				url =
						$"{url}&startTime={new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds()}";
			}

			if (endDate != null)
			{
				url = $"{url}&endTime={new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds()}";
			}

			var candleResponse = await MakeJsonRequestAsync<JToken>(url);
			return candleResponse.Select(
					cr =>
							this.ParseCandle(
									cr,
									marketSymbol,
									periodSeconds,
									2,
									1,
									0,
									3,
									12,
									TimestampType.UnixMilliseconds,
									5,
									4,
									10
							)
			);
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var response = await MakeJsonRequestAsync<JToken>(
					"/accounts/balances",
					payload: await GetNoncePayloadAsync()
			);
			return (response[0]?["balances"] ?? throw new InvalidOperationException())
					.Select(
							x =>
									new
									{
										Currency = x["currency"].Value<string>(),
										TotalBalance = x["available"].Value<decimal>()
													+ x["hold"].Value<decimal>()
									}
					)
					.ToDictionary(k => k.Currency, v => v.TotalBalance);
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			var response = await MakeJsonRequestAsync<JToken>(
					"/accounts/balances",
					payload: await GetNoncePayloadAsync()
			);
			return (response[0]?["balances"] ?? throw new InvalidOperationException())
					.Select(
							x =>
									new
									{
										Currency = x["currency"].Value<string>(),
										AvailableBalance = x["available"].Value<decimal>()
									}
					)
					.ToDictionary(k => k.Currency, v => v.AvailableBalance);
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances)
		{
			var response = await MakeJsonRequestAsync<JToken>(
					"/margin/borrowStatus",
					payload: await GetNoncePayloadAsync()
			);
			var balances = response.Select(
					x =>
							new
							{
								Currency = x["currency"].Value<string>(),
								AvailableBalance = x["available"].Value<decimal>()
							}
			);
			if (includeZeroBalances)
			{
				balances = balances.Where(x => x.AvailableBalance > 0);
			}

			return balances.ToDictionary(k => k.Currency, v => v.AvailableBalance);
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			if (order.Price == null && order.OrderType != OrderType.Market)
				throw new ArgumentNullException(nameof(order.Price));
			var orderAmount = await ClampOrderQuantity(order.MarketSymbol, order.Amount);
			var orderPrice = order.Price.GetValueOrDefault();
			if (order.OrderType != OrderType.Market)
			{
				orderPrice = await ClampOrderPrice(order.MarketSymbol, order.Price!.Value);
			}

			var payload = await GetNoncePayloadAsync();

			payload["symbol"] = order.MarketSymbol;
			payload["side"] = order.IsBuy ? "BUY" : "SELL";
			payload["quantity"] = orderAmount;

			if (!string.IsNullOrEmpty(order.ClientOrderId))
			{
				payload["clientOrderId"] = order.ClientOrderId;
			}

			if (order.IsPostOnly.GetValueOrDefault())
			{
				payload["type"] = "LIMIT_MAKER";
			}

			switch (order.OrderType)
			{
				case OrderType.Limit when !order.IsPostOnly.GetValueOrDefault():
					payload["type"] = "LIMIT";
					payload["price"] = orderPrice;
					break;
				case OrderType.Limit when order.IsPostOnly.GetValueOrDefault():
					payload["type"] = "LIMIT_MAKER";
					break;
				case OrderType.Market:
					payload["type"] = "MARKET";
					break;
				case OrderType.Stop:
				default:
					throw new ArgumentOutOfRangeException(nameof(order.OrderType));
			}

			foreach (var kvp in order.ExtraParameters)
			{
				payload[kvp.Key] = kvp.Value;
			}

			var response = await MakeJsonRequestAsync<JToken>(
					"/orders",
					payload: payload,
					requestMethod: "POST"
			);
			var orderInfo = await GetOrderDetailsAsync(response["id"].ToStringInvariant());

			return orderInfo;
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			var query = !string.IsNullOrEmpty(marketSymbol) ? $"?symbol={marketSymbol}" : "";
			var result = await MakeJsonRequestAsync<JToken>(
					$"/orders{query}",
					payload: await GetNoncePayloadAsync()
			);

			return result.Select(o => ParseOrder(o, marketSymbol)).ToList();
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			var result = await MakeJsonRequestAsync<JToken>(
					$"/orders/{orderId}",
					payload: await GetNoncePayloadAsync()
			);
			return ParseOrder(result);
		}

		protected override async Task<
				IEnumerable<ExchangeOrderResult>
		> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			var query = !string.IsNullOrEmpty(marketSymbol) ? $"?symbol={marketSymbol}" : "";
			if (afterDate != null)
			{
				var startDateParam =
						$"startDate={new DateTimeOffset(afterDate.Value).ToUnixTimeMilliseconds()}";
				query = query.Length > 0 ? $"?{startDateParam}" : $"&{startDateParam}";
			}

			var result = await MakeJsonRequestAsync<JToken>(
					$"/trades{query}",
					payload: await GetNoncePayloadAsync()
			);

			return ParseCompletedOrderDetails(result);
		}

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			await MakeJsonRequestAsync<JToken>(
					$"/orders/{orderId}",
					payload: await GetNoncePayloadAsync(),
					requestMethod: "DELETE"
			);
		}

		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(
				ExchangeWithdrawalRequest withdrawalRequest
		)
		{
			// If we have an address tag, verify that Polo lets you specify it as part of the withdrawal
			if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
			{
				if (
						!WithdrawalFieldCount.TryGetValue(
								withdrawalRequest.Currency,
								out int fieldCount
						)
						|| fieldCount == 0
				)
				{
					throw new APIException(
							$"Coin {withdrawalRequest.Currency} has unknown withdrawal field count. Please manually verify the number of fields allowed during a withdrawal (Address + Tag = 2) and add it to PoloniexWithdrawalFields.csv before calling Withdraw"
					);
				}
				else if (fieldCount == 1)
				{
					throw new APIException(
							$"Coin {withdrawalRequest.Currency} only allows an address to be specified and address tag {withdrawalRequest.AddressTag} was provided."
					);
				}
				else if (fieldCount > 2)
				{
					throw new APIException("More than two fields on a withdrawal is unsupported.");
				}
			}

			var paramsList = new List<object>
						{
								"currency",
								NormalizeMarketSymbol(withdrawalRequest.Currency),
								"amount",
								withdrawalRequest.Amount,
								"address",
								withdrawalRequest.Address
						};
			if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
			{
				paramsList.Add("paymentId");
				paramsList.Add(withdrawalRequest.AddressTag);
			}

			var token = await MakePrivateAPIRequestAsync("withdraw", paramsList.ToArray());

			return new ExchangeWithdrawalResponse
			{
				Id = token["withdrawalNumber"]?.ToString(),
				Message = token["response"].ToStringInvariant()
			};
		}

		protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(
				string currency,
				bool forceRegenerate = false
		)
		{
			// Never reuse IOTA addresses
			if (currency.Equals("MIOTA", StringComparison.OrdinalIgnoreCase))
			{
				forceRegenerate = true;
			}

			IReadOnlyDictionary<string, ExchangeCurrency> currencies = await GetCurrenciesAsync();
			var depositAddresses = new Dictionary<string, ExchangeDepositDetails>(
					StringComparer.OrdinalIgnoreCase
			);
			if (
					!forceRegenerate
					&& !(await TryFetchExistingAddresses(currency, currencies, depositAddresses))
			)
			{
				return null;
			}

			if (!depositAddresses.TryGetValue(currency, out var depositDetails))
			{
				depositDetails = await CreateDepositAddress(currency, currencies);
			}

			return depositDetails;
		}

		/// <summary>Gets the deposit history for a symbol</summary>
		/// <param name="currency">(ignored) The currency to check.</param>
		/// <returns>Collection of ExchangeCoinTransfers</returns>
		protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(
				string currency
		)
		{
			JToken result = await MakePrivateAPIRequestAsync(
					"returnDepositsWithdrawals",
					new object[]
					{
										"start",
										DateTime.MinValue.ToUniversalTime().UnixTimestampFromDateTimeSeconds(),
										"end",
										CryptoUtility.UtcNow.UnixTimestampFromDateTimeSeconds()
					}
			);

			var transactions = new List<ExchangeTransaction>();

			foreach (JToken token in result["deposits"])
			{
				var deposit = new ExchangeTransaction
				{
					Currency = token["currency"].ToStringUpperInvariant(),
					Address = token["address"].ToStringInvariant(),
					Amount = token["amount"].ConvertInvariant<decimal>(),
					BlockchainTxId = token["txid"].ToStringInvariant(),
					Timestamp = token["timestamp"]
								.ConvertInvariant<double>()
								.UnixTimeStampToDateTimeSeconds()
				};

				string status = token["status"].ToStringUpperInvariant();
				switch (status)
				{
					case "COMPLETE":
						deposit.Status = TransactionStatus.Complete;
						break;
					case "PENDING":
						deposit.Status = TransactionStatus.Processing;
						break;
					default:
						// TODO: API Docs don't specify what other options there will be for transaction status
						deposit.Status = TransactionStatus.Unknown;
						deposit.Notes = "Transaction status: " + status;
						break;
				}

				transactions.Add(deposit);
			}

			return transactions;
		}

		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(
				Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback,
				params string[] symbols
		) =>
				await ConnectWebsocketPublicAsync(
						async (socket) =>
						{
							await SubscribeToChannel(socket, "ticker", symbols);
						},
						async (socket, symbol, sArray, token) =>
						{
							var tickers = new List<KeyValuePair<string, ExchangeTicker>>
								{
												new KeyValuePair<string, ExchangeTicker>(
														symbol,
														await this.ParseTickerWebSocketAsync(symbol, token)
												)
								};
							callback(tickers);
						}
				);

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		) =>
				await ConnectWebsocketPublicAsync(
						async (socket) =>
						{
							await SubscribeToChannel(socket, "trades", marketSymbols);
						},
						async (socket, symbol, sArray, token) =>
						{
							var trade = token.ParseTrade(
											amountKey: "quantity",
											priceKey: "price",
											typeKey: "takerSide",
											timestampKey: "ts",
											TimestampType.UnixMilliseconds,
											idKey: "id"
									);
							await callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
						}
				);

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(
				Action<ExchangeOrderBook> callback,
				int maxCount = 20,
				params string[] marketSymbols
		) =>
				await ConnectWebsocketPublicAsync(
						async (socket) =>
						{
							await SubscribeToOrderBookDepthChannel(socket, marketSymbols, maxCount);
						},
						(socket, symbol, sArray, token) =>
						{
							var book = token.ParseOrderBookFromJTokenArrays();
							book.MarketSymbol = symbol;
							callback(book);
							return Task.CompletedTask;
						}
				);

		protected override async Task<IWebSocket> OnGetCandlesWebSocketAsync(
				Func<MarketCandle, Task> callback,
				int periodSeconds,
				params string[] marketSymbols
		) =>
				await ConnectWebsocketPublicAsync(
						async (socket) =>
						{
							await SubscribeToChannel(
											socket,
											$"candles_{CryptoUtility.SecondsToPeriodStringLongReverse(periodSeconds).ToLowerInvariant()}",
											marketSymbols
									);
						},
						async (socket, symbol, sArray, token) =>
						{
							var candle = this.ParseCandle(
											token,
											symbol,
											periodSeconds,
											"open",
											"high",
											"low",
											"close",
											"ts",
											TimestampType.UnixMilliseconds,
											"quantity",
											"amount",
											null,
											"tradeCount"
									);

							await callback(candle);
						}
				);

		private static string ParseFeesCurrency(bool isBuy, string symbol)
		{
			string feesCurrency = null;
			string[] currencies = symbol.Split('_');
			if (currencies.Length == 2)
			{
				// fees are in the "To" currency
				feesCurrency = isBuy ? currencies[1] : currencies[0];
			}

			return feesCurrency;
		}

		private async Task<bool> TryFetchExistingAddresses(
				string currency,
				IReadOnlyDictionary<string, ExchangeCurrency> currencies,
				Dictionary<string, ExchangeDepositDetails> depositAddresses
		)
		{
			JToken result = await MakePrivateAPIRequestAsync("returnDepositAddresses");
			foreach (JToken jToken in result)
			{
				var token = (JProperty)jToken;
				var details = new ExchangeDepositDetails { Currency = token.Name };

				if (
						!TryPopulateAddressAndTag(
								currency,
								currencies,
								details,
								token.Value.ToStringInvariant()
						)
				)
				{
					return false;
				}

				depositAddresses[details.Currency] = details;
			}

			return true;
		}

		private static bool TryPopulateAddressAndTag(
				string currency,
				IReadOnlyDictionary<string, ExchangeCurrency> currencies,
				ExchangeDepositDetails details,
				string address
		)
		{
			if (currencies.TryGetValue(currency, out ExchangeCurrency coin))
			{
				if (!string.IsNullOrWhiteSpace(coin.BaseAddress))
				{
					details.Address = coin.BaseAddress;
					details.AddressTag = address;
				}
				else
				{
					details.Address = address;
				}

				return true;
			}

			// Cannot find currency in master list.
			// Stay safe and don't return a possibly half-baked deposit address missing a tag
			return false;
		}

		private static bool ParsePairState(string state)
		{
			if (string.IsNullOrWhiteSpace(state))
				return false;

			return state == "NORMAL";
		}

		private static ExchangeAPIOrderResult ParseOrderStatus(string status) =>
				status.ToUpperInvariant() switch
				{
					"NEW" => ExchangeAPIOrderResult.Open,
					"PARTIALLY_FILLED" => ExchangeAPIOrderResult.FilledPartially,
					"FILLED" => ExchangeAPIOrderResult.Filled,
					"PENDING_CANCEL" => ExchangeAPIOrderResult.PendingCancel,
					"PARTIALLY_CANCELED" => ExchangeAPIOrderResult.FilledPartiallyAndCancelled,
					"CANCELED" => ExchangeAPIOrderResult.Canceled,
					"FAILED" => ExchangeAPIOrderResult.Rejected,
					_ => ExchangeAPIOrderResult.Unknown
				};

		/// <summary>
		/// Create a deposit address
		/// </summary>
		/// <param name="currency">Currency to create an address for</param>
		/// <param name="currencies">Lookup of existing currencies</param>
		/// <returns>ExchangeDepositDetails with an address or a BaseAddress/AddressTag pair.</returns>
		private async Task<ExchangeDepositDetails> CreateDepositAddress(
				string currency,
				IReadOnlyDictionary<string, ExchangeCurrency> currencies
		)
		{
			JToken result = await MakePrivateAPIRequestAsync(
					"generateNewAddress",
					new object[] { "currency", currency }
			);
			var details = new ExchangeDepositDetails { Currency = currency, };

			if (
					!TryPopulateAddressAndTag(
							currency,
							currencies,
							details,
							result["response"].ToStringInvariant()
					)
			)
			{
				return null;
			}

			return details;
		}

		private Task<IWebSocket> ConnectWebsocketPublicAsync(
				Func<IWebSocket, Task> connected,
				Func<IWebSocket, string, string[], JToken, Task> callback
		)
		{
			Timer pingTimer = null;
			return ConnectPublicWebSocketAsync(
					url: "/public",
					messageCallback: async (socket, msg) =>
					{
						var token = JToken.Parse(msg.ToStringFromUTF8());
						var eventType = token["event"]?.ToStringInvariant();
						if (eventType != null)
						{
							if (eventType != "error")
								return;
							Logger.Info(
												"Websocket unable to connect: " + token["msg"]?.ToStringInvariant()
										);
							return;
						}

						if (token["data"] == null)
							return;

						foreach (var d in token["data"])
						{
							await callback(socket, d["symbol"]?.ToStringInvariant(), null, d);
						}
					},
					connectCallback: async (socket) =>
					{
						await connected(socket);
						pingTimer ??= new Timer(
											callback: async s =>
													await socket.SendMessageAsync(
															JsonConvert.SerializeObject(
																	new { Event = "ping" },
																	SerializerSettings
															)
													),
											null,
											0,
											15000
									);
					},
					disconnectCallback: socket =>
					{
						pingTimer?.Dispose();
						pingTimer = null;
						return Task.CompletedTask;
					}
			);
		}

		private static async Task SubscribeToChannel(
				IWebSocket socket,
				string channel,
				string[] marketSymbols
		)
		{
			if (marketSymbols.Length == 0)
			{
				marketSymbols = new[] { "all" };
			}

			var payload = JsonConvert.SerializeObject(
					new
					{
						Event = "subscribe",
						Channel = new[] { channel },
						Symbols = marketSymbols
					},
					SerializerSettings
			);

			await socket.SendMessageAsync(payload);
		}

		private async Task SubscribeToOrderBookDepthChannel(
				IWebSocket socket,
				string[] marketSymbols,
				int depth = 20
		)
		{
			var depthIsValid = depth == 5 || depth == 10 || depth == 20;
			if (!depthIsValid)
				throw new ArgumentOutOfRangeException(nameof(depth));
			if (marketSymbols.Length == 0)
			{
				marketSymbols = (await OnGetMarketSymbolsAsync()).ToArray();
			}

			var payload = JsonConvert.SerializeObject(
					new
					{
						Event = "subscribe",
						Channel = new[] { "book" },
						Symbols = marketSymbols,
						Depth = depth
					},
					SerializerSettings
			);

			await socket.SendMessageAsync(payload);
		}
	}

	public partial class ExchangeName
	{
		public const string Poloniex = "Poloniex";
	}
}
