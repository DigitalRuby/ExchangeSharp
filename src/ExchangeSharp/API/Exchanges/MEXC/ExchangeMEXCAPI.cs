using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ExchangeSharp.API.Exchanges.MEXC.Models;

namespace ExchangeSharp
{
	public sealed class ExchangeMEXCAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.mexc.com/api/v3";
		public override string BaseUrlWebSocket { get; set; } = "wss://wbs.mexc.com/ws";

		public override string PeriodSecondsToString(int seconds) =>
			CryptoUtility.SecondsToPeriodInMinutesUpToHourString(seconds);

		private ExchangeMEXCAPI()
		{
			NonceStyle = NonceStyle.UnixMilliseconds;
			MarketSymbolSeparator = string.Empty;
			MarketSymbolIsUppercase = true;
			RateLimit = new RateGate(20, TimeSpan.FromSeconds(2));
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
		}

		public override Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
		{
			var quoteLength = 3;
			if (marketSymbol.EndsWith("USDT") ||
			    marketSymbol.EndsWith("USDC") ||
			    marketSymbol.EndsWith("TUSD"))
			{
				quoteLength = 4;
			}

			var baseSymbol = marketSymbol.Substring(marketSymbol.Length - quoteLength);

			return ExchangeMarketSymbolToGlobalMarketSymbolWithSeparatorAsync(
				marketSymbol.Replace(baseSymbol, "")
				+ GlobalMarketSymbolSeparator
				+ baseSymbol);
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await OnGetMarketSymbolsMetadataAsync())
				.Select(x => x.MarketSymbol);
		}

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			var symbols = await MakeJsonRequestAsync<JToken>("/exchangeInfo", BaseUrl);

			return (symbols["symbols"] ?? throw new ArgumentNullException())
				.Select(symbol => new ExchangeMarketMexc()
				{
					MarketSymbol = symbol["symbol"].ToStringInvariant(),
					IsActive = symbol["isSpotTradingAllowed"].ConvertInvariant<bool>(),
					MarginEnabled = symbol["isMarginTradingAllowed"].ConvertInvariant<bool>(),
					BaseCurrency = symbol["baseAsset"].ToStringInvariant(),
					QuoteCurrency = symbol["quoteAsset"].ToStringInvariant(),
					BaseAssetPrecision = symbol["baseAssetPrecision"].ConvertInvariant<int>(),
					QuoteAssetPrecision = symbol["quoteAssetPrecision"].ConvertInvariant<int>(),
					QuantityStepSize = symbol["baseSizePrecision"].ConvertInvariant<decimal>(),
					// Not 100% sure about this
					PriceStepSize =
						CryptoUtility.PrecisionToStepSize(symbol["quoteCommissionPrecision"].ConvertInvariant<decimal>()),
					MinTradeSizeInQuoteCurrency = symbol["quoteAmountPrecision"].ConvertInvariant<decimal>(),
					MaxTradeSizeInQuoteCurrency = symbol["maxQuoteAmount"].ConvertInvariant<decimal>()
				});
		}

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
			var token = await MakeJsonRequestAsync<JToken>("/ticker/24hr", BaseUrl);
			foreach (var t in token)
			{
				var symbol = (t["symbol"] ?? throw new ArgumentNullException()).ToStringInvariant();
				try
				{
					tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol,
						await this.ParseTickerAsync(
							t,
							symbol,
							"askPrice",
							"bidPrice",
							"lastPrice",
							"volume",
							timestampType: TimestampType.UnixMilliseconds,
							timestampKey: "closeTime")));
				}
				catch (OverflowException)
				{
				}
				catch (InvalidDataException)
				{
				}
			}

			return tickers;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol) =>
			await this.ParseTickerAsync(
				await MakeJsonRequestAsync<JToken>($"/ticker/24hr?symbol={marketSymbol.ToUpperInvariant()}", BaseUrl),
				marketSymbol,
				"askPrice",
				"bidPrice",
				"lastPrice",
				"volume",
				timestampType: TimestampType.UnixMilliseconds,
				timestampKey: "closeTime");

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
		{
			const int maxDepth = 5000;
			const string sequenceKey = "lastUpdateId";
			marketSymbol = marketSymbol.ToUpperInvariant();
			if (string.IsNullOrEmpty(marketSymbol))
			{
				throw new ArgumentOutOfRangeException(nameof(marketSymbol), "Market symbol cannot be empty.");
			}

			if (maxCount > maxDepth)
			{
				throw new ArgumentOutOfRangeException(nameof(maxCount), $"Max order book depth is {maxDepth}");
			}

			var token = await MakeJsonRequestAsync<JToken>($"/depth?symbol={marketSymbol}");
			var orderBook = token.ParseOrderBookFromJTokenArrays(sequence: sequenceKey);
			orderBook.MarketSymbol = marketSymbol;
			orderBook.ExchangeName = Name;
			orderBook.LastUpdatedUtc = DateTime.UtcNow;

			return orderBook;
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol,
			int? limit = null)
		{
			const int maxLimit = 1000;
			const int defaultLimit = 500;
			marketSymbol = marketSymbol.ToUpperInvariant();
			if (limit == null || limit <= 0)
			{
				limit = defaultLimit;
			}

			if (limit > maxLimit)
			{
				throw new ArgumentOutOfRangeException(nameof(limit), $"Max recent trades limit is {maxLimit}");
			}

			var token = await MakeJsonRequestAsync<JToken>($"/trades?symbol={marketSymbol}&limit={limit.Value}");
			return token
				.Select(t => t.ParseTrade(
					"qty",
					"price",
					"isBuyerMaker",
					"time",
					TimestampType.UnixMilliseconds,
					"id",
					"true"));
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
			string marketSymbol,
			int periodSeconds,
			DateTime? startDate = null,
			DateTime? endDate = null,
			int? limit = null)
		{
			var period = PeriodSecondsToString(periodSeconds);
			const int maxLimit = 1000;
			const int defaultLimit = 500;
			if (limit == null || limit <= 0)
			{
				limit = defaultLimit;
			}

			if (limit > maxLimit)
			{
				throw new ArgumentOutOfRangeException(nameof(limit), $"Max recent candlesticks limit is {maxLimit}");
			}


			var url = $"/klines?symbol={marketSymbol}&interval={period}&limit={limit.Value}";
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
						1,
						2,
						3,
						4,
						0,
						TimestampType.UnixMilliseconds,
						5,
						7
					));
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var token = await GetBalance();
			return (token["balances"] ?? throw new InvalidOperationException())
				.Select(
					x =>
						new
						{
							Currency = x["asset"].Value<string>(),
							TotalBalance = x["free"].Value<decimal>()
							               + x["locked"].Value<decimal>()
						}
				)
				.ToDictionary(k => k.Currency, v => v.TotalBalance);
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
		{
			var token = await GetBalance();
			return (token["balances"] ?? throw new InvalidOperationException())
				.Select(
					x =>
						new
						{
							Currency = x["asset"].Value<string>(),
							AvailableBalance = x["free"].Value<decimal>()
						}
				)
				.ToDictionary(k => k.Currency, v => v.AvailableBalance);
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
			string marketSymbol = null)
		{
			if (string.IsNullOrEmpty(marketSymbol))
			{
				throw new ArgumentNullException(nameof(marketSymbol), $"Market symbol cannot be null");
			}

			var token = await MakeJsonRequestAsync<JToken>($"/openOrders?symbol={marketSymbol.ToUpperInvariant()}",
				payload: await GetNoncePayloadAsync());

			return token.Select(ParseOrder);
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
		{
			if (string.IsNullOrEmpty(order.MarketSymbol))
			{
				throw new ArgumentNullException(nameof(order.MarketSymbol));
			}
			if (order.Price == null && order.OrderType != OrderType.Market)
			{
				throw new ArgumentNullException(nameof(order.Price));
			}

			var payload = await GetNoncePayloadAsync();
			payload["symbol"] = order.MarketSymbol;
			payload["quantity"] = order.Amount;
			payload["side"] = order.IsBuy ? "BUY" : "SELL";

			if (order.OrderType != OrderType.Market)
			{
				payload["price"] = order.Price.Value;
				if (order.IsPostOnly != true)
					payload["type"] = "LIMIT";
			}

			switch (order.OrderType)
			{
				case OrderType.Limit when !order.IsPostOnly.GetValueOrDefault():
					payload["type"] = "LIMIT";
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

			if (!string.IsNullOrEmpty(order.ClientOrderId))
			{
				payload["clientOrderId"] = order.ClientOrderId;
			}

			order.ExtraParameters.CopyTo(payload);

			var token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "POST");

			// MEXC does not return order status with the place order response, so we need to send one more request to get the order details.
			return await GetOrderDetailsAsync(
				(token["orderId"] ?? throw new InvalidOperationException()).ToStringInvariant(),
				order.MarketSymbol
			);
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
			if (string.IsNullOrEmpty(marketSymbol))
			{
				throw new ArgumentNullException(nameof(marketSymbol), $"Market symbol cannot be null");
			}

			if (string.IsNullOrEmpty(orderId))
			{
				throw new ArgumentNullException(
					nameof(orderId),
					"Order details request requires order ID or client-supplied order ID"
				);
			}

			var param = isClientOrderId ? $"origClientOrderId={orderId}" : $"orderId={orderId}";
			var token = await MakeJsonRequestAsync<JToken>($"/order?symbol={marketSymbol.ToUpperInvariant()}&{param}",
				payload: await GetNoncePayloadAsync());

			return ParseOrder(token);
		}

		protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
			if (string.IsNullOrEmpty(orderId))
			{
				throw new ArgumentNullException(
					nameof(orderId),
					"Cancel order request requires order ID"
				);
			}

			if (string.IsNullOrEmpty(marketSymbol))
			{
				throw new ArgumentNullException(
					nameof(marketSymbol),
					"Cancel order request requires symbol"
				);
			}

			var payload = await GetNoncePayloadAsync();
			payload["symbol"] = marketSymbol!;
			switch (isClientOrderId)
			{
				case true:
					payload["origClientOrderId"] = orderId;
					break;
				default:
					payload["orderId"] = orderId;
					break;
			}

			await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "DELETE");
		}

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(
				Action<ExchangeOrderBook> callback,
				int maxCount = 20,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}

			var initialSequenceIds = new Dictionary<string, long>();

			foreach (var marketSymbol in marketSymbols)
			{
				var initialBook = await OnGetOrderBookAsync(marketSymbol, maxCount);
				initialBook.IsFromSnapshot = true;

				callback(initialBook);

				initialSequenceIds[marketSymbol] = initialBook.SequenceId;
			}

			return await ConnectPublicWebSocketAsync(
					string.Empty,
					(_socket, msg) =>
					{
						var json = msg.ToStringFromUTF8();

						if (json.Contains("invalid") || json.Contains("Not Subscribed"))
						{
							Logger.Warn(json);
							return Task.CompletedTask;
						}

						MarketDepthDiffUpdate update = null;
						try
						{
							update = JsonConvert.DeserializeObject<MarketDepthDiffUpdate>(json, SerializerSettings);
						}
						catch
						{
						}

						if (update == null || string.IsNullOrWhiteSpace(update.Channel) || update.Details == null)
						{
							return Task.CompletedTask;
						}

						if (update.Details.Version < initialSequenceIds[update.Symbol])
						{
							// A deprecated update should be ignored
							return Task.CompletedTask;
						}

						var eventTimeDateTime = update.EventTime.UnixTimeStampToDateTimeMilliseconds();

						var orderBook = new ExchangeOrderBook
						{
							IsFromSnapshot = false,
							ExchangeName = Name,
							SequenceId = update.Details.Version,
							MarketSymbol = update.Symbol,
							LastUpdatedUtc = eventTimeDateTime,
						};

						if (update.Details.Asks != null)
						{
							foreach (var ask in update.Details.Asks)
							{
								orderBook.Asks[ask.Price] = new ExchangeOrderPrice
								{
									Price = ask.Price,
									Amount = ask.Volume,
								};
							}
						}

						if (update.Details.Bids != null)
						{
							foreach (var bid in update.Details.Bids)
							{
								orderBook.Bids[bid.Price] = new ExchangeOrderPrice
								{
									Price = bid.Price,
									Amount = bid.Volume,
								};
							}
						}

						callback(orderBook);

						return Task.CompletedTask;
					},
			    async (_socket) =>
					{
						var subscriptionParams = marketSymbols
							.Select(ms => $"spot@public.increase.depth.v3.api@{ms}")
							.ToList();

						await _socket.SendMessageAsync(new WebSocketSubscription
						{
							Method = "SUBSCRIPTION",
							Params = subscriptionParams,
						});
					}
			);
		}

		protected override Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object>? payload
		)
		{
			if (
					CanMakeAuthenticatedRequest(payload)
					|| (payload == null && request.RequestUri.AbsoluteUri.Contains("userDataStream"))
			)
			{
				request.AddHeader("X-MEXC-APIKEY", PublicApiKey!.ToUnsecureString());
			}
			return base.ProcessRequestAsync(request, payload);
		}

		protected override Uri ProcessRequestUrl(
				UriBuilder url,
				Dictionary<string, object>? payload,
				string? method
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				// payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
				var query = (url.Query ?? string.Empty).Trim('?', '&');
				string newQuery =
						"timestamp="
						+ payload!["nonce"].ToStringInvariant()
						+ (query.Length != 0 ? "&" + query : string.Empty)
						+ (
								payload.Count > 1
										? "&" + CryptoUtility.GetFormForPayload(payload, false)
										: string.Empty
						);
				string signature = CryptoUtility.SHA256Sign(
						newQuery,
						CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey)
				);
				newQuery += "&signature=" + signature;
				url.Query = newQuery;
				return url.Uri;
			}
			return base.ProcessRequestUrl(url, payload, method);
		}

		private async Task<JToken> GetBalance()
		{
			var token = await MakeJsonRequestAsync<JToken>("/account", payload: await GetNoncePayloadAsync());
			return token;
		}

		private static ExchangeOrderResult ParseOrder(JToken token)
		{
			// [
			// {
			// 	"symbol": "LTCBTC",
			// 	"orderId": "C02__443776347957968896088",
			// 	"orderListId": -1,
			// 	"clientOrderId": "",
			// 	"price": "0.001395",
			// 	"origQty": "0.004",
			// 	"executedQty": "0",
			// 	"cummulativeQuoteQty": "0",
			// 	"status": "NEW",
			// 	"timeInForce": null,
			// 	"type": "LIMIT",
			// 	"side": "SELL",
			// 	"stopPrice": null,
			// 	"icebergQty": null,
			// 	"time": 1721586762185,
			// 	"updateTime": null,
			// 	"isWorking": true,
			// 	"origQuoteOrderQty": "0.00000558"
			// }
			// ]

			return new ExchangeOrderResult
			{
				OrderId = token["orderId"].ToStringInvariant(),
				ClientOrderId = token["orderListId"].ToStringInvariant(),
				MarketSymbol = token["symbol"].ToStringInvariant(),
				Amount = token["origQty"].ConvertInvariant<decimal>(),
				AmountFilled = token["executedQty"].ConvertInvariant<decimal>(),
				Price = token["price"].ConvertInvariant<decimal>(),
				AveragePrice = token["price"].ConvertInvariant<decimal>(),
				IsBuy = token["side"].ToStringInvariant() == "BUY",
				OrderDate = token["time"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds(),
				Result = ParseOrderStatus(token["status"].ToStringInvariant())
			};
		}

		private static ExchangeAPIOrderResult ParseOrderStatus(string status) =>
			status.ToUpperInvariant() switch
			{
				"NEW" => ExchangeAPIOrderResult.Open,
				"PARTIALLY_FILLED" => ExchangeAPIOrderResult.FilledPartially,
				"FILLED" => ExchangeAPIOrderResult.Filled,
				"PARTIALLY_CANCELED" => ExchangeAPIOrderResult.FilledPartiallyAndCancelled,
				"CANCELED" => ExchangeAPIOrderResult.Canceled,
				_ => ExchangeAPIOrderResult.Unknown
			};
	}

	public partial class ExchangeName
	{
		public const string MEXC = "MEXC";
	}
}
