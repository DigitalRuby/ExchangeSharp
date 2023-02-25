/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Diagnostics;
using System.Web;

namespace ExchangeSharp
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public sealed partial class ExchangePoloniexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.poloniex.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://api2.poloniex.com";

		private ExchangePoloniexAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            MarketSymbolSeparator = "_";
            MarketSymbolIsReversed = true;
            WebSocketOrderBookType = WebSocketOrderBookType.DeltasOnly;
        }

        /// <summary>
        /// Number of fields Poloniex provides for withdrawals since specifying
        /// extra content in the API request won't be rejected and may cause withdraweal to get stuck.
        /// </summary>
        public static IReadOnlyDictionary<string, int> WithdrawalFieldCount { get; set; }

        private async Task<JToken> MakePrivateAPIRequestAsync(string command, IReadOnlyList<object> parameters = null)
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

			using var resourceStream = typeof(ExchangePoloniexAPI).Assembly.GetManifestResourceStream("ExchangeSharp.Properties.Resources.PoloniexWithdrawalFields.csv");
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
			ExchangeGlobalCurrencyReplacements["STR"] = "XLM"; // wtf
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

		/// <summary>
		/// Parses an order which has not been filled.
		/// </summary>
		/// <param name="result">The JToken to parse.</param>
		/// <param name="marketSymbol">Market symbol or null if it's in the result</param>
		/// <returns>ExchangeOrderResult with the open order and how much is remaining to fill</returns>
		public ExchangeOrderResult ParseOpenOrder(JToken result, string marketSymbol = null)
        {
            ExchangeOrderResult order = new ExchangeOrderResult
            {
                Amount = result["startingAmount"].ConvertInvariant<decimal>(),
                IsBuy = result["type"].ToStringLowerInvariant() != "sell",
                OrderDate = result["date"].ToDateTimeInvariant(),
                OrderId = result["orderNumber"].ToStringInvariant(),
                Price = result["rate"].ConvertInvariant<decimal>(),
                Result = ExchangeAPIOrderResult.Open,
                MarketSymbol = (marketSymbol ?? result.Parent.Path)
            };

            decimal amount = result["amount"].ConvertInvariant<decimal>();
            order.AmountFilled = amount - order.Amount;

            // fee is a percentage taken from the traded amount rounded to 8 decimals
            order.Fees = CalculateFees(amount, order.Price.Value, order.IsBuy, result["fee"].ConvertInvariant<decimal>());

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
					(order.AveragePrice.GetValueOrDefault(decimal.Zero) *
						order.AmountFilled.GetValueOrDefault(decimal.Zero) + tradeAmt * tradeRate) /
					(order.AmountFilled.GetValueOrDefault(decimal.Zero) + tradeAmt);
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
				order.Fees += CalculateFees(tradeAmt, tradeRate, order.IsBuy, trade["fee"].ConvertInvariant<decimal>());
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

		public void ParseClosePositionTrades(IEnumerable<JToken> trades, ExchangeCloseMarginPositionResult closePosition)
        {
            bool closePositionMetadataSet = false;
            var tradeIds = new List<string>();
            foreach (JToken trade in trades)
            {
                if (!closePositionMetadataSet)
                {
                    closePosition.IsBuy = trade["type"].ToStringLowerInvariant() != "sell";

                    if (!string.IsNullOrWhiteSpace(closePosition.MarketSymbol))
                    {
                        closePosition.FeesCurrency = ParseFeesCurrency(closePosition.IsBuy, closePosition.MarketSymbol);
                    }

                    closePositionMetadataSet = true;
                }

                decimal tradeAmt = trade["amount"].ConvertInvariant<decimal>();
                decimal tradeRate = trade["rate"].ConvertInvariant<decimal>();

                closePosition.AveragePrice = (closePosition.AveragePrice * closePosition.AmountFilled + tradeAmt * tradeRate) / (closePosition.AmountFilled + tradeAmt);
                closePosition.AmountFilled += tradeAmt;

                tradeIds.Add(trade["tradeID"].ToStringInvariant());

                if (closePosition.CloseDate == DateTime.MinValue)
                {
                    closePosition.CloseDate = trade["date"].ToDateTimeInvariant();
                }

                // fee is a percentage taken from the traded amount rounded to 8 decimals
                closePosition.Fees += CalculateFees(tradeAmt, tradeRate, closePosition.IsBuy, trade["fee"].ConvertInvariant<decimal>());
            }

            closePosition.TradeIds = tradeIds.ToArray();
        }

        private static decimal CalculateFees(decimal tradeAmt, decimal tradeRate, bool isBuy, decimal fee)
        {
            decimal amount = isBuy ? tradeAmt * fee : tradeAmt * tradeRate * fee;
            return Math.Round(amount, 8, MidpointRounding.AwayFromZero);
        }

        private void ParseCompletedOrderDetails(List<ExchangeOrderResult> orders, JToken trades, string marketSymbol)
        {
            IEnumerable<string> orderNumsInTrades = trades.Select(x => x["orderNumber"].ToStringInvariant()).Distinct();
            foreach (string orderNum in orderNumsInTrades)
            {
                IEnumerable<JToken> tradesForOrder = trades.Where(x => x["orderNumber"].ToStringInvariant() == orderNum);
                ExchangeOrderResult order = new ExchangeOrderResult { OrderId = orderNum, MarketSymbol = marketSymbol };
                ParseOrderTrades(tradesForOrder, order);
                //order.Price = order.AveragePrice;
                order.Result = ExchangeAPIOrderResult.Filled;
                orders.Add(order);
            }
        }

        private async Task<ExchangeTicker> ParseTickerWebSocketAsync(string symbol, JToken token)
        {
            /*
            last: args[1],
            lowestAsk: args[2],
            highestBid: args[3],
            percentChange: args[4],
            baseVolume: args[5],
            quoteVolume: args[6],
            isFrozen: args[7],
            high24hr: args[8],
            low24hr: args[9]
            */
            return await this.ParseTickerAsync(token, symbol, 2, 3, 1, 5, 6);
        }

        public override string PeriodSecondsToString(int seconds)
        {
	        var allowedPeriods = new[]
	        {
		        "MINUTE_1", "MINUTE_5", "MINUTE_10", "MINUTE_15",
		        "MINUTE_30", "HOUR_1", "HOUR_2", "HOUR_4", "HOUR_6",
		        "HOUR_12", "DAY_1", "DAY_3", "WEEK_1", "MONTH_1"
	        };
	        var period = CryptoUtility.SecondsToPeriodStringLongReverse(seconds);
	        var periodIsvalid = allowedPeriods.Any(x => x == period);
	        if (!periodIsvalid) throw new ArgumentOutOfRangeException(nameof(period), $"{period} is not valid period on Poloniex");

	        return period;
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
	        if (CanMakeAuthenticatedRequest(payload))
	        {
		        payload["signTimestamp"] = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
		        var form = payload.GetFormForPayload();
                var sig = $"{request.Method}\n" +
                $"{request.RequestUri.PathAndQuery}\n" +
                $"{HttpUtility.UrlEncode(form)}";
                request.AddHeader("key", PublicApiKey.ToUnsecureString());
                request.AddHeader("signature", CryptoUtility.SHA256Sign(sig, PrivateApiKey.ToUnsecureString()));
                request.AddHeader("signTimestamp", payload["signTimestamp"].ToStringInvariant());
                await request.WriteToRequestAsync(form);
            }
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            /*
             * {"1CR":{"id":1,"name":"1CRedit","txFee":"0.01000000","minConf":3,"depositAddress":null,"disabled":0,"delisted":1,"frozen":0},
             *  "XC":{"id":230,"name":"XCurrency","txFee":"0.01000000","minConf":12,"depositAddress":null,"disabled":1,"delisted":1,"frozen":0},
             *   ... }
             */
            var currencies = new Dictionary<string, ExchangeCurrency>();
            Dictionary<string, JToken> currencyMap = await MakeJsonRequestAsync<Dictionary<string, JToken>>("/public?command=returnCurrencies");
            foreach (var kvp in currencyMap)
            {
                var currency = new ExchangeCurrency
                {
                    BaseAddress = kvp.Value["depositAddress"].ToStringInvariant(),
                    FullName = kvp.Value["name"].ToStringInvariant(),
                    DepositEnabled = true,
                    WithdrawalEnabled = true,
                    MinConfirmations = kvp.Value["minConf"].ConvertInvariant<int>(),
                    Name = kvp.Key,
                    TxFee = kvp.Value["txFee"].ConvertInvariant<decimal>(),
                };

                string disabled = kvp.Value["disabled"].ToStringInvariant();
                string delisted = kvp.Value["delisted"].ToStringInvariant();
                string frozen = kvp.Value["frozen"].ToStringInvariant();
                if (string.Equals(disabled, "1") || string.Equals(delisted, "1") || string.Equals(frozen, "1"))
                {
                    currency.DepositEnabled = false;
                    currency.WithdrawalEnabled = false;
                }

                currencies[currency.Name] = currency;
            }

            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await GetMarketSymbolsMetadataAsync()).Where(x => x.IsActive.Value).Select(x => x.MarketSymbol);
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
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

			var markets = new List<ExchangeMarket>();
            var symbols = await MakeJsonRequestAsync<JToken>("/markets");

            foreach (var symbol in symbols)
            {
	            var market = new ExchangeMarket
	            {
		            MarketSymbol = symbol["symbol"].ToStringInvariant(),
		            IsActive = ParsePairState(symbol["state"].ToStringInvariant()),
		            BaseCurrency = symbol["baseCurrencyName"].ToStringInvariant(),
		            QuoteCurrency = symbol["quoteCurrencyName"].ToStringInvariant(),
		            MinTradeSize = symbol["symbolTradeLimit"]["minQuantity"].Value<decimal>(),
		            MinTradeSizeInQuoteCurrency = symbol["symbolTradeLimit"]["minAmount"].Value<decimal>(),
		            PriceStepSize = CryptoUtility.PrecisionToStepSize(symbol["symbolTradeLimit"]["priceScale"].Value<decimal>()),
		            QuantityStepSize = CryptoUtility.PrecisionToStepSize(symbol["symbolTradeLimit"]["quantityScale"].Value<decimal>()),
		            MarginEnabled = symbol["crossMargin"]["supportCrossMargin"].Value<bool>()
	            };
	            markets.Add(market);
            }

            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            IEnumerable<KeyValuePair<string, ExchangeTicker>> tickers = await GetTickersAsync();
            foreach (var kv in tickers)
            {
                if (kv.Key == marketSymbol)
                {
                    return kv.Value;
                }
            }
            return null;
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
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
		            instrument, symbol, askKey: "ask", bidKey: "bid", baseVolumeKey: "quantity", lastKey: "close",
		            quoteVolumeKey: "amount", timestampKey: "ts", timestampType: TimestampType.UnixMilliseconds);
	            tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));
            }

            return tickers;
        }

        protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] symbols)
        {
            Dictionary<string, string> idsToSymbols = new Dictionary<string, string>();
            return await ConnectPublicWebSocketAsync(string.Empty, async (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token[0].ConvertInvariant<int>() == 1002)
                {
                    if (token is JArray outerArray && outerArray.Count > 2 && outerArray[2] is JArray array && array.Count > 9 &&
                        idsToSymbols.TryGetValue(array[0].ToStringInvariant(), out string symbol))
                    {
                        callback.Invoke(new List<KeyValuePair<string, ExchangeTicker>>
                        {
                            new KeyValuePair<string, ExchangeTicker>(symbol, await ParseTickerWebSocketAsync(symbol, array))
                        });
                    }
                }
            }, async (_socket) =>
            {
                var tickers = await GetTickersAsync();
                foreach (var ticker in tickers)
                {
                    idsToSymbols[ticker.Value.Id] = ticker.Key;
                }
                // subscribe to ticker channel (1002)
                await _socket.SendMessageAsync(new { command = "subscribe", channel = 1002 });
            });
        }

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			Dictionary<int, string> messageIdToSymbol = new Dictionary<int, string>();
			Dictionary<string, int> symbolToMessageId = new Dictionary<string, int>();
			var symMeta = await GetMarketSymbolsMetadataAsync();
			foreach (var symbol in symMeta)
			{
				messageIdToSymbol.Add(int.Parse(symbol.MarketId), symbol.MarketSymbol);
				symbolToMessageId.Add(symbol.MarketSymbol, int.Parse(symbol.MarketId));
			}
			return await ConnectPublicWebSocketAsync(string.Empty, async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token.Type == JTokenType.Object && token["error"] != null)
					throw new APIException($"Exchange returned error: {token["error"].ToStringInvariant()}");
				int msgId = token[0].ConvertInvariant<int>();

				if (msgId == 1010 || token.Count() == 2) // "[7,2]"
				{
                    // this is a heartbeat message
					return;
				}

				var seq = token[1].ConvertInvariant<long>();
				var dataArray = token[2];
				foreach (var data in dataArray)
				{
					var dataType = data[0].ToStringInvariant();
					if (dataType == "i")
					{
						// can also populate messageIdToSymbol from here
						continue;
					}
					else if (dataType == "t")
					{
						if (messageIdToSymbol.TryGetValue(msgId, out string symbol))
						{   //   0        1                 2                  3         4          5            6
							// ["t", "<trade id>", <1 for buy 0 for sell>, "<price>", "<size>", <timestamp>, "<epoch_ms>"]
							ExchangeTrade trade = data.ParseTrade(amountKey: 4, priceKey: 3, typeKey: 2, timestampKey: 6,
								timestampType: TimestampType.UnixMilliseconds, idKey: 1, typeKeyIsBuyValue: "1");
							await callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
						}
					}
					else if (dataType == "o")
					{
						continue;
					}
					else
					{
						continue;
					}
				}
			}, async (_socket) =>
			{
				IEnumerable<int> marketIDs = null;
				if (marketSymbols == null || marketSymbols.Length == 0)
				{
					marketIDs = messageIdToSymbol.Keys;
				}
				else
				{
					marketIDs = marketSymbols.Select(s => symbolToMessageId[s]);
				}
				// subscribe to order book and trades channel for each symbol
				foreach (var id in marketIDs)
				{
					await _socket.SendMessageAsync(new { command = "subscribe", channel = id });
				}
			});
		}

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            Dictionary<int, Tuple<string, long>> messageIdToSymbol = new Dictionary<int, Tuple<string, long>>();
            return await ConnectPublicWebSocketAsync(string.Empty, (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                int msgId = token[0].ConvertInvariant<int>();

                //return if this is a heartbeat message
                if (msgId == 1010)
                {
                    return Task.CompletedTask;
                }

                var seq = token[1].ConvertInvariant<long>();
                var dataArray = token[2];
                ExchangeOrderBook book = new ExchangeOrderBook();
                foreach (var data in dataArray)
                {
                    var dataType = data[0].ToStringInvariant();
                    if (dataType == "i")
                    {
                        var marketInfo = data[1];
                        var market = marketInfo["currencyPair"].ToStringInvariant();
                        messageIdToSymbol[msgId] = new Tuple<string, long>(market, 0);

                        // we are only returning the deltas, this would create a full order book which we don't want, but keeping it
                        //  here for historical reference
                        /*
                        foreach (JProperty jprop in marketInfo["orderBook"][0].Cast<JProperty>())
                        {
                            var depth = new ExchangeOrderPrice
                            {
                                Price = jprop.Name.ConvertInvariant<decimal>(),
                                Amount = jprop.Value.ConvertInvariant<decimal>()
                            };
                            book.Asks[depth.Price] = depth;
                        }
                        foreach (JProperty jprop in marketInfo["orderBook"][1].Cast<JProperty>())
                        {
                            var depth = new ExchangeOrderPrice
                            {
                                Price = jprop.Name.ConvertInvariant<decimal>(),
                                Amount = jprop.Value.ConvertInvariant<decimal>()
                            };
                            book.Bids[depth.Price] = depth;
                        }
                        */
                    }
                    else if (dataType == "o")
                    {
                        //removes or modifies an existing item on the order books
                        if (messageIdToSymbol.TryGetValue(msgId, out Tuple<string, long> symbol))
                        {
                            int type = data[1].ConvertInvariant<int>();
                            var depth = new ExchangeOrderPrice { Price = data[2].ConvertInvariant<decimal>(), Amount = data[3].ConvertInvariant<decimal>() };
                            var list = (type == 1 ? book.Bids : book.Asks);
                            list[depth.Price] = depth;
                            book.MarketSymbol = symbol.Item1;
                            book.SequenceId = symbol.Item2 + 1;
                            messageIdToSymbol[msgId] = new Tuple<string, long>(book.MarketSymbol, book.SequenceId);
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                if (book != null && (book.Asks.Count != 0 || book.Bids.Count != 0))
                {
                    callback(book);
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                // subscribe to order book and trades channel for each symbol
                foreach (var sym in marketSymbols)
                {
                    await _socket.SendMessageAsync(new { command = "subscribe", channel = NormalizeMarketSymbol(sym) });
                }
            });
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
	        //https://api.poloniex.com/markets/{symbol}/orderBook?scale={scale}&limit={limit}
	        // {
		       //  "time" : 1677005825632,
		       //  "scale" : "0.01",
		       //  "asks" : [ "24702.89", "0.046082", "24702.90", "0.001681", "24703.09", "0.002037", "24710.10", "0.143572", "24712.18", "0.00118", "24713.68", "0.606951", "24724.80", "0.133", "24728.93", "0.7", "24728.94", "0.4", "24737.10", "0.135203" ],
		       //  "bids" : [ "24700.03", "1.006472", "24700.02", "0.001208", "24698.71", "0.607319", "24697.99", "0.001973", "24688.50", "0.133", "24679.41", "0.4", "24679.40", "0.135", "24678.55", "0.3", "24667.00", "0.262", "24661.39", "0.14" ],
		       //  "ts" : 1677005825637
	        // }
	        var response = await MakeJsonRequestAsync<JToken>($"/markets/{marketSymbol}/orderBook?limit={maxCount}");
            return response.ParseOrderBookFromJTokenArray();
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
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

			var tradesResponse = await MakeJsonRequestAsync<JToken>($"/markets/{marketSymbol}/trades?limit={limit}");

			var trades = tradesResponse
				.Select(t =>
					t.ParseTrade(
						amountKey: "amount", priceKey: "price", typeKey: "takerSide",
						timestampKey: "ts", TimestampType.UnixMilliseconds, idKey: "id",
						typeKeyIsBuyValue: "BUY")).ToList();

			return trades;
		}

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
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
		        url = $"{url}&startTime={new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds()}";
	        }
	        if (endDate != null)
	        {
		        url = $"{url}&endTime={new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds()}";
	        }

	        var candleResponse = await MakeJsonRequestAsync<JToken>(url);
	        return candleResponse
		        .Select(cr => this.ParseCandle(
			        cr, marketSymbol, periodSeconds, 2, 1, 0, 3, 12, TimestampType.UnixMilliseconds,
			        5, 4, 10))
		        .ToList();
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
	        // Dictionary<string, object> payload = await GetNoncePayloadAsync();
	        Dictionary<string, object> payload = new Dictionary<string, object>();
	        var response =  await MakeJsonRequestAsync<JToken>("/accounts/balances", payload: payload);
            return null;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JToken result = await MakePrivateAPIRequestAsync("returnBalances");
            foreach (JProperty child in result.Children())
            {
                decimal amount = child.Value.ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts[child.Name] = amount;
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances)
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var accountArgumentName = "account";
            var accountArgumentValue = "margin";
            JToken result = await MakePrivateAPIRequestAsync("returnAvailableAccountBalances", new object[] { accountArgumentName, accountArgumentValue });
            foreach (JProperty child in result[accountArgumentValue].Children())
            {
                decimal amount = child.Value.ConvertInvariant<decimal>();
                if (amount > 0m || includeZeroBalances)
                {
                    amounts[child.Name] = amount;
                }
            }
            return amounts;
        }

        protected override async Task<ExchangeMarginPositionResult> OnGetOpenPositionAsync(string marketSymbol)
        {
            List<object> orderParams = new List<object>
            {
                "currencyPair", marketSymbol
            };

            JToken result = await MakePrivateAPIRequestAsync("getMarginPosition", orderParams);
            ExchangeMarginPositionResult marginPositionResult = new ExchangeMarginPositionResult()
            {
                Amount = result["amount"].ConvertInvariant<decimal>(),
                Total = result["total"].ConvertInvariant<decimal>(),
                BasePrice = result["basePrice"].ConvertInvariant<decimal>(),
                LiquidationPrice = result["liquidationPrice"].ConvertInvariant<decimal>(),
                ProfitLoss = result["pl"].ConvertInvariant<decimal>(),
                LendingFees = result["lendingFees"].ConvertInvariant<decimal>(),
                Type = result["type"].ToStringInvariant(),
                MarketSymbol = marketSymbol
            };
            return marginPositionResult;
        }

        protected override async Task<ExchangeCloseMarginPositionResult> OnCloseMarginPositionAsync(string marketSymbol)
        {
            List<object> orderParams = new List<object>
            {
                "currencyPair", marketSymbol
            };

            JToken result = await MakePrivateAPIRequestAsync("closeMarginPosition", orderParams);

            ExchangeCloseMarginPositionResult closePositionResult = new ExchangeCloseMarginPositionResult()
            {
                Success = result["success"].ConvertInvariant<bool>(),
                Message = result["message"].ToStringInvariant(),
                MarketSymbol = marketSymbol
            };

            JToken symbolTrades = result["resultingTrades"];
            if (symbolTrades == null || !symbolTrades.Any())
                return closePositionResult;

            JToken trades = symbolTrades[marketSymbol];
            if (trades != null && trades.Children().Count() != 0)
            {
                ParseClosePositionTrades(trades, closePositionResult);
            }

            return closePositionResult;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == OrderType.Market)
            {
                throw new NotSupportedException("Order type " + order.OrderType + " not supported");
            }

            decimal orderAmount = await ClampOrderQuantity(order.MarketSymbol, order.Amount);
			if (order.Price == null) throw new ArgumentNullException(nameof(order.Price));
			decimal orderPrice = await ClampOrderPrice(order.MarketSymbol, order.Price.Value);

            List<object> orderParams = new List<object>
            {
                "currencyPair", order.MarketSymbol,
                "rate", orderPrice.ToStringInvariant(),
                "amount", orderAmount.ToStringInvariant()
            };
			if (order.IsPostOnly != null) { orderParams.Add("postOnly"); orderParams.Add(order.IsPostOnly.Value ? "1" : "0"); } // (optional) Set to "1" if you want this sell order to only be placed if no portion of it fills immediately.
			foreach (KeyValuePair<string, object> kv in order.ExtraParameters)
            {
                orderParams.Add(kv.Key);
                orderParams.Add(kv.Value);
            }

			JToken result = null;
			try
			{
				result = await MakePrivateAPIRequestAsync(order.IsBuy ? (order.IsMargin ? "marginBuy" : "buy") : (order.IsMargin ? "marginSell" : "sell"), orderParams);
			}
			catch (Exception e)
			{
				if (!e.Message.Contains("Unable to fill order completely"))
				{
					throw;
				}
				result = JToken.FromObject(new { orderNumber = "0", currencyPair = order.MarketSymbol });
			}
			ExchangeOrderResult exchangeOrderResult = ParsePlacedOrder(result, order);
			exchangeOrderResult.MarketSymbol = order.MarketSymbol;
            exchangeOrderResult.FeesCurrency = ParseFeesCurrency(order.IsBuy, order.MarketSymbol);
            return exchangeOrderResult;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            if (marketSymbol.Length == 0)
            {
                marketSymbol = "all";
            }

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JToken result = await MakePrivateAPIRequestAsync("returnOpenOrders", new object[] { "currencyPair", marketSymbol });
            if (marketSymbol == "all")
            {
                foreach (JProperty prop in result)
                {
                    if (prop.Value is JArray array)
                    {
                        foreach (JToken token in array)
                        {
                            orders.Add(ParseOpenOrder(token, null));
                        }
                    }
                }
            }
            else if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    orders.Add(ParseOpenOrder(token, marketSymbol));
                }
            }

            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
			if (isClientOrderId) throw new NotSupportedException("Querying by client order ID is not implemented in ExchangeSharp. Please submit a PR if you are interested in this feature");
			JToken resultArray = await MakePrivateAPIRequestAsync("returnOrderTrades", new object[] { "orderNumber", orderId });
            string tickerSymbol = resultArray[0]["currencyPair"].ToStringInvariant();
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            ParseCompletedOrderDetails(orders, resultArray, tickerSymbol);
            if (orders.Count != 1)
            {
                throw new APIException($"ReturnOrderTrades for a single orderNumber returned {orders.Count} orders. Expected 1.");
            }

            orders[0].OrderId = orderId;
            orders[0].Price = orders[0].AveragePrice;
            return orders[0];
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            marketSymbol = string.IsNullOrWhiteSpace(marketSymbol) ? "all" : NormalizeMarketSymbol(marketSymbol);

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            afterDate = afterDate ?? CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(365.0));
            long afterTimestamp = (long)afterDate.Value.UnixTimestampFromDateTimeSeconds();
            JToken result = await MakePrivateAPIRequestAsync("returnTradeHistory", new object[] { "currencyPair", marketSymbol, "limit", 10000, "start", afterTimestamp });
            if (marketSymbol != "all")
            {
                ParseCompletedOrderDetails(orders, result as JArray, marketSymbol);
            }
            else
            {
                foreach (JProperty prop in result)
                {
                    ParseCompletedOrderDetails(orders, prop.Value as JArray, prop.Name);
                }
            }
            return orders;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
			if (isClientOrderId) throw new NotSupportedException("Cancelling by client order ID is not supported in ExchangeSharp. Please submit a PR if you are interested in this feature");
			await MakePrivateAPIRequestAsync("cancelOrder", new object[] { "orderNumber", orderId.ConvertInvariant<long>() });
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            // If we have an address tag, verify that Polo lets you specify it as part of the withdrawal
            if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
            {
                if (!WithdrawalFieldCount.TryGetValue(withdrawalRequest.Currency, out int fieldCount) || fieldCount == 0)
                {
                    throw new APIException($"Coin {withdrawalRequest.Currency} has unknown withdrawal field count. Please manually verify the number of fields allowed during a withdrawal (Address + Tag = 2) and add it to PoloniexWithdrawalFields.csv before calling Withdraw");
                }
                else if (fieldCount == 1)
                {
                    throw new APIException($"Coin {withdrawalRequest.Currency} only allows an address to be specified and address tag {withdrawalRequest.AddressTag} was provided.");
                }
                else if (fieldCount > 2)
                {
                    throw new APIException("More than two fields on a withdrawal is unsupported.");
                }
            }

            var paramsList = new List<object> { "currency", NormalizeMarketSymbol(withdrawalRequest.Currency), "amount", withdrawalRequest.Amount, "address", withdrawalRequest.Address };
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

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            // Never reuse IOTA addresses
            if (currency.Equals("MIOTA", StringComparison.OrdinalIgnoreCase))
            {
                forceRegenerate = true;
            }

            IReadOnlyDictionary<string, ExchangeCurrency> currencies = await GetCurrenciesAsync();
            var depositAddresses = new Dictionary<string, ExchangeDepositDetails>(StringComparer.OrdinalIgnoreCase);
            if (!forceRegenerate && !(await TryFetchExistingAddresses(currency, currencies, depositAddresses)))
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
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
        {
            JToken result = await MakePrivateAPIRequestAsync("returnDepositsWithdrawals",
                new object[]
                {
                    "start", DateTime.MinValue.ToUniversalTime().UnixTimestampFromDateTimeSeconds(),
                    "end", CryptoUtility.UtcNow.UnixTimestampFromDateTimeSeconds()
                });

            var transactions = new List<ExchangeTransaction>();

            foreach (JToken token in result["deposits"])
            {
                var deposit = new ExchangeTransaction
                {
                    Currency = token["currency"].ToStringUpperInvariant(),
                    Address = token["address"].ToStringInvariant(),
                    Amount = token["amount"].ConvertInvariant<decimal>(),
                    BlockchainTxId = token["txid"].ToStringInvariant(),
                    Timestamp = token["timestamp"].ConvertInvariant<double>().UnixTimeStampToDateTimeSeconds()
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

        private async Task<bool> TryFetchExistingAddresses(string currency, IReadOnlyDictionary<string, ExchangeCurrency> currencies, Dictionary<string, ExchangeDepositDetails> depositAddresses)
        {
            JToken result = await MakePrivateAPIRequestAsync("returnDepositAddresses");
            foreach (JToken jToken in result)
            {
                var token = (JProperty)jToken;
                var details = new ExchangeDepositDetails { Currency = token.Name };

                if (!TryPopulateAddressAndTag(currency, currencies, details, token.Value.ToStringInvariant()))
                {
                    return false;
                }

                depositAddresses[details.Currency] = details;
            }

            return true;
        }

        private static bool TryPopulateAddressAndTag(string currency, IReadOnlyDictionary<string, ExchangeCurrency> currencies, ExchangeDepositDetails details, string address)
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
	        if (string.IsNullOrWhiteSpace(state)) return false;

	        return state == "NORMAL";
        }

        /// <summary>
        /// Create a deposit address
        /// </summary>
        /// <param name="currency">Currency to create an address for</param>
        /// <param name="currencies">Lookup of existing currencies</param>
        /// <returns>ExchangeDepositDetails with an address or a BaseAddress/AddressTag pair.</returns>
        private async Task<ExchangeDepositDetails> CreateDepositAddress(string currency, IReadOnlyDictionary<string, ExchangeCurrency> currencies)
        {
            JToken result = await MakePrivateAPIRequestAsync("generateNewAddress", new object[] { "currency", currency });
            var details = new ExchangeDepositDetails
            {
                Currency = currency,
            };

            if (!TryPopulateAddressAndTag(currency, currencies, details, result["response"].ToStringInvariant()))
            {
                return null;
            }

            return details;
        }
    }

    public partial class ExchangeName { public const string Poloniex = "Poloniex"; }
}
