using Newtonsoft.Json;

namespace ExchangeSharp
{
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public sealed partial class ExchangeBTSEAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.btse.com/spot";
		public const string TestnetUrl = "https://testapi.btse.io/spot";

		public ExchangeBTSEAPI()
		{
			NonceStyle = NonceStyle.UnixMillisecondsString;
		}
		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await GetTickersAsync()).Select(pair => pair.Key);
		}

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			JToken allPairs = await MakeJsonRequestAsync<JToken>("/api/v3.1/market_summary", BaseUrl);
			var tasks = allPairs.Select(async token => await ParseBTSETicker(token,
				token["symbol"].Value<string>()));

			return (await Task.WhenAll(tasks)).Select(ticker =>
				new KeyValuePair<string, ExchangeTicker>(ticker.MarketSymbol, ticker));
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			JToken ticker = await MakeJsonRequestAsync<JToken>("/api/v3.1/market_summary", BaseUrl,
				new Dictionary<string, object>()
				{
					{"symbol", marketSymbol}
				});
			return await ParseBTSETicker(ticker, marketSymbol);
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol,
			int periodSeconds, DateTime? startDate = null, DateTime? endDate = null,
			int? limit = null)
		{
			var payload = new Dictionary<string, object>()
			{
				{"symbol", marketSymbol},
				{"resolution", periodSeconds}
			};

			if (startDate != null)
			{
				payload.Add("start", startDate.Value.UnixTimestampFromDateTimeMilliseconds());
			}

			if (endDate != null)
			{
				payload.Add("end", startDate.Value.UnixTimestampFromDateTimeMilliseconds());
			}

			JToken ticker = await MakeJsonRequestAsync<JArray>("/api/v3.1/ohlcv", null, payload, "GET");
			return ticker.Select(token =>
				this.ParseCandle(token, marketSymbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixMilliseconds, 5));
		}

		protected override async Task OnCancelOrderAsync(string orderId, string? marketSymbol = null)
		{
			var payload = await GetNoncePayloadAsync();

			payload["order_id"] = orderId.ConvertInvariant<long>();
			var url = new UriBuilder(BaseUrl) {Path = "/api/v3.1/order"};
			url.AppendPayloadToQuery(new Dictionary<string, object>()
			{
				{"symbol", marketSymbol},
				{"orderID", orderId}
			});

			await MakeJsonRequestAsync<JToken>($"/api/v3.1/order{url.Query}",
				requestMethod: "DELETE", payload: payload);
		}

		protected override Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			return GetBTSEBalance(false);
		}

		protected override Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
		{
			return GetBTSEBalance(true);
		}

		protected override async Task<Dictionary<string, decimal>> OnGetFeesAsync()
		{
			var payload = await GetNoncePayloadAsync();

			var result = await MakeJsonRequestAsync<JToken>("/api/v3.1/user/fees",
				requestMethod: "GET", payload: payload);

			//taker or maker fees in BTSE.. i chose maker for here
			return Extract(result, token => (token["symbol"].Value<string>(), token["makerFee"].Value<decimal>()));
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
			string? marketSymbol = null)
		{
			if (marketSymbol == null) throw new ArgumentNullException(nameof(marketSymbol));
			var payload = await GetNoncePayloadAsync();

			var url = new UriBuilder(BaseUrl) {Path = "/api/v3.1/user/open_orders"};
			url.AppendPayloadToQuery(new Dictionary<string, object>()
			{
				{"symbol", marketSymbol}
			});

			var result = await MakeJsonRequestAsync<JToken>("/api/v3.1/user/open_orders"+url.Query,
				requestMethod: "GET", payload: payload);

			return Extract2(result, token => new ExchangeOrderResult()
			{
				Amount = token["size"].Value<decimal>(),
				AmountFilled = token["filledSize"].Value<decimal>(),
				OrderId = token["orderID"].Value<string>(),
				IsBuy = token["side"].Value<string>() == "BUY",
				Price = token["price"].Value<decimal>(),
				MarketSymbol = token["symbol"].Value<string>(),
				OrderDate = token["timestamp"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds(),
				ClientOrderId = token["clOrderID"].Value<string>(),
				Result = FromOrderState(token["orderState"].Value<string>())
			});
		}

		private ExchangeAPIOrderResult FromOrderState(string s)
		{
			switch (s)
			{
				case "STATUS_ACTIVE":
					return ExchangeAPIOrderResult.Pending;
				case "ORDER_CANCELLED":
					return ExchangeAPIOrderResult.Canceled;
				case "ORDER_FULLY_TRANSACTED":
					return ExchangeAPIOrderResult.Filled;
				case "ORDER_PARTIALLY_TRANSACTED":
					return ExchangeAPIOrderResult.FilledPartially;
				default:
					return ExchangeAPIOrderResult.Unknown;
			}
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest request)
		{var payload = await GetNoncePayloadAsync();

			var dict = new Dictionary<string, object>();

			var id = request.OrderId ?? request.ClientOrderId;
			if (!string.IsNullOrEmpty(id))
			{
				dict.Add("clOrderID",id);

			}

			dict.Add("size", request.Amount);
			dict.Add("side", request.IsBuy ? "BUY" : "SELL");
			dict.Add("symbol", request.MarketSymbol);

			switch (request.OrderType )
			{
				case OrderType.Limit:
					dict.Add("txType", "LIMIT");
					dict.Add("type", "LIMIT");
					dict.Add("price", request.Price);
					break;
				case OrderType.Market:
					dict.Add("type", "MARKET");
					break;
				case OrderType.Stop:
					dict.Add("stopPrice", request.StopPrice);
					dict.Add("price", request.Price);
					dict.Add("txType", "STOP");
					break;
			}

			foreach (var extraParameter in request.ExtraParameters)
			{
				if (!dict.ContainsKey(extraParameter.Key))
				{
					dict.Add(extraParameter.Key, extraParameter.Value);
				}
			}


			payload.Add("body", dict);

			var result = await MakeJsonRequestAsync<JToken>("/api/v3.1/order",
				requestMethod: "POST", payload: payload);
			return Extract2(result, token =>
			{
				var status = ExchangeAPIOrderResult.Unknown;
				switch (token["status"].Value<int>())
				{
					case 2:
						status = ExchangeAPIOrderResult.Pending;
						break;
					case 4:
						status = ExchangeAPIOrderResult.Filled;
						break;
					case 5:
						status = ExchangeAPIOrderResult.FilledPartially;
						break;
					case 6:
						status = ExchangeAPIOrderResult.Canceled;
						break;
					case 9: //trigger inserted
					case 10: //trigger activated
						status = ExchangeAPIOrderResult.Pending;
						break;
					case 15: //rejected
						status = ExchangeAPIOrderResult.Error;
						break;
					case 16: //not found
						status = ExchangeAPIOrderResult.Unknown;
						break;
				}

				return new ExchangeOrderResult()
				{
					Message = token["message"].Value<string>(),
					OrderId = token["orderID"].Value<string>(),
					IsBuy = token["side"].Value<string>().ToLowerInvariant() == "buy",
					Price = token["price"].Value<decimal>(),
					MarketSymbol = token["symbol"].Value<string>(),
					Result = status,
					Amount = token["size"].Value<decimal>(),
					OrderDate = token["timestamp"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds(),
					ClientOrderId = token["clOrderID"].Value<string>(),
					AveragePrice = token["averageFillPrice"].Value<decimal>(),
					AmountFilled = token["fillSize"].Value<decimal>(),

				};
			}).First();
		}

		protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
		{
			if (method == "GET" && (payload?.Count ?? 0) != 0 && !payload.ContainsKey("nonce"))
			{
				url.AppendPayloadToQuery(payload);
			}
			return base.ProcessRequestUrl(url, payload, method);
		}

		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				if (payload.TryGetValue("body", out var body))
				{
					payload.Remove("body");
				}

				var nonce = payload["nonce"].ToString();
				payload.Remove("nonce");

				var json = JsonConvert.SerializeObject(body ?? payload);
				if (json == "{}")
				{
					json = "";
				}

				var passphrase = Passphrase?.ToUnsecureString();
				if (string.IsNullOrEmpty(passphrase))
				{
					passphrase = PrivateApiKey?.ToUnsecureString();
				}

				var hexSha384 = CryptoUtility.SHA384Sign(
					$"{request.RequestUri.AbsolutePath.Replace("/spot", string.Empty)}{nonce}{json}",
					passphrase);
				request.AddHeader("btse-sign", hexSha384);
				request.AddHeader("btse-nonce", nonce);
				request.AddHeader("btse-api", PublicApiKey.ToUnsecureString());
				await request.WriteToRequestAsync(json);
			}

			await base.ProcessRequestAsync(request, payload);
		}

		protected override async Task<Dictionary<string, object>> GetNoncePayloadAsync()
		{
			var result = await base.GetNoncePayloadAsync();
			if (result.ContainsKey("recvWindow"))
			{
				result.Remove("recvWindow");
			}

			return result;
		}

		private Dictionary<TKey, TValue> Extract<TKey, TValue>(JToken token, Func<JToken, (TKey, TValue)> processor)
		{
			if (token is JArray resultArr)
			{
				return resultArr.Select(processor.Invoke)
					.ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
			}

			var resItem = processor.Invoke(token);
			return new Dictionary<TKey, TValue>()
			{
				{resItem.Item1, resItem.Item2}
			};
		}

		private IEnumerable<TValue> Extract2<TValue>(JToken token, Func<JToken, TValue> processor)
		{
			if (token is JArray resultArr)
			{
				return resultArr.Select(processor.Invoke);
			}

			return new List<TValue>()
			{
				processor.Invoke(token)
			};
		}

		private async Task<ExchangeTicker> ParseBTSETicker(JToken ticker, string marketSymbol)
		{
			return await this.ParseTickerAsync(ticker, marketSymbol, "lowestAsk", "highestBid", "last", "volume", null,
				null, TimestampType.UnixMilliseconds, "base", "quote", "symbol");
		}

		private async Task<Dictionary<string, decimal>> GetBTSEBalance(bool availableOnly)
		{
			var payload = await GetNoncePayloadAsync();

			var result = await MakeJsonRequestAsync<JToken>("/api/v3.1/user/wallet",
				requestMethod: "GET", payload: payload);
			return Extract(result, token => (token["currency"].Value<string>(), token[availableOnly?"available": "total"].Value<decimal>()));
		}

	}

	public partial class ExchangeName
	{
		public const string BTSE = "BTSE";
	}
}
