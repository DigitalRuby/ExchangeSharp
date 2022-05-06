/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
	using ExchangeSharp.Coinbase;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Net;
	using System.Threading.Tasks;

	public sealed partial class ExchangeCoinbaseAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.pro.coinbase.com";
		public override string BaseUrlWebSocket { get; set; } = "wss://ws-feed.pro.coinbase.com";

		/// <summary>
		/// The response will also contain a CB-AFTER header which will return the cursor id to use in your next request for the page after this one. The page after is an older page and not one that happened after this one in chronological time.
		/// </summary>
		private string cursorAfter;

		/// <summary>
		/// The response will contain a CB-BEFORE header which will return the cursor id to use in your next request for the page before the current one. The page before is a newer page and not one that happened before in chronological time.
		/// </summary>
		private string cursorBefore;

		private ExchangeCoinbaseAPI()
		{
			RequestContentType = "application/json";
			NonceStyle = NonceStyle.UnixSeconds;
			NonceEndPoint = "/time";
			NonceEndPointField = "iso";
			NonceEndPointStyle = NonceStyle.Iso8601;
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
			/* Rate limits from Coinbase Pro webpage
			 * Public endpoints - We throttle public endpoints by IP: 10 requests per second, up to 15 requests per second in bursts. Some endpoints may have custom rate limits.
			 * Private endpoints - We throttle private endpoints by profile ID: 15 requests per second, up to 30 requests per second in bursts. Some endpoints may have custom rate limits.
			 * fills endpoint has a custom rate limit of 10 requests per second, up to 20 requests per second in bursts. */
			RateLimit = new RateGate(9, TimeSpan.FromSeconds(1)); // set to 9 to be safe
		}


		private ExchangeOrderResult ParseFill(JToken result)
		{
			decimal amount = result["size"].ConvertInvariant<decimal>();
			decimal price = result["price"].ConvertInvariant<decimal>();
			string symbol = result["product_id"].ToStringInvariant();

			decimal fees = result["fee"].ConvertInvariant<decimal>();

			ExchangeOrderResult order = new ExchangeOrderResult
			{
				TradeId = result["trade_id"].ToStringInvariant(),
				Amount = amount,
				AmountFilled = amount,
				Price = price,
				Fees = fees,
				AveragePrice = price,
				IsBuy = (result["side"].ToStringInvariant() == "buy"),
				// OrderDate - not provided here. ideally would be null but ExchangeOrderResult.OrderDate is not nullable
				CompletedDate = null, // order not necessarily fully filled at this point
				TradeDate = result["created_at"].ToDateTimeInvariant(), // even though it is named "created_at", the documentation says that it is the: timestamp of fill
				MarketSymbol = symbol,
				OrderId = result["order_id"].ToStringInvariant(),
			};

			return order;
		}

		private ExchangeOrderResult ParseOrder(JToken result)
		{
			decimal executedValue = result["executed_value"].ConvertInvariant<decimal>();
			decimal amountFilled = result["filled_size"].ConvertInvariant<decimal>();
			decimal amount = result["size"].ConvertInvariant<decimal>(amountFilled);
			decimal price = result["price"].ConvertInvariant<decimal>();
			decimal stop_price = result["stop_price"].ConvertInvariant<decimal>();
			decimal? averagePrice = (amountFilled <= 0m ? null : (decimal?)(executedValue / amountFilled));
			decimal fees = result["fill_fees"].ConvertInvariant<decimal>();
			string marketSymbol = result["product_id"].ToStringInvariant(result["id"].ToStringInvariant());

			ExchangeOrderResult order = new ExchangeOrderResult
			{
				Amount = amount,
				AmountFilled = amountFilled,
				Price = price <= 0m ? stop_price : price,
				Fees = fees,
				FeesCurrency = marketSymbol.Substring(0, marketSymbol.IndexOf('-')),
				AveragePrice = averagePrice,
				IsBuy = (result["side"].ToStringInvariant() == "buy"),
				OrderDate = result["created_at"].ToDateTimeInvariant(),
				CompletedDate = result["done_at"].ToDateTimeInvariant(),
				MarketSymbol = marketSymbol,
				OrderId = result["id"].ToStringInvariant()
			};
			switch (result["status"].ToStringInvariant())
			{
				case "pending":
					order.Result = ExchangeAPIOrderResult.PendingOpen;
					break;
				case "active":
				case "open":
					if (order.Amount == order.AmountFilled)
					{
						order.Result = ExchangeAPIOrderResult.Filled;
					}
					else if (order.AmountFilled > 0.0m)
					{
						order.Result = ExchangeAPIOrderResult.FilledPartially;
					}
					else
					{
						order.Result = ExchangeAPIOrderResult.Open;
					}
					break;
				case "done":
				case "settled":
					switch (result["done_reason"].ToStringInvariant())
					{
						case "cancelled":
						case "canceled":
							order.Result = ExchangeAPIOrderResult.Canceled;
							break;
						case "filled":
							order.Result = ExchangeAPIOrderResult.Filled;
							break;
						default:
							order.Result = ExchangeAPIOrderResult.Unknown;
							break;
					}
					break;
				case "rejected":
					order.Result = ExchangeAPIOrderResult.Rejected;
					break;
				case "cancelled":
				case "canceled":
					order.Result = ExchangeAPIOrderResult.Canceled;
					break;
				default:
					throw new NotImplementedException($"Unexpected status type: {result["status"].ToStringInvariant()}");
			}
			return order;
		}

		protected override bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
		{
			return base.CanMakeAuthenticatedRequest(payload) && Passphrase != null;
		}

		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				// Coinbase is funny and wants a seconds double for the nonce, weird... we convert it to double and back to string invariantly to ensure decimal dot is used and not comma
				string timestamp = payload["nonce"].ToStringInvariant();
				payload.Remove("nonce");
				string form = CryptoUtility.GetJsonForPayload(payload);
				byte[] secret = CryptoUtility.ToBytesBase64Decode(PrivateApiKey);
				string toHash = timestamp + request.Method.ToUpperInvariant() + request.RequestUri.PathAndQuery + form;
				string signatureBase64String = CryptoUtility.SHA256SignBase64(toHash, secret);
				secret = null;
				toHash = null;
				request.AddHeader("CB-ACCESS-KEY", PublicApiKey.ToUnsecureString());
				request.AddHeader("CB-ACCESS-SIGN", signatureBase64String);
				request.AddHeader("CB-ACCESS-TIMESTAMP", timestamp);
				request.AddHeader("CB-ACCESS-PASSPHRASE", CryptoUtility.ToUnsecureString(Passphrase));
				if (request.Method == "POST")
				{
					await CryptoUtility.WriteToRequestAsync(request, form);
				}
			}
		}

		protected override void ProcessResponse(IHttpWebResponse response)
		{
			base.ProcessResponse(response);
			cursorAfter = response.GetHeader("CB-AFTER").FirstOrDefault();
			cursorBefore = response.GetHeader("CB-BEFORE").FirstOrDefault();
		}

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			var markets = new List<ExchangeMarket>();
			JToken products = await MakeJsonRequestAsync<JToken>("/products");
			foreach (JToken product in products)
			{
				var market = new ExchangeMarket
				{
					MarketSymbol = product["id"].ToStringUpperInvariant(),
					QuoteCurrency = product["quote_currency"].ToStringUpperInvariant(),
					BaseCurrency = product["base_currency"].ToStringUpperInvariant(),
					IsActive = string.Equals(product["status"].ToStringInvariant(), "online", StringComparison.OrdinalIgnoreCase),
					MinTradeSize = product["base_min_size"].ConvertInvariant<decimal>(),
					MaxTradeSize = product["base_max_size"].ConvertInvariant<decimal>(),
					PriceStepSize = product["quote_increment"].ConvertInvariant<decimal>(),
					QuantityStepSize = product["base_increment"].ConvertInvariant<decimal>(),
				};
				markets.Add(market);
			}

			return markets;
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await GetMarketSymbolsMetadataAsync()).Where(market => market.IsActive ?? true).Select(market => market.MarketSymbol);
		}

		protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
		{
			var currencies = new Dictionary<string, ExchangeCurrency>();
			JToken products = await MakeJsonRequestAsync<JToken>("/currencies");
			foreach (JToken product in products)
			{
				var currency = new ExchangeCurrency
				{
					Name = product["id"].ToStringUpperInvariant(),
					FullName = product["name"].ToStringInvariant(),
					DepositEnabled = true,
					WithdrawalEnabled = true
				};

				currencies[currency.Name] = currency;
			}

			return currencies;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			JToken ticker = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/ticker");
			return await this.ParseTickerAsync(ticker, marketSymbol, "ask", "bid", "price", "volume", null, "time", TimestampType.Iso8601UTC);
		}

		protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
		{
			// Hack found here: https://github.com/coinbase/gdax-node/issues/91#issuecomment-352441654 + using Fiddler

			// Get coinbase accounts
			JArray accounts = await this.MakeJsonRequestAsync<JArray>("/coinbase-accounts", null, await GetNoncePayloadAsync(), "GET");

			foreach (JToken token in accounts)
			{
				string currency = token["currency"].ConvertInvariant<string>();
				if (currency.Equals(symbol, StringComparison.InvariantCultureIgnoreCase))
				{
					JToken accountWalletAddress = await this.MakeJsonRequestAsync<JToken>(
																						  $"/coinbase-accounts/{token["id"]}/addresses",
																						  null,
																						  await GetNoncePayloadAsync(),
																						  "POST");

					return new ExchangeDepositDetails { Address = accountWalletAddress["address"].ToStringInvariant(), Currency = currency };
				}

			}
			throw new APIException($"Address not found for {symbol}");
		}

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			Dictionary<string, ExchangeTicker> tickers = new Dictionary<string, ExchangeTicker>(StringComparer.OrdinalIgnoreCase);
			System.Threading.ManualResetEvent evt = new System.Threading.ManualResetEvent(false);
			List<string> symbols = (await GetMarketSymbolsAsync()).ToList();

			// stupid Coinbase does not have a one shot API call for tickers outside of web sockets
			using (var socket = await GetTickersWebSocketAsync((t) =>
			{
				lock (tickers)
				{
					if (symbols.Count != 0)
					{
						foreach (var kv in t)
						{
							if (!tickers.ContainsKey(kv.Key))
							{
								tickers[kv.Key] = kv.Value;
								symbols.Remove(kv.Key);
							}
						}
						if (symbols.Count == 0)
						{
							evt.Set();
						}
					}
				}
			}
			))
			{
				evt.WaitOne(10000);
				return tickers;
			}
		}

		protected override Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
		{
			return ConnectPublicWebSocketAsync(string.Empty, (_socket, msg) =>
			{
				string message = msg.ToStringFromUTF8();
				var book = new ExchangeOrderBook();

				// string comparison on the json text for faster deserialization
				// More likely to be an l2update so check for that first
				if (message.Contains(@"""l2update"""))
				{
					// parse delta update
					var delta = JsonConvert.DeserializeObject<Level2>(message, SerializerSettings);
					book.MarketSymbol = delta.ProductId;
					book.SequenceId = delta.Time.Ticks;
					foreach (string[] change in delta.Changes)
					{
						decimal price = change[1].ConvertInvariant<decimal>();
						decimal amount = change[2].ConvertInvariant<decimal>();
						if (change[0] == "buy")
						{
							book.Bids[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
						}
						else
						{
							book.Asks[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
						}
					}
				}
				else if (message.Contains(@"""snapshot"""))
				{
					// parse snapshot
					var snapshot = JsonConvert.DeserializeObject<Snapshot>(message, SerializerSettings);
					book.MarketSymbol = snapshot.ProductId;
					foreach (decimal[] ask in snapshot.Asks)
					{
						decimal price = ask[0];
						decimal amount = ask[1];
						book.Asks[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
					}

					foreach (decimal[] bid in snapshot.Bids)
					{
						decimal price = bid[0];
						decimal amount = bid[1];
						book.Bids[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
					}
				}
				else
				{
					// no other message type handled
					return Task.CompletedTask;
				}

				callback(book);
				return Task.CompletedTask;
			}, async (_socket) =>
			{
				// subscribe to order book channel for each symbol
				if (marketSymbols == null || marketSymbols.Length == 0)
				{
					marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
				}
				var chan = new Channel { Name = ChannelType.Level2, ProductIds = marketSymbols.ToList() };
				var channelAction = new ChannelAction { Type = ActionType.Subscribe, Channels = new List<Channel> { chan } };
				await _socket.SendMessageAsync(channelAction);
			});
		}

		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
		{
			return await ConnectPublicWebSocketAsync("/", async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["type"].ToStringInvariant() == "ticker")
				{
					ExchangeTicker ticker = await this.ParseTickerAsync(token, token["product_id"].ToStringInvariant(), "best_ask", "best_bid", "price", "volume_24h", null, "time", TimestampType.Iso8601UTC);
					callback(new List<KeyValuePair<string, ExchangeTicker>>() { new KeyValuePair<string, ExchangeTicker>(token["product_id"].ToStringInvariant(), ticker) });
				}
			}, async (_socket) =>
			{
				marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? (await GetMarketSymbolsAsync()).ToArray() : marketSymbols;
				var subscribeRequest = new
				{
					type = "subscribe",
					product_ids = marketSymbols,
					channels = new object[]
					{
						new
						{
							name = "ticker",
							product_ids = marketSymbols.ToArray()
						}
					}
				};
				await _socket.SendMessageAsync(subscribeRequest);
			});
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectPublicWebSocketAsync("/", async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["type"].ToStringInvariant() == "error")
				{ // {{ "type": "error", "message": "Failed to subscribe", "reason": "match is not a valid channel" }}
					Logger.Info(token["message"].ToStringInvariant() + ": " + token["reason"].ToStringInvariant());
					return;
				}
				if (token["type"].ToStringInvariant() != "match") return; //the ticker channel provides the trade information as well
				if (token["time"] == null) return;
				ExchangeTrade trade = ParseTradeWebSocket(token);
				string marketSymbol = token["product_id"].ToStringInvariant();
				await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
			}, async (_socket) =>
			{
				var subscribeRequest = new
				{
					type = "subscribe",
					product_ids = marketSymbols,
					channels = new object[]
					{
						new
						{
							name = "matches",
							product_ids = marketSymbols
						}
					}
				};
				await _socket.SendMessageAsync(subscribeRequest);
			});
		}

		private ExchangeTrade ParseTradeWebSocket(JToken token)
		{
			return token.ParseTradeCoinbase("size", "price", "side", "time", TimestampType.Iso8601UTC, "trade_id");
		}

		protected override async Task<IWebSocket> OnUserDataWebSocketAsync(Action<object> callback)
		{
			return await ConnectPublicWebSocketAsync("/", async (_socket, msg) =>
			{
				var token = msg.ToStringFromUTF8();
				var response = JsonConvert.DeserializeObject<BaseMessage>(token, SerializerSettings);
				switch (response.Type)
				{
					case ResponseType.Subscriptions:
						var subscription = JsonConvert.DeserializeObject<Subscription>(token, SerializerSettings);
						if (subscription.Channels == null || !subscription.Channels.Any())
						{
							Trace.WriteLine($"{nameof(OnUserDataWebSocketAsync)}() no channels subscribed");
						}
						else
						{
							Trace.WriteLine($"{nameof(OnUserDataWebSocketAsync)}() subscribed to " +
								$"{string.Join(",", subscription.Channels.Select(c => c.ToString()))}");
						}
						break;
					case ResponseType.Ticker:
						throw new NotImplementedException($"Not expecting type {response.Type} in {nameof(OnUserDataWebSocketAsync)}()");
					case ResponseType.Snapshot:
						throw new NotImplementedException($"Not expecting type {response.Type} in {nameof(OnUserDataWebSocketAsync)}()");
					case ResponseType.L2Update:
						throw new NotImplementedException($"Not expecting type {response.Type} in {nameof(OnUserDataWebSocketAsync)}()");
					case ResponseType.Heartbeat:
						var heartbeat = JsonConvert.DeserializeObject<Heartbeat>(token, SerializerSettings);
						Trace.WriteLine($"{nameof(OnUserDataWebSocketAsync)}() heartbeat received {heartbeat}");
						break;
					case ResponseType.Received:
						var received = JsonConvert.DeserializeObject<Received>(token, SerializerSettings);
						callback(received.ExchangeOrderResult);
						break;
					case ResponseType.Open:
						var open = JsonConvert.DeserializeObject<Open>(token, SerializerSettings);
						callback(open.ExchangeOrderResult);
						break;
					case ResponseType.Done:
						var done = JsonConvert.DeserializeObject<Done>(token, SerializerSettings);
						callback(done.ExchangeOrderResult);
						break;
					case ResponseType.Match:
						var match = JsonConvert.DeserializeObject<Match>(token, SerializerSettings);
						callback(match.ExchangeOrderResult);
						break;
					case ResponseType.LastMatch:
						//var lastMatch = JsonConvert.DeserializeObject<LastMatch>(token);
						throw new NotImplementedException($"Not expecting type {response.Type} in {nameof(OnUserDataWebSocketAsync)}()");
					case ResponseType.Error:
						var error = JsonConvert.DeserializeObject<Error>(token, SerializerSettings);
						throw new APIException($"{error.Reason}: {error.Message}");
					case ResponseType.Change:
						var change = JsonConvert.DeserializeObject<Change>(token, SerializerSettings);
						callback(change.ExchangeOrderResult);
						break;
					case ResponseType.Activate:
						var activate = JsonConvert.DeserializeObject<Activate>(token, SerializerSettings);
						callback(activate.ExchangeOrderResult);
						break;
					case ResponseType.Status:
						//var status = JsonConvert.DeserializeObject<Status>(token);
						throw new NotImplementedException($"Not expecting type {response.Type} in {nameof(OnUserDataWebSocketAsync)}()");
					default:
						throw new NotImplementedException($"Not expecting type {response.Type} in {nameof(OnUserDataWebSocketAsync)}()");
				}
			}, async (_socket) =>
			{
				var marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
				var nonce = await GetNoncePayloadAsync();
				string timestamp = nonce["nonce"].ToStringInvariant();
				byte[] secret = CryptoUtility.ToBytesBase64Decode(PrivateApiKey);
				string toHash = timestamp + "GET" + "/users/self/verify";
				var subscribeRequest = new
				{
					type = "subscribe",
					channels = new object[]
					{
						new
						{
							name = "user",
							product_ids = marketSymbols,
						}
					},
					signature = CryptoUtility.SHA256SignBase64(toHash, secret), // signature base 64 string
					key = PublicApiKey.ToUnsecureString(),
					passphrase = CryptoUtility.ToUnsecureString(Passphrase),
					timestamp = timestamp
			};
				await _socket.SendMessageAsync(subscribeRequest);
			});
		}

		protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
		{
			/*
            [{
                "time": "2014-11-07T22:19:28.578544Z",
                "trade_id": 74,
                "price": "10.00000000",
                "size": "0.01000000",
                "side": "buy"
            }, {
                "time": "2014-11-07T01:08:43.642366Z",
                "trade_id": 73,
                "price": "100.00000000",
                "size": "0.01000000",
                "side": "sell"
            }]
            */

			ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
			{
				Callback = callback,
				EndDate = endDate,
				ParseFunction = (JToken token) => token.ParseTrade("size", "price", "side", "time", TimestampType.Iso8601UTC, "trade_id"),
				StartDate = startDate,
				MarketSymbol = marketSymbol,
				Url = "/products/[marketSymbol]/trades",
				UrlFunction = (ExchangeHistoricalTradeHelper _state) =>
				{
					return _state.Url + (string.IsNullOrWhiteSpace(cursorBefore) ? string.Empty : "?before=" + cursorBefore.ToStringInvariant());
				}
			};
			await state.ProcessHistoricalTrades();
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
		{
			//https://docs.pro.coinbase.com/#pagination Coinbase limit is 100, however pagination can return more (4 later)
			int requestLimit = (limit == null || limit < 1 || limit > 100) ? 100 : (int)limit;

			string baseUrl = "/products/" + marketSymbol.ToUpperInvariant() + "/trades" + "?limit=" + requestLimit;
			JToken trades = await MakeJsonRequestAsync<JToken>(baseUrl);
			List<ExchangeTrade> tradeList = new List<ExchangeTrade>();
			foreach (JToken trade in trades)
			{
				tradeList.Add(trade.ParseTrade("size", "price", "side", "time", TimestampType.Iso8601UTC, "trade_id"));
			}
			return tradeList;
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 50)
		{
			string url = "/products/" + marketSymbol.ToUpperInvariant() + "/book?level=2";
			JToken token = await MakeJsonRequestAsync<JToken>(url);
			return token.ParseOrderBookFromJTokenArrays();
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
		{
			if (limit != null)
			{
				throw new APIException("Limit parameter not supported");
			}

			// /products/<product-id>/candles
			// https://api.pro.coinbase.com/products/LTC-BTC/candles?granularity=86400&start=2017-12-04T18:15:33&end=2017-12-11T18:15:33
			List<MarketCandle> candles = new List<MarketCandle>();
			string url = "/products/" + marketSymbol + "/candles?granularity=" + periodSeconds;
			if (startDate == null)
			{
				startDate = CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(1.0));
			}
			url += "&start=" + startDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
			if (endDate == null)
			{
				endDate = CryptoUtility.UtcNow;
			}
			url += "&end=" + endDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

			// time, low, high, open, close, volume
			JToken token = await MakeJsonRequestAsync<JToken>(url);
			foreach (JToken candle in token)
			{
				candles.Add(this.ParseCandle(candle, marketSymbol, periodSeconds, 3, 2, 1, 4, 0, TimestampType.UnixSeconds, 5));
			}
			// re-sort in ascending order
			candles.Sort((c1, c2) => c1.Timestamp.CompareTo(c2.Timestamp));
			return candles;
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
			JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, await GetNoncePayloadAsync(), "GET");
			foreach (JToken token in array)
			{
				decimal amount = token["balance"].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					amounts[token["currency"].ToStringInvariant()] = amount;
				}
			}
			return amounts;
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
		{
			Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
			JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, await GetNoncePayloadAsync(), "GET");
			foreach (JToken token in array)
			{
				decimal amount = token["available"].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					amounts[token["currency"].ToStringInvariant()] = amount;
				}
			}
			return amounts;
		}

		protected override async Task<Dictionary<string, decimal>> OnGetFeesAsync()
		{
			var symbols = await OnGetMarketSymbolsAsync();

			Dictionary<string, decimal> fees = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

			JObject token = await MakeJsonRequestAsync<JObject>("/fees", null, await GetNoncePayloadAsync(), "GET");
			/*
			 * We can chose between maker and taker fee, but currently ExchangeSharp only supports 1 fee rate per symbol.
			 * Here, we choose taker fee, which are usually higher
			*/
			decimal makerRate = token["taker_fee_rate"].Value<decimal>(); //percentage between 0 and 1

			fees = symbols
				.Select(symbol => new KeyValuePair<string, decimal>(symbol, makerRate))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			return fees;
		}

		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest request)
		{
			var nonce = await GenerateNonceAsync();
			var payload = new Dictionary<string, object>
			{
				{ "nonce", nonce },
				{ "amount", request.Amount },
				{ "currency", request.Currency },
				{ "crypto_address", request.Address },
				{ "add_network_fee_to_total", !request.TakeFeeFromAmount },
			};

			if (!string.IsNullOrEmpty(request.AddressTag))
			{
				payload.Add("destination_tag", request.AddressTag);
			}

			var result = await MakeJsonRequestAsync<WithdrawalResult>("/withdrawals/crypto", null, payload, "POST");
			var feeParsed = decimal.TryParse(result.Fee, out var fee);

			return new ExchangeWithdrawalResponse
			{
				Id = result.Id,
				Fee = feeParsed ? fee : (decimal?)null
			};
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
		{
			object nonce = await GenerateNonceAsync();
			Dictionary<string, object> payload = new Dictionary<string, object>
			{
				{ "nonce", nonce },
				{ "type", order.OrderType.ToStringLowerInvariant() },
				{ "side", (order.IsBuy ? "buy" : "sell") },
				{ "product_id", order.MarketSymbol },
				{ "size", order.RoundAmount().ToStringInvariant() }
			};
			payload["time_in_force"] = "GTC"; // good til cancel
			switch (order.OrderType)
			{
				case OrderType.Limit:
					if (order.IsPostOnly != null) payload["post_only"] = order.IsPostOnly; // [optional]** Post only flag, ** Invalid when time_in_force is IOC or FOK
					if (order.Price == null) throw new ArgumentNullException(nameof(order.Price));
					payload["price"] = order.Price.ToStringInvariant();
					break;

				case OrderType.Stop:
					payload["stop"] = (order.IsBuy ? "entry" : "loss");
					payload["stop_price"] = order.StopPrice.ToStringInvariant();
					if (order.Price == null) throw new ArgumentNullException(nameof(order.Price));
					payload["type"] = order.Price > 0m ? "limit" : "market";
					break;

				case OrderType.Market:
				default:
					break;
			}

			order.ExtraParameters.CopyTo(payload);
			var result = await MakeJsonRequestFullAsync<JToken>("/orders", null, payload, "POST");
			var resultOrder = ParseOrder(result.Response);
			resultOrder.HTTPHeaderDate = result.HTTPHeaderDate.Value.UtcDateTime;
			return resultOrder;
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{ // Orders may be queried using either the exchange assigned id or the client assigned client_oid. When using client_oid it must be preceded by the client: namespace.
			JToken obj = await MakeJsonRequestAsync<JToken>("/orders/" + (isClientOrderId ? "client:" : "") + orderId,
				null, await GetNoncePayloadAsync(), "GET");
			var order = ParseOrder(obj);
			if (!order.MarketSymbol.Equals(marketSymbol, StringComparison.InvariantCultureIgnoreCase))
				throw new DataMisalignedException($"Order {orderId} found, but symbols {order.MarketSymbol} and {marketSymbol} don't match");
			else return order;
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			JArray array = await MakeJsonRequestAsync<JArray>("orders?status=open&status=pending&status=active" + (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "&product_id=" + marketSymbol), null, await GetNoncePayloadAsync(), "GET");
			foreach (JToken token in array)
			{
				orders.Add(ParseOrder(token));
			}

			return orders;
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			JArray array = await MakeJsonRequestAsync<JArray>("orders?status=done" + (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "&product_id=" + marketSymbol), null, await GetNoncePayloadAsync(), "GET");
			foreach (JToken token in array)
			{
				ExchangeOrderResult result = ParseOrder(token);
				if (afterDate == null || result.OrderDate >= afterDate)
				{
					orders.Add(result);
				}
			}

			return orders;
		}

		public async Task<IEnumerable<ExchangeOrderResult>> GetFillsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			marketSymbol = NormalizeMarketSymbol(marketSymbol);
			var productId = (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "product_id=" + marketSymbol);
			do
			{
				var after = cursorAfter == null ? string.Empty : $"after={cursorAfter}&";
				await new SynchronizationContextRemover();
				await MakeFillRequest(afterDate, productId, orders, after);
			} while (cursorAfter != null);
			return orders;
		}

		private async Task MakeFillRequest(DateTime? afterDate, string productId, List<ExchangeOrderResult> orders, string after)
		{
			var interrogation = after != "" || productId != "" ? "?" : string.Empty;
			JArray array = await MakeJsonRequestAsync<JArray>($"fills{interrogation}{after}{productId}", null, await GetNoncePayloadAsync());

			foreach (JToken token in array)
			{
				ExchangeOrderResult result = ParseFill(token);
				if (afterDate == null || result.OrderDate >= afterDate)
				{
					orders.Add(result);
				}

				if (afterDate != null && result.OrderDate < afterDate)
				{
					cursorAfter = null;
					break;
				}
			}
		}

		protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
			var jToken = await MakeJsonRequestAsync<JToken>("orders/" + (isClientOrderId ? "client:" : "") + orderId, null, await GetNoncePayloadAsync(), "DELETE");
			if (jToken.ToStringInvariant() != orderId)
				throw new APIException($"Cancelled {jToken.ToStringInvariant()} when trying to cancel {orderId}");
		}
	}

	public partial class ExchangeName { public const string Coinbase = "Coinbase"; }
}
