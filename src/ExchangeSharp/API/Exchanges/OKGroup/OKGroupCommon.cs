/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

// different namespace for this since we don't want this to be easily accessible publically
namespace ExchangeSharp.OKGroup
{
	public abstract partial class OKGroupCommon : ExchangeAPI
	{
		/// <summary>
		/// Base URL V2 for the OK group API
		/// </summary>
		public abstract string BaseUrlV2 { get; set; }
		/// <summary>
		/// Base URL V3 for the OK group API
		/// </summary>
		public abstract string BaseUrlV3 { get; set; }

        /// <summary>
        /// Are futures and swap enabled?
        /// </summary>
		protected abstract bool IsFuturesAndSwapEnabled { get; }

		/// <summary>
		/// China time to utc, no DST correction needed
		/// </summary>
		private static readonly TimeSpan chinaTimeOffset = TimeSpan.FromHours(-8);

		protected OKGroupCommon() : base()
		{
            RequestContentType = "application/x-www-form-urlencoded";
            MarketSymbolSeparator = "-";
            MarketSymbolIsUppercase = true;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
		}

		public override string PeriodSecondsToString(int seconds)
        {
            return CryptoUtility.SecondsToPeriodStringLong(seconds);
        }

        private string GetPayloadForm(Dictionary<string, object> payload)
        {
            payload["api_key"] = PublicApiKey.ToUnsecureString();
            string form = CryptoUtility.GetFormForPayload(payload, false);
            string sign = form + "&secret_key=" + PrivateApiKey.ToUnsecureString();
            sign = CryptoUtility.MD5Sign(sign);
            return form + "&sign=" + sign;
        }

        private string GetAuthForWebSocket()
        {
            string apiKey = PublicApiKey.ToUnsecureString();
            string param = "api_key=" + apiKey + "&secret_key=" + PrivateApiKey.ToUnsecureString();
            string sign = CryptoUtility.MD5Sign(param);
            return $"{{ \"event\": \"login\", \"parameters\": {{ \"api_key\": \"{apiKey}\", \"sign\": \"{sign}\" }} }}";
        }

        #region ProcessRequest

        protected override JToken CheckJsonResponse(JToken result)
        {
            if (result is JArray)
            {
                return result;
            }
            JToken innerResult = result["result"];
            if (innerResult != null && !innerResult.ConvertInvariant<bool>())
            {
                throw new APIException("Result is false: " + result.ToString());
            }
            innerResult = result["code"];
            if (innerResult != null && innerResult.ConvertInvariant<int>() != 0)
            {
                throw new APIException("Code is non-zero: " + result.ToString());
            }
            return result["data"] ?? result;
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string msg = GetPayloadForm(payload);
                await CryptoUtility.WriteToRequestAsync(request, msg);
            }
        }

        private async Task<Tuple<JToken, string>> MakeRequestOkexAsync(string marketSymbol, string subUrl, string baseUrl = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", marketSymbol ?? string.Empty), baseUrl);
            return new Tuple<JToken, string>(obj, marketSymbol);
        }
        #endregion

