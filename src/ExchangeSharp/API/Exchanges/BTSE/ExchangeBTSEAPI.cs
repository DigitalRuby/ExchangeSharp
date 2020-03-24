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
		public override string BaseUrl { get; set; } = "https://api.btse.com";

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await GetTickersAsync()).Select(pair => pair.Key);
		}

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			JToken allPairs = await MakeJsonRequestAsync<JArray>("/spot/api/v3/market_summary");
			var tasks = allPairs.Select(async token => await this.ParseTickerAsync(token,
				token["symbol"].Value<string>(), "lowestAsk", "highestBid", "last", "volume", null,
				null, TimestampType.UnixMilliseconds, "base", "quote", "symbol"));

			return (await Task.WhenAll(tasks)).Select(ticker =>
				new KeyValuePair<string, ExchangeTicker>(ticker.MarketSymbol, ticker));
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			JToken ticker = await MakeJsonRequestAsync<JObject>("/spot/api/v3/market_summary", null,
				new Dictionary<string, object>()
				{
					{"symbol", marketSymbol}
				});
			return await this.ParseTickerAsync(ticker, marketSymbol, "lowestAsk", "highestBid", "last", "volume", null,
				null, TimestampType.UnixMilliseconds, "base", "quote", "symbol");
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

			JToken ticker = await MakeJsonRequestAsync<JArray>("/spot/api/v3/ohlcv", null, payload, "GET");
			return ticker.Select(token =>
				this.ParseCandle(token, marketSymbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixMilliseconds, 5));
		}

		protected override async Task OnCancelOrderAsync(string orderId, string? marketSymbol = null)
		{
			var payload = await GetNoncePayloadAsync();

			payload["order_id"] = orderId.ConvertInvariant<long>();
			var url = new UriBuilder(BaseUrl) {Path = "/spot/api/v3/order"};
			url.AppendPayloadToQuery(new Dictionary<string, object>()
			{
				{"symbol", marketSymbol},
				{"orderID", orderId}
			});

			await MakeJsonRequestAsync<JToken>(url.ToStringInvariant().Replace(BaseUrl, ""),
				requestMethod: "DELETE", payload: payload);
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var payload = await GetNoncePayloadAsync();

			var result = await MakeJsonRequestAsync<JToken>("/spot/api/v3/user/wallet",
				requestMethod: "GET", payload: payload);
			return Extract(result, token => (token["currency"].Value<string>(), token["total"].Value<decimal>()));
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
		{
			var payload = await GetNoncePayloadAsync();

			var result = await MakeJsonRequestAsync<JToken>("/spot/api/v3/user/wallet",
				requestMethod: "GET", payload: payload);
			return Extract(result, token => (token["currency"].Value<string>(), token["available"].Value<decimal>()));
		}

		protected override async Task<Dictionary<string, decimal>> OnGetFeesAsync()
		{
			var payload = await GetNoncePayloadAsync();

			var result = await MakeJsonRequestAsync<JToken>("/spot/api/v3/user/fees",
				requestMethod: "GET", payload: payload);

			//taker or maker fees in BTSE.. i chose take for here
			return Extract(result, token => (token["symbol"].Value<string>(), token["taker"].Value<decimal>()));
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
			string? marketSymbol = null)
		{
			if (marketSymbol == null) throw new ArgumentNullException(nameof(marketSymbol));
			var payload = await GetNoncePayloadAsync();
			var url = new UriBuilder(BaseUrl) {Path = "/spot/api/v3/open_orders"};
			url.AppendPayloadToQuery(new Dictionary<string, object>()
			{
				{"symbol", marketSymbol}
			});
			var result = await MakeJsonRequestAsync<JToken>(url.ToStringInvariant().Replace(BaseUrl, ""),
				requestMethod: "GET", payload: payload);

			//taker or maker fees in BTSE.. i chose take for here
			return Extract2(result, token => new ExchangeOrderResult()
			{
				Amount = token["size"].Value<decimal>(),
				AmountFilled = token["filledSize"].Value<decimal>(),
				OrderId = token["orderID"].Value<string>(),
				IsBuy = token["side"].Value<string>() == "BUY",
				Price = token["price"].Value<decimal>(),
				MarketSymbol = token["symbol"].Value<string>(),
				OrderDate = token["timestamp"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds()
			});
		}

		public override async Task<ExchangeOrderResult[]> PlaceOrdersAsync(params ExchangeOrderRequest[] orders)
		{
			var payload = await GetNoncePayloadAsync();
			payload.Add("body", orders.Select(request => new
			{
				size = request.Amount,
				side = request.IsBuy ? "BUY" : "SELL",
				price = request.Price,
				stopPrice = request.StopPrice,
				symbol = request.MarketSymbol,
				txType = request.OrderType == OrderType.Limit ? "LIMIT" :
					request.OrderType == OrderType.Stop ? "STOP" : null,
				type = request.OrderType == OrderType.Limit ? "LIMIT" :
					request.OrderType == OrderType.Market ? "MARKET" : null
			}));
			var result = await MakeJsonRequestAsync<JToken>("/spot/api/v3/order",
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
					IsBuy = token["orderType"].Value<string>().ToLowerInvariant() == "buy",
					Price = token["price"].Value<decimal>(),
					MarketSymbol = token["symbol"].Value<string>(),
					Result = status,
					Amount = token["size"].Value<decimal>(),
					OrderDate = token["timestamp"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds(),
				};
			}).ToArray();
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

				var hexSha384 = CryptoUtility.SHA384Sign(
					$"{request.RequestUri.PathAndQuery.Replace("/spot", string.Empty)}{nonce}{json}",
					PrivateApiKey.ToUnsecureString());
				request.AddHeader("btse-sign", hexSha384);
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
	}

	public partial class ExchangeName
	{
		public const string BTSE = "BTSE";
	}
}
