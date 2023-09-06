using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp.Bybit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public abstract class ExchangeBybitV5Base : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.bybit.com";
		public override string BaseUrlPrivateWebSocket => "wss://stream.bybit.com/v5/private";

		/// <summary>
		/// Can be one of: linear, inverse, option, spot
		/// </summary>
		protected virtual MarketCategory MarketCategory =>
				throw new NotImplementedException("MarketCategory");

		/// <summary>
		/// Account status (is account Unified) needed in some private end-points (e.g. OnGetAmountsAvailableToTradeAsync or GetRecentOrderAsync).
		/// Better be set with constructor. Also it can be set explicitly or GetAccountInfo() can be used to get the account status.
		/// </summary>
		public virtual bool? IsUnifiedAccount { get; set; }

		public ExchangeBybitV5Base()
		{
			MarketSymbolIsUppercase = true;
			NonceStyle = NonceStyle.UnixMilliseconds;
			NonceOffset = TimeSpan.FromSeconds(1.0);
			NonceEndPoint = "/v3/public/time";
			NonceEndPointField = "timeNano";
			RequestContentType = "application/json";
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
			RateLimit = new RateGate(10, TimeSpan.FromSeconds(1));
			RequestWindow = TimeSpan.FromSeconds(15);
		}

		protected override async Task OnGetNonceOffset()
		{
			/*
			 * https://bybit-exchange.github.io/docs/v5/intro#parameters-for-authenticated-endpoints
					Please make sure that the timestamp parameter adheres to the following rule:
					server_time - recv_window <= timestamp < server_time + 1000
			 */
			try
			{
				JToken token = await MakeJsonRequestAsync<JToken>(NonceEndPoint!);
				JToken value = token[NonceEndPointField];

				DateTime serverDate = value
						.ConvertInvariant<long>()
						.UnixTimeStampToDateTimeNanoseconds();
				NonceOffset = (CryptoUtility.UtcNow - serverDate) + TimeSpan.FromSeconds(1);
				Logger.Info(
						$"Nonce offset set for {Name}: {NonceOffset.TotalMilliseconds} milisec"
				);
			}
			catch
			{
				// if this fails we don't want to crash, just run without a nonce
				Logger.Warn($"Failed to get nonce offset for {Name}");
			}
		}

		protected override async Task<Dictionary<string, object>> GetNoncePayloadAsync()
		{
			return new Dictionary<string, object>
			{
				["nonce"] = await GenerateNonceAsync(),
				["category"] = MarketCategory.ToStringLowerInvariant()
			};
		}

		protected override Uri ProcessRequestUrl(
				UriBuilder url,
				Dictionary<string, object> payload,
				string method
		)
		{
			if (payload != null && payload.Count > 1 && method == "GET")
			{
				string query = CryptoUtility.GetFormForPayload(
						payload,
						includeNonce: false,
						orderByKey: true,
						formEncode: false
				);
				query = query.Replace("=True", "=true").Replace("=False", "=false");
				url.Query = query;
			}
			return url.Uri;
		}

		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				string nonce = payload["nonce"].ToStringInvariant();
				payload.Remove("nonce");
				var recvWindow = (int)RequestWindow.TotalMilliseconds;
				string toSign = $"{nonce}{PublicApiKey.ToUnsecureString()}{recvWindow}";
				string json = string.Empty;
				if (request.Method == "POST")
				{
					json = JsonConvert.SerializeObject(payload);
					toSign += json;
				}
				else if (request.Method == "GET" && !string.IsNullOrEmpty(request.RequestUri.Query))
				{
					toSign += request.RequestUri.Query.Substring(1);
				}
				string signature = CryptoUtility.SHA256Sign(
						toSign,
						PrivateApiKey.ToUnsecureString()
				);
				request.AddHeader("X-BAPI-SIGN", signature);
				request.AddHeader("X-BAPI-API-KEY", PublicApiKey.ToUnsecureString());
				request.AddHeader("X-BAPI-TIMESTAMP", nonce);
				request.AddHeader("X-BAPI-RECV-WINDOW", recvWindow.ToStringInvariant());
				if (request.Method == "POST")
				{
					await CryptoUtility.WriteToRequestAsync(request, json);
				}
			}
		}

		protected override JToken CheckJsonResponse(JToken result)
		{
			int retCode = result["retCode"].ConvertInvariant<int>();
			if (retCode != 0)
			{
				string message = result["retMsg"].ToStringInvariant();
				throw new APIException($"{{code: {retCode}, message: '{message}'}}");
			}
			return result["result"];
		}

		#region Public

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			List<ExchangeMarket> markets = new List<ExchangeMarket>();
			var responseToken = await MakeJsonRequestAsync<JToken>(
					$"/v5/market/instruments-info?category={MarketCategory.ToStringLowerInvariant()}"
			);

			foreach (var marketJson in responseToken["list"])
			{
				var priceFilter = marketJson["priceFilter"];
				var sizeFilter = marketJson["lotSizeFilter"];
				bool isInverse = MarketCategory == MarketCategory.Inverse;
				var market = new ExchangeMarket()
				{
					MarketSymbol = marketJson["symbol"].ToStringInvariant(),
					BaseCurrency = marketJson["baseCoin"].ToStringInvariant(),
					QuoteCurrency = marketJson["quoteCoin"].ToStringInvariant(),
					QuantityStepSize = sizeFilter["qtyStep"].ConvertInvariant<decimal>(),
					MinPrice = priceFilter["minPrice"].ConvertInvariant<decimal>(),
					MaxPrice = priceFilter["maxPrice"].ConvertInvariant<decimal>(),
					PriceStepSize = priceFilter["tickSize"].ConvertInvariant<decimal>(),
					IsActive =
								marketJson["status"] == null
								|| marketJson["status"].ToStringLowerInvariant() == "trading"
				};
				if (isInverse)
				{
					market.MinTradeSizeInQuoteCurrency = sizeFilter[
							"minOrderQty"
					].ConvertInvariant<decimal>();
					market.MaxTradeSizeInQuoteCurrency = sizeFilter[
							"maxOrderQty"
					].ConvertInvariant<decimal>();
				}
				else
				{
					market.MinTradeSize = sizeFilter["minOrderQty"].ConvertInvariant<decimal>();
					market.MaxTradeSize = sizeFilter["maxOrderQty"].ConvertInvariant<decimal>();
				}
				markets.Add(market);
			}
			return markets;
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string marketSymbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			string url = "/v5/market/kline";
			int maxLimit = 200;
			limit ??= maxLimit;
			if (limit.Value > maxLimit)
			{
				limit = maxLimit;
			}
			List<MarketCandle> candles = new List<MarketCandle>();
			string periodString = PeriodSecondsToString(periodSeconds);
			if (startDate == null)
			{
				endDate ??= CryptoUtility.UtcNow;
				startDate = endDate - TimeSpan.FromMinutes(limit.Value * periodSeconds / 60);
			}
			url +=
					$"?category={MarketCategory.ToStringLowerInvariant()}&symbol={marketSymbol}&interval={periodString}&limit={limit.Value.ToStringInvariant()}&start={((long)startDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant()}";

			var responseJson = await MakeJsonRequestAsync<JToken>(url);
			var baseVolKey = MarketCategory == MarketCategory.Inverse ? 6 : 5;
			var quoteVolKey = MarketCategory == MarketCategory.Inverse ? 5 : 6;
			foreach (var token in responseJson["list"])
			{
				candles.Add(
						this.ParseCandle(
								token,
								marketSymbol,
								periodSeconds,
								openKey: 1,
								highKey: 2,
								lowKey: 3,
								closeKey: 4,
								timestampKey: 0,
								timestampType: TimestampType.UnixMilliseconds,
								baseVolumeKey: baseVolKey,
								quoteVolumeKey: quoteVolKey
						)
				);
			}
			return candles;
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			var upperLimit =
					MarketCategory == MarketCategory.Linear || MarketCategory == MarketCategory.Inverse
							? 200
							: MarketCategory == MarketCategory.Spot
									? 50
									: 25;
			var limit = Math.Min(maxCount, upperLimit);
			string url =
					$"/v5/market/orderbook?category={MarketCategory.ToStringLowerInvariant()}&symbol={marketSymbol}&limit={limit}";
			JToken token = await MakeJsonRequestAsync<JToken>(url);
			var book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(
					token,
					asks: "a",
					bids: "b",
					sequence: "u"
			);
			book.LastUpdatedUtc = CryptoUtility.ParseTimestamp(
					token["ts"],
					TimestampType.UnixMilliseconds
			);
			book.ExchangeName = Name;
			book.MarketSymbol = token["s"].ToStringInvariant();
			book.IsFromSnapshot = true;
			return book;
		}

		#endregion Public

		#region Private

		/// <summary>
		/// Account status (is account Unified) needed in some private end-points (e.g. OnGetAmountsAvailableToTradeAsync or GetRecentOrderAsync).
		/// Better be set with constructor. If it's not set, this method will be used to get the account status.
		/// </summary>
		public async Task GetAccountUnifiedStatusAsync()
		{
			JObject result = await MakeJsonRequestAsync<JObject>(
					"/v5/user/query-api",
					null,
					await GetNoncePayloadAsync()
			);
			IsUnifiedAccount =
					result["unified"].ConvertInvariant<int>() == 1
					|| result["uta"].ConvertInvariant<int>() == 1;
		}

		public async Task<DateTime> GetAPIKeyExpirationDateAsync()
		{
			JObject result = await MakeJsonRequestAsync<JObject>(
					"/v5/user/query-api",
					null,
					await GetNoncePayloadAsync()
			);
			return CryptoUtility.ParseTimestamp(result["expiredAt"], TimestampType.Iso8601UTC);
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			if (IsUnifiedAccount == null)
			{
				await GetAccountUnifiedStatusAsync();
			}
			var payload = await GetNoncePayloadAsync();
			string accType =
					MarketCategory == MarketCategory.Inverse
							? "CONTRACT"
							: IsUnifiedAccount.Value
									? "UNIFIED"
									: MarketCategory == MarketCategory.Spot
											? "SPOT"
											: "CONTRACT";
			payload["accountType"] = accType;

			JObject result = await MakeJsonRequestAsync<JObject>(
					"/v5/account/wallet-balance",
					null,
					payload
			);
			Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);

			var accountBalances = result["list"].FirstOrDefault(
					i => i["accountType"].ToStringInvariant() == accType
			);
			if (accountBalances == null)
			{
				return amounts;
			}
			if (IsUnifiedAccount.Value)
			{
				// All assets that can be used as collateral, converted to USD, will be here
				amounts.Add(
						"USD",
						accountBalances["totalAvailableBalance"].ConvertInvariant<decimal>()
				);
			}
			string balanceKey = accType == "SPOT" ? "free" : "availableToWithdraw";
			foreach (var coin in accountBalances["coin"])
			{
				decimal amount = coin[balanceKey].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					string coinName = coin["coin"].ToStringInvariant();
					amounts[coinName] = amount;
				}
			}
			return amounts;
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload.Add("symbol", order.MarketSymbol);
			payload.Add("side", order.IsBuy ? "Buy" : "Sell");
			payload.Add("orderType", order.OrderType.ToStringInvariant());
			payload.Add("qty", order.Amount.ToStringInvariant());
			if (!string.IsNullOrWhiteSpace(order.ClientOrderId))
			{
				payload.Add("orderLinkId", order.ClientOrderId);
			}
			else if (MarketCategory == MarketCategory.Option)
			{
				throw new ArgumentNullException(
						"OrderLinkId is required for market category 'Option'"
				);
			}
			if (order.Price > 0)
			{
				payload.Add("price", order.Price.ToStringInvariant());
			}
			else if (order.OrderType == OrderType.Limit)
			{
				throw new ArgumentNullException("Price is required for LIMIT order type.");
			}
			if (order.OrderType == OrderType.Stop)
			{
				if (order.StopPrice == 0)
				{
					throw new ArgumentNullException("StopPrice is required for STOP order type.");
				}
				if (MarketCategory == MarketCategory.Spot)
				{
					payload["orderFilter"] = "tpslOrder";
				}
				payload["triggerPrice"] = order.StopPrice.ToStringInvariant();
				payload["orderType"] = order.Price > 0m ? "Limit" : "Market";
				payload["triggerDirection"] = order.IsBuy ? 1 : 2;
			}
			order.ExtraParameters.CopyTo(payload);

			JToken token = await MakeJsonRequestAsync<JToken>(
					"/v5/order/create",
					null,
					payload,
					"POST"
			);
			ExchangeOrderResult orderResult = new ExchangeOrderResult()
			{
				Amount = order.Amount,
				MarketSymbol = order.MarketSymbol,
				IsBuy = order.IsBuy,
				Price = order.Price,
				ClientOrderId = order.ClientOrderId,
				OrderId = token["orderId"].ToStringInvariant()
			};
			return orderResult;
		}

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string? marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload.Add("symbol", marketSymbol);
			if (isClientOrderId)
			{
				payload.Add("orderLinkId", orderId);
			}
			else
			{
				payload.Add("orderId", orderId);
			}
			try
			{
				await MakeJsonRequestAsync<JToken>("/v5/order/cancel", null, payload, "POST");
			}
			catch (APIException e)
			{
				// Spot STOP orders need to be cancelled in a specific way
				if (
						MarketCategory == MarketCategory.Spot
						&& (e.Message.Contains("170145") || e.Message.Contains("170213"))
				) // 170145 - This order type does not support cancellation, 170213 - Order does not exist.
				{
					payload = await GetNoncePayloadAsync();
					payload.Add("symbol", marketSymbol);
					if (isClientOrderId)
					{
						payload.Add("orderLinkId", orderId);
					}
					else
					{
						payload.Add("orderId", orderId);
					}
					payload.Add("orderFilter", "tpslOrder");
					await MakeJsonRequestAsync<JToken>("/v5/order/cancel", null, payload, "POST");
				}
				else
					throw;
			}
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string? marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			// WARNING: This method can't query spot STOP orders.
			// Need to add bool paramater to distinguish stop orders and then if order is stop add 'orderFilter' to payload. Like in GetRecentOrderAsync()
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			if (
					(
							MarketCategory == MarketCategory.Linear
							|| MarketCategory == MarketCategory.Inverse
					) && string.IsNullOrEmpty(marketSymbol)
			)
			{
				throw new ArgumentNullException(
						"marketSymbol is null. For linear & inverse, either symbol or settleCoin is required"
				);
			}
			payload.Add("symbol", marketSymbol);
			if (isClientOrderId)
			{
				payload.Add("orderLinkId", orderId);
			}
			else
			{
				payload.Add("orderId", orderId);
			}

			JToken obj = await MakeJsonRequestAsync<JToken>("/v5/order/history", null, payload);
			var item = obj?["list"]?.FirstOrDefault();
			return item == null ? null : ParseOrder(item);
		}

		public async Task<ExchangeOrderResult> GetRecentOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false,
				bool? isStop = null
		)
		{
			if (
					(
							MarketCategory == MarketCategory.Linear
							|| MarketCategory == MarketCategory.Inverse
					) && string.IsNullOrEmpty(marketSymbol)
			)
			{
				throw new ArgumentNullException(
						"marketSymbol is null. For linear & inverse, either symbol or settleCoin is required"
				);
			}
			if (IsUnifiedAccount == null)
			{
				await GetAccountUnifiedStatusAsync();
			}
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload.Add("symbol", marketSymbol);
			int openOnlyInt =
					MarketCategory == MarketCategory.Spot && !IsUnifiedAccount.Value
							? 0
							: MarketCategory == MarketCategory.Inverse
							|| (MarketCategory == MarketCategory.Linear && !IsUnifiedAccount.Value)
									? 2
									: 1;
			if (MarketCategory == MarketCategory.Spot && !IsUnifiedAccount.Value)
			{
				if (isStop == null)
				{
					throw new ArgumentNullException("isStop needed for spot not-unified account");
				}
				if (isStop.Value)
				{
					payload.Add("orderFilter", "tpslOrder");
				}
			}
			payload.Add("openOnly", openOnlyInt);

			if (isClientOrderId)
			{
				payload.Add("orderLinkId", orderId);
			}
			else
			{
				payload.Add("orderId", orderId);
			}

			JToken obj = await MakeJsonRequestAsync<JToken>("/v5/order/realtime", null, payload);
			var item = obj["list"]?.FirstOrDefault();
			return item == null ? null : ParseOrder(item);
		}

		/// <summary>
		/// Returns open, partially filled orders, also cancelled, rejected or totally filled orders by last 10 minutes for linear, inverse and spot(unified).
		/// Spot(not unified) returns only open orders
		/// </summary>
		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload.Add("symbol", marketSymbol);
			JToken obj = await MakeJsonRequestAsync<JToken>("/v5/order/realtime", null, payload);
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			if (obj?["list"] is JArray jArray)
			{
				foreach (var item in jArray)
				{
					orders.Add(ParseOrder(item));
				}
			}
			return orders;
		}

		#endregion Private

		#region WebSockets Public

		protected override async Task<IWebSocket> OnGetCandlesWebSocketAsync(
				Func<MarketCandle, Task> callbackAsync,
				int periodSeconds,
				params string[] marketSymbols
		)
		{
			return await ConnectPublicWebSocketAsync(
					url: null,
					messageCallback: async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						string topic = token["topic"]?.ToStringInvariant();
						if (string.IsNullOrWhiteSpace(topic))
						{
							string op = token["op"]?.ToStringInvariant();
							if (op == "subscribe")
							{
								if (token["success"].ConvertInvariant<bool>())
								{
									Logger.Info($"Subscribed to candles websocket on {Name}");
								}
								else
								{
									Logger.Error(
														$"Error subscribing to candles websocket on {Name}: {token["ret_msg"].ToStringInvariant()}"
												);
								}
							}
							return;
						}
						var strArray = topic.Split('.');
						string symbol = strArray[strArray.Length - 1];
						int periodSecondsInResponse = StringToPeriodSeconds(
											strArray[strArray.Length - 2]
									);

						foreach (var item in token["data"] as JArray)
						{
							MarketCandle candle = this.ParseCandle(
												item,
												symbol,
												periodSecondsInResponse,
												openKey: "open",
												highKey: "high",
												lowKey: "low",
												closeKey: "close",
												timestampKey: "start",
												timestampType: TimestampType.UnixMilliseconds,
												baseVolumeKey: MarketCategory == MarketCategory.Inverse
														? "turnover"
														: "volume",
												quoteVolumeKey: MarketCategory == MarketCategory.Inverse
														? "volume"
														: "turnover"
										);
							candle.IsClosed = item["confirm"].ConvertInvariant<bool>();
							await callbackAsync(candle);
						}
					},
					connectCallback: async (_socket) =>
					{
						string interval = PeriodSecondsToString(periodSeconds);
						var subscribeRequest = new
						{
							op = "subscribe",
							args = marketSymbols.Select(s => $"kline.{interval}.{s}").ToArray()
						};
						await _socket.SendMessageAsync(subscribeRequest);
					},
					disconnectCallback: async (_socket) =>
					{
						Logger.Info($"Websocket for candles on {Name} disconnected");
					}
			);
		}

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(
				Action<ExchangeOrderBook> callback,
				int maxCount = 20,
				params string[] marketSymbols
		)
		{
			/*
					ABOUT DEPTH:
					Linear & inverse:
					Level 1 data, push frequency: 10ms
					Level 50 data, push frequency: 20ms
					Level 200 data, push frequency: 100ms
					Level 500 data, push frequency: 100ms

					Spot:
					Level 1 data, push frequency: 10ms
					Level 50 data, push frequency: 20ms

					Option:
					Level 25 data, push frequency: 20ms
					Level 100 data, push frequency: 100ms
			*/
			int depth;
			if (MarketCategory == MarketCategory.Linear || MarketCategory == MarketCategory.Inverse)
			{
				depth = (maxCount == 1 || maxCount == 200 || maxCount == 500) ? maxCount : 50; // Depth 50 by default
			}
			else if (MarketCategory == MarketCategory.Spot)
			{
				depth = maxCount == 1 ? maxCount : 50; // Depth 50 by default
			}
			else //Option
			{
				depth = maxCount == 100 ? maxCount : 25; //Depth 25 by default
			}
			return await ConnectPublicWebSocketAsync(
					url: null,
					messageCallback: (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						string topic = token["topic"]?.ToStringInvariant();
						if (string.IsNullOrWhiteSpace(topic))
						{
							string op = token["op"]?.ToStringInvariant();
							if (op == "subscribe")
							{
								if (token["success"].ConvertInvariant<bool>())
								{
									Logger.Info($"Subscribed to orderbook websocket on {Name}");
								}
								else
								{
									Logger.Error(
														$"Error subscribing to orderbook websocket on {Name}: {token["ret_msg"].ToStringInvariant()}"
												);
								}
							}
							return Task.CompletedTask;
						}
						var book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(
											token["data"],
											asks: "a",
											bids: "b",
											sequence: "u"
									);
						book.LastUpdatedUtc = CryptoUtility.ParseTimestamp(
											token["ts"],
											TimestampType.UnixMilliseconds
									);
						book.ExchangeName = Name;
						book.MarketSymbol = token["data"]["s"].ToStringInvariant();
						book.IsFromSnapshot = token["type"].ToStringInvariant() == "snapshot";
						callback(book);
						return Task.CompletedTask;
					},
					connectCallback: async (_socket) =>
					{
						var subscribeRequest = new
						{
							op = "subscribe",
							args = marketSymbols.Select(s => $"orderbook.{depth}.{s}").ToArray()
						};
						await _socket.SendMessageAsync(subscribeRequest);
					},
					disconnectCallback: async (_socket) =>
					{
						Logger.Info($"Websocket for orderbook on {Name} disconnected");
					}
			);
		}

		#endregion Websockets Public

		#region Websockets Private

		protected override async Task<IWebSocket> OnUserDataWebSocketAsync(Action<object> callback)
		{
			return await ConnectPrivateWebSocketAsync(
					url: null,
					messageCallback: (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());

						var data = token["data"];
						if (data == null)
						{
							string op = token["op"]?.ToStringInvariant();
							if (op == "auth")
							{
								if (token["success"].ConvertInvariant<bool>())
								{
									Logger.Info($"Authenticated to user data websocket on {Name}");
								}
								else
								{
									Logger.Error(
														$"Error authenticating to user data websocket on {Name}: {token["ret_msg"].ToStringInvariant()}"
												);
								}
							}
							else if (op == "subscribe")
							{
								if (token["success"].ConvertInvariant<bool>())
								{
									Logger.Info($"Subscribed to user data websocket on {Name}");
								}
								else
								{
									Logger.Error(
														$"Error subscribing to user data websocket on {Name}: {token["ret_msg"].ToStringInvariant()}"
												);
								}
							}
						}
						else
						{
							var topic = token["topic"]?.ToStringInvariant();
							switch (topic)
							{
								case "order":
									foreach (var jObject in data)
									{
										callback(ParseOrder(jObject));
									}
									break;
									// Just orders for now, other topics can be added
							}
						}
						return Task.CompletedTask;
					},
					connectCallback: async (_socket) =>
					{
						long expires =
											(long)DateTime.Now.UnixTimestampFromDateTimeMilliseconds() + 10000; //10 seconds for auth request
						string stringToSign = $"GET/realtime{expires}";
						string signature = CryptoUtility.SHA256Sign(
											stringToSign,
											CryptoUtility.ToUnsecureString(PrivateApiKey)
									);
						var authRequest = new
						{
							op = "auth",
							args = new string[]
											{
														CryptoUtility.ToUnsecureString(PublicApiKey),
														expires.ToStringInvariant(),
														signature
									}
						};
						await _socket.SendMessageAsync(authRequest);

						var subscribeRequest = new
						{
							op = "subscribe",
							args = new string[] { "order" }
						};
						await _socket.SendMessageAsync(subscribeRequest);
					},
					disconnectCallback: async (_socket) =>
					{
						Logger.Info($"Socket for user data on {Name} disconnected");
					}
			);
		}

		#endregion Websockets Private

		#region Helpers

		private ExchangeOrderResult ParseOrder(JToken jToken)
		{
			string marketSymbol = jToken["symbol"].ToStringInvariant();
			decimal executedValue = jToken["cumExecValue"].ConvertInvariant<decimal>();
			decimal amountFilled = jToken["cumExecQty"].ConvertInvariant<decimal>();
			decimal averagePrice =
					(executedValue == 0 || amountFilled == 0) ? 0m : executedValue / amountFilled;
			decimal triggerPrice = jToken["triggerPrice"]?.ConvertInvariant<decimal>() ?? 0;
			decimal price = jToken["price"].ConvertInvariant<decimal>();
			ExchangeOrderResult order = new ExchangeOrderResult
			{
				MarketSymbol = marketSymbol,
				Amount = jToken["qty"].ConvertInvariant<decimal>(),
				AmountFilled = amountFilled,
				Price = price == 0 ? triggerPrice : price,
				Fees = jToken["cumExecFee"].ConvertInvariant<decimal>(),
				FeesCurrency =
							marketSymbol.EndsWith("USDT") && MarketCategory == MarketCategory.Linear
									? "USDT"
									: null,
				AveragePrice = averagePrice,
				IsBuy = jToken["side"].ToStringInvariant() == "Buy",
				OrderDate =
							jToken["createdTime"] == null
									? default
									: CryptoUtility.ParseTimestamp(
											jToken["createdTime"],
											TimestampType.UnixMilliseconds
									),
				OrderId = jToken["orderId"]?.ToStringInvariant(),
				ClientOrderId = jToken["orderLinkId"]?.ToStringInvariant(),
				Result = StringToOrderStatus(jToken["orderStatus"].ToStringInvariant())
			};
			if (
					order.Result == ExchangeAPIOrderResult.Filled
					|| order.Result == ExchangeAPIOrderResult.Rejected
					|| order.Result == ExchangeAPIOrderResult.Canceled
			)
			{
				order.CompletedDate =
						jToken["updatedTime"] == null
								? (DateTime?)null
								: CryptoUtility.ParseTimestamp(
										jToken["updatedTime"],
										TimestampType.UnixMilliseconds
								);
				if (order.Result == ExchangeAPIOrderResult.Filled)
				{
					order.TradeDate = order.CompletedDate;
				}
			}
			return order;
		}

		public override string PeriodSecondsToString(int seconds)
		{
			switch (seconds)
			{
				// 1,3,5,15,30,60,120,240,360,720,D,M,W
				case 60:
				case 3 * 60:
				case 5 * 60:
				case 15 * 60:
				case 30 * 60:
				case 60 * 60:
				case 120 * 60:
				case 240 * 60:
				case 360 * 60:
				case 720 * 60:
					return (seconds / 60).ToStringInvariant();
				case 24 * 60 * 60:
					return "D";
				case 7 * 24 * 60 * 60:
					return "W";
				case 30 * 24 * 60 * 60:
					return "M";
				default:
					throw new ArgumentOutOfRangeException("seconds");
			}
		}

		private int StringToPeriodSeconds(string str)
		{
			switch (str)
			{
				case "M":
					return 30 * 24 * 60 * 60;
				case "W":
					return 7 * 24 * 60 * 60;
				case "D":
					return 24 * 60 * 60;
				default:
					return int.Parse(str) * 60;
			}
		}

		protected ExchangeAPIOrderResult StringToOrderStatus(string orderStatus)
		{
			switch (orderStatus)
			{
				case "Created":
				case "New":
				case "Active":
				case "Untriggered":
				case "Triggered":
					return ExchangeAPIOrderResult.Open;
				case "PartiallyFilled":
					return ExchangeAPIOrderResult.FilledPartially;
				case "Filled":
					return ExchangeAPIOrderResult.Filled;
				case "Deactivated":
				case "Cancelled":
				case "PartiallyFilledCanceled":
					return ExchangeAPIOrderResult.Canceled;
				case "PendingCancel":
					return ExchangeAPIOrderResult.PendingCancel;
				case "Rejected":
					return ExchangeAPIOrderResult.Rejected;
				default:
					return ExchangeAPIOrderResult.Unknown;
			}
		}

		#endregion Helpers
	}
}