        #region Public APIs
        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }
        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
			/* V3 spot sample
			[
				{
					"base_currency":"BTC",
					"instrument_id":"BTC-USDT",
					"min_size":"0.001",
					"quote_currency":"USDT",
					"size_increment":"0.00000001",
					"tick_size":"0.1"
				},
				{
					"base_currency":"OKB",
					"instrument_id":"OKB-USDT",
					"min_size":"1",
					"quote_currency":"USDT",
					"size_increment":"0.0001",
					"tick_size":"0.0001"
				}
			]
			*/
			List<ExchangeMarket> markets = new List<ExchangeMarket>();
			parseMarketSymbolTokens(await MakeJsonRequestAsync<JToken>(
				"/spot/v3/instruments", BaseUrlV3));
			if (IsFuturesAndSwapEnabled)
			{
				parseMarketSymbolTokens(await MakeJsonRequestAsync<JToken>(
					"/futures/v3/instruments", BaseUrlV3));
				parseMarketSymbolTokens(await MakeJsonRequestAsync<JToken>(
					"/swap/v3/instruments", BaseUrlV3));
			}
			void parseMarketSymbolTokens(JToken allMarketSymbolTokens)
			{
				foreach (JToken marketSymbolToken in allMarketSymbolTokens)
				{
					var marketName = marketSymbolToken["instrument_id"].ToStringInvariant();
					var market = new ExchangeMarket
					{
						MarketSymbol = marketName,
						IsActive = true,
						QuoteCurrency = marketSymbolToken["quote_currency"].ToStringInvariant(),
						BaseCurrency = marketSymbolToken["base_currency"].ToStringInvariant(),
						PriceStepSize = marketSymbolToken["tick_size"].ConvertInvariant<decimal>(),
						MinPrice = marketSymbolToken["tick_size"].ConvertInvariant<decimal>(), // assuming that this is also the min price since it isn't provided explicitly by the exchange
						MinTradeSize = marketSymbolToken["min_size"].ConvertInvariant<decimal>(),
						QuantityStepSize = marketSymbolToken["size_increment"].ConvertInvariant<decimal>(),
					};
					markets.Add(market);
				}
			}
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{ // V3: /api/swap/v3/instruments/BTC-USD-SWAP/ticker
			var data = await MakeRequestOkexAsync(marketSymbol,
				"/swap/v3/instruments/$SYMBOL$/ticker", baseUrl: BaseUrlV3);
            return await ParseTickerV3Async(data.Item2, data.Item1);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
            // V3: /api/spot/v3/instruments/ticker (/api is already included in base URL)
			List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
			parseData(await MakeRequestOkexAsync(null, "/spot/v3/instruments/ticker", BaseUrlV3));
			if (IsFuturesAndSwapEnabled)
			{
				parseData(await MakeRequestOkexAsync(null, "/futures/v3/instruments/ticker", BaseUrlV3));
				parseData(await MakeRequestOkexAsync(null, "/swap/v3/instruments/ticker", BaseUrlV3));
			}
			async void parseData(Tuple<JToken, string> data)
			{
				foreach (JToken token in data.Item1)
				{
					var marketSymbol = token["instrument_id"].ToStringInvariant();
					tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, await ParseTickerV3Async(marketSymbol, token)));
				}
			}
            return tickers;
        }

        protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
			/*
			 spot request:
			{"op": "subscribe", "args": ["spot/trade:BTC-USD"]}
			 futures request:
			{"op": "subscribe", "args":["futures/trade:BTC-USD-190628"]}
			 swap request:
			{"op": "subscribe", “args":["swap/trade:BTC-USD-SWAP"]}
			*/
			/*
				 response:
				 {
					  "table": "swap/trade",
					  "data": [{
							"instrument_id": "BTC-USD-SWAP",
							"price": "3250",
							"side": "sell",
							"size": "1",
							"timestamp": "2018-12-17T09:48:41.903Z",
							"trade_id": "126518511769403393"
					  }]
				 }
			 */
			return await ConnectWebSocketOkexAsync(async (_socket) =>
			{
				await AddMarketSymbolsToChannel(_socket, "/trade:{0}", marketSymbols);
			}, async (_socket, symbol, sArray, token) =>
			{
				ExchangeTrade trade;
				var instrumentType = GetInstrumentType(symbol);
				if (instrumentType == "futures")
				{
					trade = token.ParseTrade(amountKey: "qty", priceKey: "price",
					typeKey: "side", timestampKey: "timestamp",
					timestampType: TimestampType.Iso8601, idKey: "trade_id");
				}
				else
				{
					trade = token.ParseTrade(amountKey: "size", priceKey: "price",
					typeKey: "side", timestampKey: "timestamp",
					timestampType: TimestampType.Iso8601, idKey: "trade_id");
				}
				await callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
			});
        }

        protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
			/*
			 request:
			{"op": "subscribe", "args": ["swap/depth:BTC-USD-SWAP"]}

			 response-snapshot:
			 {
			    "table": "swap/depth",
			    "action": "partial",
			    "data": [{
			        "instrument_id": "BTC-USD-SWAP",
			        "asks": [["3983", "888", 10, 3],....],
			        "bids": [
			            ["3983", "789", 0, 3],....
			        ],
			        "timestamp": "2018-12-04T09:38:36.300Z",
			        "checksum": 200119424
			    }]
			}

			response-update:
			{
			    "table": "swap/depth",
			    "action": "update",
			    "data": [{
			        "instrument_id": "BTC-USD-SWAP",
			        "asks": [],
			        "bids": [
			            ["3983", "789", 0, 3]
			        ],
			        "timestamp": "2018-12-04T09:38:36.300Z",
			        "checksum": -1200119424
			    }]
			}

			 */

			return await ConnectWebSocketOkexAsync(async (_socket) =>
            {
                marketSymbols = await AddMarketSymbolsToChannel(_socket, "/depth:{0}", marketSymbols);
            }, (_socket, symbol, sArray, token) =>
            {
					 ExchangeOrderBook book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token);
                book.MarketSymbol = symbol;
                callback(book);
                return Task.CompletedTask;
            });
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            var token = await MakeRequestOkexAsync(marketSymbol, $"/spot/v3/instruments/{marketSymbol}/book", BaseUrlV3);

            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token.Item1, maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            List<ExchangeTrade> allTrades = new List<ExchangeTrade>();
            var trades = await MakeRequestOkexAsync(marketSymbol, "/trades.do?symbol=$SYMBOL$");
            foreach (JToken trade in trades.Item1)
            {
                // [ { "date": "1367130137", "date_ms": "1367130137000", "price": 787.71, "amount": 0.003, "tid": "230433", "type": "sell" } ]
                allTrades.Add(trade.ParseTrade("amount", "price", "type", "date_ms", TimestampType.UnixMilliseconds, "tid"));
            }
            callback(allTrades);
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            /*
             [
	            1417564800000,	timestamp
	            384.47,		open
	            387.13,		high
	            383.5,		low
	            387.13,		close
	            1062.04,	volume
            ]
            */

            List<MarketCandle> candles = new List<MarketCandle>();
            string url = "/kline.do?symbol=" + marketSymbol;
            if (startDate != null)
            {
                url += "&since=" + (long)startDate.Value.UnixTimestampFromDateTimeMilliseconds();
            }
            if (limit != null)
            {
                url += "&size=" + (limit.Value.ToStringInvariant());
            }
            string periodString = PeriodSecondsToString(periodSeconds);
            url += "&type=" + periodString;
            JToken obj = await MakeJsonRequestAsync<JToken>(url);
            foreach (JArray token in obj)
            {
                candles.Add(this.ParseCandle(token, marketSymbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixMilliseconds, 5));
            }
            return candles;
        }

	#endregion

		#region Private APIs
		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            /*
             {
			  "result": true,
			  "info": {
				"funds": {
				  "borrow": {
					"ssc": "0",


					"xlm": "0",
					"swftc": "0",
					"hmc": "0"
				  },
				  "free": {
					"ssc": "0",

					"swftc": "0",
					"hmc": "0"
				  },
				  "freezed": {
					"ssc": "0",

					"xlm": "0",
					"swftc": "0",
					"hmc": "0"
				  }
				}
			  }
			}
             */
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>("/userinfo.do", BaseUrl, payload, "POST");
            var funds = token["info"]["funds"];

            foreach (JProperty fund in funds)
            {
                ParseAmounts(fund.Value, amounts);
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>("/userinfo.do", BaseUrl, payload, "POST");
            var funds = token["info"]["funds"];
            var free = funds["free"];

            return ParseAmounts(funds["free"], amounts);
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["symbol"] = order.MarketSymbol;
            payload["type"] = (order.IsBuy ? "buy" : "sell");

            // Okex has strict rules on which prices and quantities are allowed. They have to match the rules defined in the market definition.
            decimal outputQuantity = await ClampOrderQuantity(order.MarketSymbol, order.Amount);
            decimal outputPrice = await ClampOrderPrice(order.MarketSymbol, order.Price);

            if (order.OrderType == OrderType.Market)
            {
                // TODO: Fix later once Okex fixes this on their end
                throw new NotSupportedException("Okex confuses price with amount while sending a market order, so market orders are disabled for now");

                /*
                payload["type"] += "_market";
                if (order.IsBuy)
                {
                    // for market buy orders, the price is to total amount you want to buy,
                    // and it must be higher than the current price of 0.01 BTC (minimum buying unit), 0.1 LTC or 0.01 ETH
                    payload["price"] = outputQuantity;
                }
                else
                {
                    // For market buy roders, the amount is not required
                    payload["amount"] = outputQuantity;
                }
                */
            }
            else
            {
                payload["price"] = outputPrice;
                payload["amount"] = outputQuantity;
            }
            order.ExtraParameters.CopyTo(payload);

            JToken obj = await MakeJsonRequestAsync<JToken>("/trade.do", BaseUrl, payload, "POST");
            order.Amount = outputQuantity;
            order.Price = outputPrice;
            return ParsePlaceOrder(obj, order);
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            if (marketSymbol.Length == 0)
            {
                throw new InvalidOperationException("Okex cancel order request requires symbol");
            }
            payload["symbol"] = marketSymbol;
            payload["order_id"] = orderId;
            await MakeJsonRequestAsync<JToken>("/cancel_order.do", BaseUrl, payload, "POST");
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            if (marketSymbol.Length == 0)
            {
                throw new InvalidOperationException("Okex single order details request requires symbol");
            }
            payload["symbol"] = marketSymbol;
            payload["order_id"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/order_info.do", BaseUrl, payload, "POST");
            foreach (JToken order in token["orders"])
            {
                orders.Add(ParseOrder(order));
            }

            // only return the first
            return orders[0];
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            payload["symbol"] = marketSymbol;
            // if order_id is -1, then return all unfilled orders, otherwise return the order specified
            payload["order_id"] = -1;
            JToken token = await MakeJsonRequestAsync<JToken>("/order_info.do", BaseUrl, payload, "POST");
            foreach (JToken order in token["orders"])
            {
                orders.Add(ParseOrder(order));
            }

            return orders;
        }
        #endregion

        #region Private Functions

        private async Task<ExchangeTicker> ParseTickerAsync(string symbol, JToken data)
        {
            //{"date":"1518043621","ticker":{"high":"0.01878000","vol":"1911074.97335534","last":"0.01817627","low":"0.01813515","buy":"0.01817626","sell":"0.01823447"}}
            return await this.ParseTickerAsync(data["ticker"], symbol, "sell", "buy", "last", "vol", null, "date", TimestampType.UnixSeconds);
        }

		private async Task<ExchangeTicker> ParseTickerV2Async(string symbol, JToken ticker)
		{
			// {"buy":"0.00001273","change":"-0.00000009","changePercentage":"-0.70%","close":"0.00001273","createdDate":1527355333053,"currencyId":535,"dayHigh":"0.00001410","dayLow":"0.00001174","high":"0.00001410","inflows":"19.52673814","last":"0.00001273","low":"0.00001174","marketFrom":635,"name":{},"open":"0.00001282","outflows":"52.53715678","productId":535,"sell":"0.00001284","symbol":"you_btc","volume":"5643177.15601228"}
			return await this.ParseTickerAsync(ticker, symbol, "sell", "buy", "last", "volume", null, "createdDate", TimestampType.UnixMilliseconds);
		}

		private async Task<ExchangeTicker> ParseTickerV3Async(string symbol, JToken ticker)
		{
			/*
			[
				{
					"best_ask":"3995.4",
					"best_bid":"3995.3",
					"instrument_id":"BTC-USDT",
					"product_id":"BTC-USDT",
					"last":"3995.3",
					"ask":"3995.4",
					"bid":"3995.3",
					"open_24h":"3989.7",
					"high_24h":"4031.9",
					"low_24h":"3968.9",
					"base_volume_24h":"31254.359231295",
					"timestamp":"2019-03-20T04:07:07.912Z",
					"quote_volume_24h":"124925963.3459723295"

				},
				{
					"best_ask":"1.3205",
					"best_bid":"1.3204",
					"instrument_id":"OKB-USDT",
					"product_id":"OKB-USDT",
					"last":"1.3205",
					"ask":"1.3205",
					"bid":"1.3204",
					"open_24h":"1.0764",
					"high_24h":"1.44",
					"low_24h":"1.0601",
					"base_volume_24h":"183010468.2062",
					"timestamp":"2019-03-20T04:07:05.878Z",
					"quote_volume_24h":"233516598.011530085"
				}
			]
			*/
			return await this.ParseTickerAsync(ticker, symbol, askKey: "best_ask", bidKey: "best_bid", lastKey: "last",
				baseVolumeKey: "base_volume_24h", quoteVolumeKey: "quote_volume_24h",
				timestampKey: "timestamp", timestampType: TimestampType.Iso8601);
		}

		private Dictionary<string, decimal> ParseAmounts(JToken token, Dictionary<string, decimal> amounts)
        {
            foreach (JProperty prop in token)
            {
                var amount = prop.Value.ConvertInvariant<decimal>();
                if (amount == 0m)
                    continue;

                if (amounts.ContainsKey(prop.Name))
                {
                    amounts[prop.Name] += amount;
                }
                else
                {
                    amounts[prop.Name] = amount;
                }
            }
            return amounts;
        }

        private ExchangeOrderResult ParsePlaceOrder(JToken token, ExchangeOrderRequest order)
        {
            /*
              {"result":true,"order_id":123456}
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = order.Amount,
                Price = order.Price,
                IsBuy = order.IsBuy,
                OrderId = token["order_id"].ToStringInvariant(),
                MarketSymbol = order.MarketSymbol
            };
            result.AveragePrice = result.Price;
            result.Result = ExchangeAPIOrderResult.Pending;

            return result;
        }

        private ExchangeAPIOrderResult ParseOrderStatus(int status)
        {
            // status: -1 = cancelled, 0 = unfilled, 1 = partially filled, 2 = fully filled, 3 = cancel request in process
            switch (status)
            {
                case -1:
                    return ExchangeAPIOrderResult.Canceled;
                case 0:
                    return ExchangeAPIOrderResult.Pending;
                case 1:
                    return ExchangeAPIOrderResult.FilledPartially;
                case 2:
                    return ExchangeAPIOrderResult.Filled;
                case 3:
                    return ExchangeAPIOrderResult.PendingCancel;
				case 4:
					return ExchangeAPIOrderResult.Error;
				default:
                    return ExchangeAPIOrderResult.Unknown;
            }
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
            {
			"result": true,
			"orders": [
				{
					"amount": 0.1,
					"avg_price": 0,
					"create_date": 1418008467000,
					"deal_amount": 0,
					"order_id": 10000591,
					"orders_id": 10000591,
					"price": 500,
					"status": 0,
					"symbol": "btc_usd",
					"type": "sell"
			},
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = token["amount"].ConvertInvariant<decimal>(),
                AmountFilled = token["deal_amount"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                AveragePrice = token["avg_price"].ConvertInvariant<decimal>(),
                IsBuy = token["type"].ToStringInvariant().StartsWith("buy"),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["create_date"].ConvertInvariant<long>()),
                OrderId = token["order_id"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant(),
                Result = ParseOrderStatus(token["status"].ConvertInvariant<int>()),
            };

            return result;
        }

        private Task<IWebSocket> ConnectWebSocketOkexAsync(Func<IWebSocket, Task> connected, Func<IWebSocket, string, string[], JToken, Task> callback, int symbolArrayIndex = 3)
        {
			Timer pingTimer = null;
            return ConnectWebSocketAsync(url: string.Empty, messageCallback: async (_socket, msg) =>
            {
				// https://github.com/okcoin-okex/API-docs-OKEx.com/blob/master/README-en.md
				// All the messages returning from WebSocket API will be optimized by Deflate compression
				var msgString = msg.ToStringFromUTF8Deflate();
				if (msgString == "pong")
				{ // received reply to our ping
					return;
				}
                JToken token = JToken.Parse(msgString);
                var eventProperty = token["event"]?.ToStringInvariant();
                if (eventProperty != null)
                {
					if (eventProperty == "error")
					{
						Logger.Info("Websocket unable to connect: " + token["message"]?.ToStringInvariant());
						return;
					}
					else if (eventProperty == "subscribe" && token["channel"] != null)
					{ // subscription successful
						if (pingTimer == null)
						{
							pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync("ping"),
								state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
						}
						return;
					}
					else return;
				}
				else if (token["table"] != null)
                {
	                var data = token["data"];
	                foreach (var dataRow in data)
	                {
		                var marketSymbol = dataRow["instrument_id"].ToStringInvariant();
		                await callback(_socket, marketSymbol, null, dataRow);
					}
                }
            }, connectCallback: async (_socket) => await connected(_socket)
			, disconnectCallback: s =>
			{
				pingTimer.Dispose();
				pingTimer = null;
				return Task.CompletedTask;
			});
        }

        private Task<IWebSocket> ConnectPrivateWebSocketOkexAsync(Func<IWebSocket, Task> connected, Func<IWebSocket, string, string[], JToken, Task> callback, int symbolArrayIndex = 3)
        {
            return ConnectWebSocketOkexAsync(async (_socket) =>
            {
                await _socket.SendMessageAsync(GetAuthForWebSocket());
            }, async (_socket, symbol, sArray, token) =>
            {
                if (symbol == "login")
                {
                    await connected(_socket);
                }
                else
                {
                    await callback(_socket, symbol, sArray, token);
                }
            }, 0);
        }

        private async Task<string[]> AddMarketSymbolsToChannel(IWebSocket socket, string channelFormat, string[] marketSymbols)
        {
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			var spotSymbols = marketSymbols.Where(ms => ms.Split('-').Length == 2);
			var futureSymbols = marketSymbols.Where(
				ms => ms.Split('-').Length == 3 && int.TryParse(ms.Split('-')[2], out int i));
			var swapSymbols = marketSymbols.Where(
				ms => ms.Split('-').Length == 3 && ms.Split('-')[2] == "SWAP");

			await sendMessageAsync("spot", spotSymbols);
			await sendMessageAsync("futures", futureSymbols);
			await sendMessageAsync("swap", swapSymbols);

			async Task sendMessageAsync(string category, IEnumerable<string> symbolsToSend)
			{
				var channels = symbolsToSend
						.Select(marketSymbol => string.Format($"{category}{channelFormat}", NormalizeMarketSymbol(marketSymbol)))
						.ToArray();
				await socket.SendMessageAsync(new { op = "subscribe", args = channels });
			}
            return marketSymbols;
        }

		private string GetInstrumentType(string marketSymbol)
		{
			string type;
			if (marketSymbol.Split('-').Length == 3 && marketSymbol.Split('-')[2] == "SWAP")
			{
				type = "swap";
			}
			else if (marketSymbol.Split('-').Length == 3 && int.TryParse(marketSymbol.Split('-')[2], out _))
			{
				type = "futures";
			}
			else
			{
				type = "spot";
			}
			return type;
		}

		#endregion
	}
}
