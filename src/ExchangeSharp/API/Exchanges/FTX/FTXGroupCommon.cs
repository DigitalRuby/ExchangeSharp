using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public abstract class FTXGroupCommon : ExchangeAPI
	{
		#region [ Constructor(s) ]

		public FTXGroupCommon()
		{
			NonceStyle = NonceStyle.UnixMillisecondsString;
			MarketSymbolSeparator = "/";
			RequestContentType = "application/json";
		}

		#endregion

		#region [ Implementation ]

		/// <inheritdoc />
		protected async override Task OnCancelOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			var url = "/orders/";
			if (isClientOrderId)
			{
				url += "by_client_id/";
			}

			await MakeJsonRequestAsync<JToken>(
					$"{url}{orderId}",
					null,
					await GetNoncePayloadAsync(),
					"DELETE"
			);
		}

		/// <inheritdoc />
		protected async override Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var balances = new Dictionary<string, decimal>();

			JToken result = await MakeJsonRequestAsync<JToken>(
					"/wallet/balances",
					null,
					await GetNoncePayloadAsync()
			);

			foreach (JObject obj in result)
			{
				decimal amount = obj["total"].ConvertInvariant<decimal>();

				balances[obj["coin"].ToStringInvariant()] = amount;
			}

			return balances;
		}

		/// <inheritdoc />
		protected async override Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			// https://docs.ftx.com/#get-balances
			// NOTE there is also is "Get balances of all accounts"?
			// "coin": "USDTBEAR",
			// "free": 2320.2,
			// "spotBorrow": 0.0,
			// "total": 2340.2,
			// "usdValue": 2340.2,
			// "availableWithoutBorrow": 2320.2

			var balances = new Dictionary<string, decimal>();

			JToken result = await MakeJsonRequestAsync<JToken>(
					$"/wallet/balances",
					null,
					await GetNoncePayloadAsync()
			);

			foreach (JToken token in result.Children())
			{
				balances.Add(
						token["coin"].ToStringInvariant(),
						token["availableWithoutBorrow"].ConvertInvariant<decimal>()
				);
			}

			return balances;
		}

		/// <inheritdoc />
		protected async override Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string marketSymbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			//period options: 15, 60, 300, 900, 3600, 14400, 86400, or any multiple of 86400 up to 30*86400

			var queryUrl = $"/markets/{marketSymbol}/candles?resolution={periodSeconds}";

			if (startDate.HasValue)
			{
				queryUrl += $"&start_time={startDate?.UnixTimestampFromDateTimeSeconds()}";
			}

			if (endDate.HasValue)
			{
				queryUrl += $"&end_time={endDate?.UnixTimestampFromDateTimeSeconds()}";
			}

			var candles = new List<MarketCandle>();

			var response = await MakeJsonRequestAsync<JToken>(
					queryUrl,
					null,
					await GetNoncePayloadAsync()
			);

			foreach (JToken candle in response.Children())
			{
				var parsedCandle = this.ParseCandle(
						candle,
						marketSymbol,
						periodSeconds,
						"open",
						"high",
						"low",
						"close",
						"startTime",
						TimestampType.Iso8601UTC,
						"volume"
				);

				candles.Add(parsedCandle);
			}

			return candles;
		}

		/// <inheritdoc />
		protected async override Task<
				IEnumerable<ExchangeOrderResult>
		> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			string query = "/orders/history";

			string parameters = "";

			if (!string.IsNullOrEmpty(marketSymbol))
			{
				parameters += $"&market={marketSymbol}";
			}

			if (afterDate != null)
			{
				parameters += $"&start_time={afterDate?.UnixTimestampFromDateTimeSeconds()}";
			}

			if (!string.IsNullOrEmpty(parameters))
			{
				query += $"?{parameters}";
			}

			JToken response = await MakeJsonRequestAsync<JToken>(
					query,
					null,
					await GetNoncePayloadAsync()
			);

			var orders = new List<ExchangeOrderResult>();

			foreach (JToken token in response.Children())
			{
				var symbol = token["market"].ToStringInvariant();

				if (!Regex.Match(symbol, @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}

				orders.Add(ParseOrder(token));
			}

			return orders;
		}

		/// <inheritdoc />
		protected async override Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			string baseUrl = $"/markets/{marketSymbol}/trades?";

			if (startDate != null)
			{
				baseUrl += $"&start_time={startDate?.UnixTimestampFromDateTimeMilliseconds()}";
			}

			if (endDate != null)
			{
				baseUrl += $"&end_time={endDate?.UnixTimestampFromDateTimeMilliseconds()}";
			}

			List<ExchangeTrade> trades = new List<ExchangeTrade>();

			while (true)
			{
				JToken result = await MakeJsonRequestAsync<JToken>(baseUrl);

				foreach (JToken trade in result.Children())
				{
					trades.Add(
							trade.ParseTrade(
									"size",
									"price",
									"side",
									"time",
									TimestampType.Iso8601UTC,
									"id",
									"buy"
							)
					);
				}

				if (!callback(trades))
				{
					break;
				}

				Task.Delay(1000).Wait();
			}
		}

		/// <inheritdoc />
		protected async override Task<IEnumerable<string>> OnGetMarketSymbolsAsync(
				bool isWebSocket = false
		)
		{
			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			//FTX contains futures which we are not interested in so we filter them out.
			var names = result
					.Children()
					.Select(x => x["name"].ToStringInvariant())
					.Where(x => Regex.Match(x, @"[\w\d]*\/[[\w\d]]*").Success)
					.ToList();

			names.Sort();

			return names;
		}

		/// <inheritdoc />
		protected async internal override Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			//{
			//	"name": "BTC-0628",
			//	"baseCurrency": null,
			//	"quoteCurrency": null,
			//	"quoteVolume24h": 28914.76,
			//	"change1h": 0.012,
			//	"change24h": 0.0299,
			//	"changeBod": 0.0156,
			//	"highLeverageFeeExempt": false,
			//	"minProvideSize": 0.001,
			//	"type": "future",
			//	"underlying": "BTC",
			//	"enabled": true,
			//	"ask": 3949.25,
			//	"bid": 3949,
			//	"last": 10579.52,
			//	"postOnly": false,
			//	"price": 10579.52,
			//	"priceIncrement": 0.25,
			//	"sizeIncrement": 0.0001,
			//	"restricted": false,
			//	"volumeUsd24h": 28914.76
			//}

			var markets = new List<ExchangeMarket>();

			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			foreach (JToken token in result.Children())
			{
				var symbol = token["name"].ToStringInvariant();

				if (!Regex.Match(symbol, @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}

				var market = new ExchangeMarket()
				{
					MarketSymbol = symbol,
					BaseCurrency = token["baseCurrency"].ToStringInvariant(),
					QuoteCurrency = token["quoteCurrency"].ToStringInvariant(),
					PriceStepSize = token["priceIncrement"].ConvertInvariant<decimal>(),
					QuantityStepSize = token["sizeIncrement"].ConvertInvariant<decimal>(),
					MinTradeSize = token["minProvideSize"].ConvertInvariant<decimal>(),
					IsActive = token["enabled"].ConvertInvariant<bool>(),
				};

				markets.Add(market);
			}

			return markets;
		}

		/// <inheritdoc />
		protected async override Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			// https://docs.ftx.com/#get-open-orders


			var markets = new List<ExchangeOrderResult>();

			JToken result = await MakeJsonRequestAsync<JToken>(
					$"/orders?market={marketSymbol}",
					null,
					await GetNoncePayloadAsync()
			);

			foreach (JToken token in result.Children())
			{
				var symbol = token["market"].ToStringInvariant();

				if (!Regex.Match(symbol, @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}

				markets.Add(ParseOrder(token));
			}

			return markets;
		}

		/// <inheritdoc />
		protected async override Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			JToken response = await MakeJsonRequestAsync<JToken>(
					$"/markets/{marketSymbol}/orderbook?depth={maxCount}"
			);

			return response.ParseOrderBookFromJTokenArrays();
		}

		/// <inheritdoc />
		protected async override Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{ // https://docs.ftx.com/#get-order-status and https://docs.ftx.com/#get-order-status-by-client-id
			if (!string.IsNullOrEmpty(marketSymbol))
				throw new NotSupportedException(
						"Searching by marketSymbol is either not implemented by or supported by this exchange. Please submit a PR if you are interested in this feature"
				);

			var url = "/orders/";
			if (isClientOrderId)
			{
				url += "by_client_id/";
			}

			JToken result = await MakeJsonRequestAsync<JToken>(
					$"{url}{orderId}",
					null,
					await GetNoncePayloadAsync()
			);

			return ParseOrder(result);
		}

		/// <inheritdoc />
		protected async override Task<
				IEnumerable<KeyValuePair<string, ExchangeTicker>>
		> OnGetTickersAsync()
		{
			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			var tickers = new Dictionary<string, ExchangeTicker>();

			foreach (JToken token in result.Children())
			{
				var symbol = token["name"].ToStringInvariant();

				if (!Regex.Match(symbol, @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}

				var ticker = await this.ParseTickerAsync(
						token,
						symbol,
						"ask",
						"bid",
						"last",
						null,
						null,
						"time",
						TimestampType.UnixSecondsDouble
				);

				tickers.Add(symbol, ticker);
			}

			return tickers;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var result = await MakeJsonRequestAsync<JToken>($"/markets/{marketSymbol}");

			return await this.ParseTickerAsync(
					result,
					marketSymbol,
					"ask",
					"bid",
					"last",
					null,
					null,
					"time",
					TimestampType.UnixSecondsDouble
			);
		}

		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(
				ExchangeWithdrawalRequest request
		)
		{
			var parameters = new Dictionary<string, object>
						{
								{ "coin", request.Currency },
								{ "size", request.Amount },
								{ "address", request.Address },
								{ "nonce", await GenerateNonceAsync() },
								{ "password", request.Password },
								{ "code", request.Code }
						};

			var result = await MakeJsonRequestAsync<JToken>(
					"/wallet/withdrawals",
					null,
					parameters,
					"POST"
			);

			return new ExchangeWithdrawalResponse
			{
				Id = result["id"].ToString(),
				Fee = result.Value<decimal?>("fee")
			};
		}

		/// <inheritdoc />
		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(
				Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers,
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

						if (
											parsedMsg["channel"].ToStringInvariant().Equals("ticker")
											&& !parsedMsg["type"].ToStringInvariant().Equals("subscribed")
									)
						{
							JToken data = parsedMsg["data"];

							var exchangeTicker = await this.ParseTickerAsync(
												data,
												parsedMsg["market"].ToStringInvariant(),
												"ask",
												"bid",
												"last",
												null,
												null,
												"time",
												TimestampType.UnixSecondsDouble
										);

							var kv = new KeyValuePair<string, ExchangeTicker>(
												exchangeTicker.MarketSymbol,
												exchangeTicker
										);

							tickers(new List<KeyValuePair<string, ExchangeTicker>> { kv });
						}
					},
					connectCallback: async (_socket) =>
					{
						//{'op': 'subscribe', 'channel': 'trades', 'market': 'BTC-PERP'}

						for (int i = 0; i < marketSymbols.Length; i++)
						{
							await _socket.SendMessageAsync(
												new
												{
													op = "subscribe",
													market = marketSymbols[i],
													channel = "ticker"
												}
										);
						}
					}
			);
		}

		/// <inheritdoc />
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

						if (parsedMsg["type"].ToStringInvariant() == "error")
						{
							throw new APIException(parsedMsg["msg"].ToStringInvariant());
						}
						else if (
											parsedMsg["channel"].ToStringInvariant().Equals("trades")
											&& !parsedMsg["type"].ToStringInvariant().Equals("subscribed")
									)
						{
							foreach (var data in parsedMsg["data"])
							{
								var exchangeTrade = data.ParseTradeFTX(
													"size",
													"price",
													"side",
													"time",
													TimestampType.Iso8601Local,
													"id"
											);

								await callback(
													new KeyValuePair<string, ExchangeTrade>(
															parsedMsg["market"].ToStringInvariant(),
															exchangeTrade
													)
											);
							}
						}
					},
					connectCallback: async (_socket) =>
					{
						//{'op': 'subscribe', 'channel': 'trades', 'market': 'BTC-PERP'}

						for (int i = 0; i < marketSymbols.Length; i++)
						{
							await _socket.SendMessageAsync(
												new
												{
													op = "subscribe",
													market = marketSymbols[i],
													channel = "trades",
												}
										);
						}
					}
			);
		}

		/// <inheritdoc />
		protected async override Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			//{
			//	"market": "XRP-PERP",
			//  "side": "sell",
			//  "price": 0.306525,
			//  "type": "limit",
			//  "size": 31431.0,
			//  "reduceOnly": false,
			//  "ioc": false,
			//  "postOnly": false,
			//  "clientId": null
			//}

			IEnumerable<ExchangeMarket> markets = await OnGetMarketSymbolsMetadataAsync();
			ExchangeMarket market = markets
					.Where(m => m.MarketSymbol == order.MarketSymbol)
					.First();

			var payload = await GetNoncePayloadAsync();

			var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
						{
								{ "market", market.MarketSymbol },
								{ "side", order.IsBuy ? "buy" : "sell" },
								{ "type", order.OrderType.ToStringLowerInvariant() },
								{ "size", order.RoundAmount() }
						};

			if (!string.IsNullOrEmpty(order.ClientOrderId))
			{
				parameters.Add("clientId", order.ClientOrderId);
			}

			if (order.IsPostOnly != null)
			{
				parameters.Add("postOnly", order.IsPostOnly);
			}

			if (order.OrderType != OrderType.Market)
			{
				int precision = BitConverter.GetBytes(
						decimal.GetBits((decimal)market.PriceStepSize)[3]
				)[2];

				if (order.Price == null)
					throw new ArgumentNullException(nameof(order.Price));

				parameters.Add("price", Math.Round(order.Price.Value, precision));
			}
			else
			{
				parameters.Add("price", null);
			}

			parameters.CopyTo(payload);

			order.ExtraParameters.CopyTo(payload);

			var response = await MakeJsonRequestAsync<JToken>("/orders", null, payload, "POST");

			ExchangeOrderResult result = new ExchangeOrderResult
			{
				OrderId = response["id"].ToStringInvariant(),
				ClientOrderId = response["clientId"].ToStringInvariant(),
				OrderDate = CryptoUtility.ToDateTimeInvariant(response["createdAt"]),
				Price = CryptoUtility.ConvertInvariant<decimal>(response["price"]),
				AmountFilled = CryptoUtility.ConvertInvariant<decimal>(response["filledSize"]),
				AveragePrice = CryptoUtility.ConvertInvariant<decimal>(response["avgFillPrice"]),
				Amount = CryptoUtility.ConvertInvariant<decimal>(response["size"]),
				MarketSymbol = response["market"].ToStringInvariant(),
				IsBuy = response["side"].ToStringInvariant() == "buy"
			};

			return result;
		}

		/// <inheritdoc />
		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				string timestamp = payload["nonce"].ToStringInvariant();

				payload.Remove("nonce");

				string form = CryptoUtility.GetJsonForPayload(payload);

				//Create the signature payload
				string toHash =
						$"{timestamp}{request.Method.ToUpperInvariant()}{request.RequestUri.PathAndQuery}";

				if (request.Method == "POST")
				{
					toHash += form;

					await CryptoUtility.WriteToRequestAsync(request, form);
				}

				byte[] secret = CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey);

				string signatureHexString = CryptoUtility.SHA256Sign(toHash, secret);

				request.AddHeader("FTX-KEY", PublicApiKey.ToUnsecureString());
				request.AddHeader("FTX-SIGN", signatureHexString);
				request.AddHeader("FTX-TS", timestamp);
			}
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Parses the json of an order.
		/// </summary>
		/// <param name="token">Json token to parse the order from.</param>
		/// <returns>Parsed exchange order result.</returns>
		private ExchangeOrderResult ParseOrder(JToken token)
		{
			return new ExchangeOrderResult()
			{
				MarketSymbol = token["market"].ToStringInvariant(),
				Price = token["price"].ConvertInvariant<decimal>(),
				AveragePrice = token["avgFillPrice"].ConvertInvariant<decimal>(),
				OrderDate = token["createdAt"].ConvertInvariant<DateTime>(),
				IsBuy = token["side"].ToStringInvariant().Equals("buy"),
				OrderId = token["id"].ToStringInvariant(),
				Amount = token["size"].ConvertInvariant<decimal>(),
				AmountFilled = token["filledSize"].ConvertInvariant<decimal>(),
				ClientOrderId = token["clientId"].ToStringInvariant(),
				Result = token["status"]
							.ToStringInvariant()
							.ToExchangeAPIOrderResult(
									token["size"].ConvertInvariant<decimal>()
											- token["filledSize"].ConvertInvariant<decimal>()
							),
				ResultCode = token["status"].ToStringInvariant()
			};
		}

		#endregion
	}
}
