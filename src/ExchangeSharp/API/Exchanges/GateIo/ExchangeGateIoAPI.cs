/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public partial class ExchangeName
	{
		public const string GateIo = "GateIo";
	}

	public sealed class ExchangeGateIoAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.gateio.ws/api/v4";
		public override string BaseUrlWebSocket { get; set; } = "wss://api.gateio.ws/ws/v4/";

		public ExchangeGateIoAPI()
		{
			MarketSymbolSeparator = "_";
			RateLimit = new RateGate(300, TimeSpan.FromSeconds(1));
			RequestContentType = "application/json";
		}

		protected override async Task<
				IEnumerable<KeyValuePair<string, ExchangeTicker>>
		> OnGetTickersAsync()
		{
			var json = await MakeJsonRequestAsync<JToken>("/spot/tickers");

			var tickers = json.Select(tickerToken => ParseTicker(tickerToken))
					.Select(
							ticker => new KeyValuePair<string, ExchangeTicker>(ticker.MarketSymbol, ticker)
					)
					.ToList();

			return tickers;
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(
				string symbol,
				int? limit = null
		)
		{
			var trades = new List<ExchangeTrade>();
			int maxRequestLimit = (limit == null || limit < 1 || limit > 100) ? 100 : (int)limit;
			var json = await MakeJsonRequestAsync<JToken>(
					$"/spot/trades?currency_pair={symbol}&limit={maxRequestLimit}"
			);

			foreach (JToken tradeToken in json)
			{
				/*
						{
								"id": "1232893232",
								"create_time": "1548000000",
								"create_time_ms": "1548000000123.456",
								"order_id": "4128442423",
								"side": "buy",
								"role": "maker",
								"amount": "0.15",
								"price": "0.03",
								"fee": "0.0005",
								"fee_currency": "ETH",
								"point_fee": "0",
								"gt_fee": "0"
						}
				*/

				trades.Add(
						tradeToken.ParseTrade(
								"amount",
								"price",
								"side",
								"create_time_ms",
								TimestampType.UnixMillisecondsDouble,
								"id"
						)
				);
			}
			return trades;
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			List<string> symbols = new List<string>();
			JToken? obj = await MakeJsonRequestAsync<JToken>("/spot/currency_pairs");
			if (!(obj is null))
			{
				foreach (JToken token in obj)
				{
					if (token["trade_status"].ToStringLowerInvariant() == "tradable")
						symbols.Add(token["id"].ToStringInvariant());
				}
			}
			return symbols;
		}

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			/*
					{
							"id": "ETH_USDT",
							"base": "ETH",
							"quote": "USDT",
							"fee": "0.2",
							"min_base_amount": "0.001",
							"min_quote_amount": "1.0",
							"amount_precision": 3,
							"precision": 6,
							"trade_status": "tradable",
							"sell_start": 1516378650,
							"buy_start": 1516378650
					}
			*/

			var markets = new List<ExchangeMarket>();
			JToken obj = await MakeJsonRequestAsync<JToken>("/spot/currency_pairs");

			if (!(obj is null))
			{
				foreach (JToken marketSymbolToken in obj)
				{
					var market = new ExchangeMarket
					{
						MarketSymbol = marketSymbolToken["id"].ToStringUpperInvariant(),
						IsActive =
									marketSymbolToken["trade_status"].ToStringLowerInvariant()
									== "tradable",
						QuoteCurrency = marketSymbolToken["quote"].ToStringUpperInvariant(),
						BaseCurrency = marketSymbolToken["base"].ToStringUpperInvariant(),
					};
					int pricePrecision = marketSymbolToken["precision"].ConvertInvariant<int>();
					market.PriceStepSize = (decimal)Math.Pow(0.1, pricePrecision);
					int quantityPrecision = marketSymbolToken[
							"amount_precision"
					].ConvertInvariant<int>();
					market.QuantityStepSize = (decimal)Math.Pow(0.1, quantityPrecision);

					market.MinTradeSizeInQuoteCurrency = marketSymbolToken[
							"min_quote_amount"
					].ConvertInvariant<decimal>();
					market.MinTradeSize = marketSymbolToken[
							"min_base_amount"
					].ConvertInvariant<decimal>();

					markets.Add(market);
				}
			}

			return markets;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
		{
			var json = await MakeJsonRequestAsync<JToken>($"/spot/tickers?currency_pair={symbol}");
			return ParseTicker(json.First());
		}

		private ExchangeTicker ParseTicker(JToken tickerToken)
		{
			bool IsEmptyString(JToken token) =>
					token.Type == JTokenType.String && token.ToObject<string>() == string.Empty;

			/*
					{
							"currency_pair": "BTC3L_USDT",
							"last": "2.46140352",
							"lowest_ask": "2.477",
							"highest_bid": "2.4606821",
							"change_percentage": "-8.91",
							"base_volume": "656614.0845820589",
							"quote_volume": "1602221.66468375534639404191",
							"high_24h": "2.7431",
							"low_24h": "1.9863",
							"etf_net_value": "2.46316141",
							"etf_pre_net_value": "2.43201848",
							"etf_pre_timestamp": 1611244800,
							"etf_leverage": "2.2803019447281203"
					}
			*/

			return new ExchangeTicker
			{
				Exchange = Name,
				MarketSymbol = tickerToken["currency_pair"].ToStringInvariant(),
				Bid = IsEmptyString(tickerToken["lowest_ask"])
							? default
							: tickerToken["lowest_ask"].ConvertInvariant<decimal>(),
				Ask = IsEmptyString(tickerToken["highest_bid"])
							? default
							: tickerToken["highest_bid"].ConvertInvariant<decimal>(),
				Last = tickerToken["last"].ConvertInvariant<decimal>(),
			};
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string symbol,
				int maxCount = 100
		)
		{
			var json = await MakeJsonRequestAsync<JToken>(
					$"/spot/order_book?currency_pair={symbol}"
			);

			/*
					{
					"id": 123456,
					"current": 1623898993123,
					"update": 1623898993121,
					"asks": [
							[
									"1.52",
									"1.151"
							],
							[
									"1.53",
									"1.218"
							]
					],
					"bids": [
							[
									"1.17",
									"201.863"
							],
							[
									"1.16",
									"725.464"
							]
					]
					}
			*/

			var orderBook = json.ParseOrderBookFromJTokenArrays(sequence: "current");
			orderBook.LastUpdatedUtc = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
					json["current"].ConvertInvariant<long>()
			);
			return orderBook;
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string symbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			string url =
					$"/spot/candlesticks?currency_pair={symbol}&interval={PeriodSecondsToString(periodSeconds)}";

			if (limit != null)
			{
				limit = (limit == null || limit < 1 || limit > 999) ? 999 : (int)limit;
				url += $"&limit={limit.ToStringInvariant()}";
			}

			if (startDate != null || endDate != null)
			{
				if (startDate == null)
				{
					startDate = endDate.Value.AddSeconds(periodSeconds * (limit ?? 999) * -1);
				}
				else if (endDate == null)
				{
					endDate = startDate.Value.AddSeconds(periodSeconds * (limit ?? 999));
				}
				else
				{
					if (endDate > startDate.Value.AddSeconds(periodSeconds * (limit ?? 999)))
					{
						endDate = startDate.Value.AddSeconds(periodSeconds * (limit ?? 999));
					}
				}
				url +=
						$"&from={((long)startDate.Value.UnixTimestampFromDateTimeSeconds()).ToStringInvariant()}";
				url +=
						$"&to={((long)endDate.Value.UnixTimestampFromDateTimeSeconds()).ToStringInvariant()}";
			}

			var json = await MakeJsonRequestAsync<JToken>(url);

			var candles = json.Select(
							candleToken =>
									new MarketCandle
									{
										Timestamp = CryptoUtility.ParseTimestamp(
													candleToken[0],
													TimestampType.UnixSeconds
											),
										BaseCurrencyVolume = candleToken[1].ConvertInvariant<double>(),
										ClosePrice = candleToken[2].ConvertInvariant<decimal>(),
										ExchangeName = Name,
										HighPrice = candleToken[3].ConvertInvariant<decimal>(),
										LowPrice = candleToken[4].ConvertInvariant<decimal>(),
										Name = symbol,
										OpenPrice = candleToken[5].ConvertInvariant<decimal>(),
										PeriodSeconds = periodSeconds,
									}
					)
					.ToList();

			return candles;
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var payload = await GetNoncePayloadAsync();
			var responseToken = await MakeJsonRequestAsync<JToken>(
					"/spot/accounts",
					payload: payload
			);
			return responseToken
					.Select(x => ParseBalance(x))
					.ToDictionary(x => x.currency, x => x.available + x.locked);
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			var payload = await GetNoncePayloadAsync();
			var responseToken = await MakeJsonRequestAsync<JToken>(
					"/spot/accounts",
					payload: payload
			);
			return responseToken
					.Select(x => ParseBalance(x))
					.ToDictionary(x => x.currency, x => x.available);
		}

		private (string currency, decimal available, decimal locked) ParseBalance(
				JToken balanceToken
		)
		{
			var currency = balanceToken["currency"].ToStringInvariant();
			var available = balanceToken["available"].ConvertInvariant<decimal>();
			var locked = balanceToken["locked"].ConvertInvariant<decimal>();

			return (currency, available, locked);
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			if (order.OrderType != OrderType.Limit)
				throw new NotSupportedException("Gate.io API supports only limit orders");

			var payload = await GetNoncePayloadAsync();
			AddOrderToPayload(order, payload);

			JToken responseToken = await MakeJsonRequestAsync<JToken>(
					"/spot/orders",
					payload: payload,
					requestMethod: "POST"
			);

			return ParseOrder(responseToken);
		}

		protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(
				params ExchangeOrderRequest[] orders
		)
		{
			var orderRequests = orders
					.Select(
							(order, i) =>
							{
								var subPayload = new Dictionary<string, object>();

								if (string.IsNullOrEmpty(order.ClientOrderId))
								{
									order.ClientOrderId =
														$"{CryptoUtility.UnixTimestampFromDateTimeMilliseconds(DateTime.Now)}-{i.ToStringInvariant()}";
								}
								AddOrderToPayload(order, subPayload);
								return subPayload;
							}
					)
					.ToList();

			var payload = await GetNoncePayloadAsync();
			payload[CryptoUtility.PayloadKeyArray] = orderRequests;

			var responseToken = await MakeJsonRequestAsync<JToken>(
					"/spot/batch_orders",
					payload: payload,
					requestMethod: "POST"
			);
			return responseToken.Select(x => ParseOrder(x)).ToArray();
		}

		private void AddOrderToPayload(
				ExchangeOrderRequest order,
				Dictionary<string, object> payload
		)
		{
			if (!string.IsNullOrEmpty(order.ClientOrderId))
			{
				payload.Add("text", $"t-{order.ClientOrderId}");
			}

			payload.Add("currency_pair", NormalizeMarketSymbol(order.MarketSymbol));
			payload.Add("type", order.OrderType.ToStringLowerInvariant());
			payload.Add("side", order.IsBuy ? "buy" : "sell");
			payload.Add("amount", order.Amount.ToStringInvariant());
			payload.Add("price", order.Price);
			if (order.IsPostOnly == true)
				payload["time_in_force"] += "poc"; // PendingOrCancelled, makes a post-only order that always enjoys a maker fee
		}

		private ExchangeOrderResult ParseOrder(JToken order)
		{
			decimal amount = order["amount"].ConvertInvariant<decimal>();
			decimal amountFilled = amount - order["left"].ConvertInvariant<decimal>();
			decimal? fillPrice =
					amountFilled == 0
							? null
							: (decimal?)(order["filled_total"].ConvertInvariant<decimal>() / amountFilled);
			decimal price = order["price"].ConvertInvariant<decimal>();
			var result = new ExchangeOrderResult
			{
				Amount = amount,
				AmountFilled = amountFilled,
				Price = price,
				AveragePrice = fillPrice,
				Message = string.Empty,
				OrderId = order["id"].ToStringInvariant(),
				OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
							order["create_time_ms"].ConvertInvariant<long>()
					),
				MarketSymbol = order["currency_pair"].ToStringInvariant(),
				IsBuy = order["side"].ToStringInvariant() == "buy",
				ClientOrderId = order["text"].ToStringInvariant(),
			};
			result.Result = ParseExchangeAPIOrderResult(
					order["status"].ToStringInvariant(),
					amountFilled
			);

			return result;
		}

		private static ExchangeAPIOrderResult ParseExchangeAPIOrderResult(
				string status,
				decimal amountFilled
		)
		{
			switch (status)
			{
				case "open":
					return ExchangeAPIOrderResult.Open;
				case "closed":
					return ExchangeAPIOrderResult.Filled;
				case "cancelled":
					return amountFilled > 0
							? ExchangeAPIOrderResult.FilledPartiallyAndCancelled
							: ExchangeAPIOrderResult.Canceled;
				default:
					throw new NotImplementedException($"Unexpected status type: {status}");
			}
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string symbol = null,
				bool isClientOrderId = false
		)
		{
			if (string.IsNullOrWhiteSpace(symbol))
			{
				throw new ArgumentNullException(
						"MarketSymbol is required for querying order details with Gate.io API"
				);
			}
			if (isClientOrderId)
				throw new NotSupportedException(
						"Querying by client order ID is not implemented in ExchangeSharp. Please submit a PR if you are interested in this feature"
				);

			var payload = await GetNoncePayloadAsync();
			var responseToken = await MakeJsonRequestAsync<JToken>(
					$"/spot/orders/{orderId}?currency_pair={symbol}",
					payload: payload
			);

			return ParseOrder(responseToken);
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string symbol = null
		)
		{
			if (string.IsNullOrWhiteSpace(symbol))
			{
				throw new ArgumentNullException(
						"MarketSymbol is required for querying open orders with Gate.io API"
				);
			}

			var payload = await GetNoncePayloadAsync();
			var responseToken = await MakeJsonRequestAsync<JToken>(
					$"/spot/orders?currency_pair={symbol}&status=open",
					payload: payload
			);
			return responseToken.Select(x => ParseOrder(x)).ToArray();
		}

		protected override async Task<
				IEnumerable<ExchangeOrderResult>
		> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
		{
			var payload = await GetNoncePayloadAsync();
			var url = $"/spot/orders?status=finished";
			if (!string.IsNullOrEmpty(symbol))
			{
				url += $"&currency_pair={symbol}";
			}
			if (afterDate.HasValue)
			{
				url +=
						$"&from={(long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(afterDate.Value)}";
				url +=
						$"&to={(long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(DateTime.Now)}";
			}
			var responseToken = await MakeJsonRequestAsync<JToken>(url, payload: payload);
			return responseToken.Select(x => ParseOrder(x)).ToArray();
		}

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string symbol = null,
				bool isClientOrderId = false
		)
		{
			if (string.IsNullOrWhiteSpace(symbol))
			{
				throw new ArgumentNullException(
						"MarketSymbol is required for cancelling order with Gate.io API"
				);
			}
			if (isClientOrderId)
				throw new NotSupportedException(
						"Cancelling by client order ID is not supported in ExchangeSharp. Please submit a PR if you are interested in this feature"
				);

			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			await MakeJsonRequestAsync<JToken>(
					$"/spot/orders/{orderId}?currency_pair={symbol}",
					BaseUrl,
					payload,
					"DELETE"
			);
		}

		string unixTimeInSeconds =>
				(
						(long)CryptoUtility.UnixTimestampFromDateTimeSeconds(DateTime.Now)
				).ToStringInvariant();

		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object>? payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				payload.Remove("nonce");

				request.AddHeader("KEY", PublicApiKey!.ToUnsecureString());
				request.AddHeader("Timestamp", unixTimeInSeconds);

				var privateApiKey = PrivateApiKey!.ToUnsecureString();

				var jsonForPayload = CryptoUtility.GetJsonForPayload(payload);
				var sourceBytes = Encoding.UTF8.GetBytes(jsonForPayload ?? "");

				using (SHA512 sha512Hash = SHA512.Create())
				{
					var hashBytes = sha512Hash.ComputeHash(sourceBytes);
					var bodyHash = BitConverter
							.ToString(hashBytes)
							.Replace("-", "")
							.ToLowerInvariant();
					var queryString = string.IsNullOrEmpty(request.RequestUri.Query)
							? ""
							: request.RequestUri.Query.Substring(1);
					var signatureString =
							$"{request.Method}\n{request.RequestUri.AbsolutePath}\n{queryString}\n{bodyHash}\n{unixTimeInSeconds}";

					using (HMACSHA512 hmac = new HMACSHA512(Encoding.UTF8.GetBytes(privateApiKey)))
					{
						var signature = CryptoUtility
								.SHA512Sign(signatureString, privateApiKey)
								.ToLowerInvariant();
						request.AddHeader("SIGN", signature);
					}
				}

				await CryptoUtility.WriteToRequestAsync(request, jsonForPayload);
			}
			else
			{
				await base.ProcessRequestAsync(request, payload);
			}
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync(true)).ToArray();
			}
			return await ConnectPublicWebSocketAsync(
					null,
					messageCallback: async (_socket, msg) =>
					{
						JToken parsedMsg = JToken.Parse(msg.ToStringFromUTF8());

						if (parsedMsg["channel"].ToStringInvariant().Equals("spot.trades"))
						{
							if (parsedMsg["error"] != null)
								throw new APIException(
													$"Exchange returned error: {parsedMsg["error"].ToStringInvariant()}"
											);
							else if (
												parsedMsg["result"]["status"].ToStringInvariant().Equals("success")
										)
							{
								// successfully subscribed to trade stream
							}
							else
							{
								var exchangeTrade = parsedMsg["result"].ParseTrade(
													"amount",
													"price",
													"side",
													"create_time_ms",
													TimestampType.UnixMillisecondsDouble,
													"id"
											);

								await callback(
													new KeyValuePair<string, ExchangeTrade>(
															parsedMsg["result"]["currency_pair"].ToStringInvariant(),
															exchangeTrade
													)
											);
							}
						}
					},
					connectCallback: async (_socket) =>
					{ /*{	"time": int(time.time()),
					"channel": "spot.trades",
					"event": "subscribe",  # "unsubscribe" for unsubscription
					"payload": ["BTC_USDT"]
				}*/
						// this doesn't work for some reason
						//await _socket.SendMessageAsync(new
						//{
						//	time = unixTimeInSeconds,
						//	channel = "spot.trades",
						//	@event = "subscribe",
						//	payload = marketSymbols,
						//});
						var quotedSymbols = marketSymbols.Select(s => $"\"{s}\"");
						var combinedString = string.Join(",", quotedSymbols);
						await _socket.SendMessageAsync(
											$"{{  \"time\": {unixTimeInSeconds},\"channel\": \"spot.trades\",\"event\": \"subscribe\",\"payload\": [{combinedString}]	}}"
									);
					}
			);
		}
	}
}
