/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Diagnostics;

namespace ExchangeSharp
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    public sealed partial class ExchangePoloniexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://poloniex.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://api2.poloniex.com";

        static ExchangePoloniexAPI()
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
            ExchangeGlobalCurrencyReplacements[typeof(ExchangePoloniexAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("STR", "XLM") // WTF
            };
        }

        public ExchangePoloniexAPI()
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

        public ExchangeOrderResult ParsePlacedOrder(JToken result)
        {
            ExchangeOrderResult order = new ExchangeOrderResult
            {
                OrderId = result["orderNumber"].ToStringInvariant(),
            };

            JToken trades = result["resultingTrades"];
            if (trades != null && trades.Children().Count() != 0)
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
                Result = ExchangeAPIOrderResult.Pending,
                MarketSymbol = (marketSymbol ?? result.Parent.Path)
            };

            decimal amount = result["amount"].ConvertInvariant<decimal>();
            order.AmountFilled = amount - order.Amount;

            // fee is a percentage taken from the traded amount rounded to 8 decimals
            order.Fees = CalculateFees(amount, order.Price, order.IsBuy, result["fee"].ConvertInvariant<decimal>());

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

                order.AveragePrice = (order.AveragePrice * order.AmountFilled + tradeAmt * tradeRate) / (order.AmountFilled + tradeAmt);
                order.Amount += tradeAmt;
                order.AmountFilled = order.Amount;

                if (order.OrderDate == DateTime.MinValue)
                {
                    order.OrderDate = trade["date"].ToDateTimeInvariant();
                }

                // fee is a percentage taken from the traded amount rounded to 8 decimals
                order.Fees += CalculateFees(tradeAmt, tradeRate, order.IsBuy, trade["fee"].ConvertInvariant<decimal>());
            }

            // Poloniex does not provide a way to get the original price
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
                order.Price = order.AveragePrice;
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

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string form = CryptoUtility.GetFormForPayload(payload);
                request.AddHeader("Key", PublicApiKey.ToUnsecureString());
                request.AddHeader("Sign", CryptoUtility.SHA512Sign(form, PrivateApiKey.ToUnsecureString()));
                request.Method = "POST";
                await CryptoUtility.WriteToRequestAsync(request, form);
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
            List<string> symbols = new List<string>();
            var tickers = await GetTickersAsync();
            foreach (var kv in tickers)
            {
                symbols.Add(kv.Key);
            }
            return symbols;
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            //https://poloniex.com/public?command=returnOrderBook&currencyPair=all&depth=0
            /*
             *       "BTC_CLAM": {
        "asks": [],
        "bids": [],
        "isFrozen": "0",
        "seq": 37268918
        }, ...
             */

            var markets = new List<ExchangeMarket>();
            Dictionary<string, JToken> lookup = await MakeJsonRequestAsync<Dictionary<string, JToken>>("/public?command=returnOrderBook&currencyPair=all&depth=0");
            // StepSize is 8 decimal places for both price and amount on everything at Polo
            const decimal StepSize = 0.00000001m;
            const decimal minTradeSize = 0.0001m;

            foreach (var kvp in lookup)
            {
                var market = new ExchangeMarket { MarketSymbol = kvp.Key, IsActive = false };

                string isFrozen = kvp.Value["isFrozen"].ToStringInvariant();
                if (string.Equals(isFrozen, "0"))
                {
                    market.IsActive = true;
                }

                string[] pairs = kvp.Key.Split('_');
                if (pairs.Length == 2)
                {
                    market.QuoteCurrency = pairs[0];
                    market.BaseCurrency = pairs[1];
                    market.PriceStepSize = StepSize;
                    market.QuantityStepSize = StepSize;
                    market.MinPrice = StepSize;
                    market.MinTradeSize = minTradeSize;
                }

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
            // {"BTC_LTC":{"last":"0.0251","lowestAsk":"0.02589999","highestBid":"0.0251","percentChange":"0.02390438","baseVolume":"6.16485315","quoteVolume":"245.82513926"}
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/public?command=returnTicker");
            foreach (JProperty prop in obj.Children())
            {
                string marketSymbol = prop.Name;
                JToken values = prop.Value;
                //NOTE: Poloniex uses the term "caseVolume" when referring to the QuoteCurrencyVolume
                ExchangeTicker ticker = await this.ParseTickerAsync(values, marketSymbol, "lowestAsk", "highestBid", "last", "quoteVolume", "baseVolume", idKey: "id");
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker));
            }
            return tickers;
        }

        protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] symbols)
        {
            Dictionary<string, string> idsToSymbols = new Dictionary<string, string>();
            return await ConnectWebSocketAsync(string.Empty, async (_socket, msg) =>
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
			Dictionary<int, Tuple<string, long>> messageIdToSymbol = new Dictionary<int, Tuple<string, long>>();
			return await ConnectWebSocketAsync(string.Empty, async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
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
						var marketInfo = data[1];
						var market = marketInfo["currencyPair"].ToStringInvariant();
						messageIdToSymbol[msgId] = new Tuple<string, long>(market, 0);
					}
					else if (dataType == "t")
					{
						if (messageIdToSymbol.TryGetValue(msgId, out Tuple<string, long> symbol))
						{   //   0        1                 2                  3         4          5
							// ["t", "<trade id>", <1 for buy 0 for sell>, "<price>", "<size>", <timestamp>]
							ExchangeTrade trade = data.ParseTrade(amountKey: 4, priceKey: 3, typeKey: 2, timestampKey: 5,
								timestampType: TimestampType.UnixSeconds, idKey: 1, typeKeyIsBuyValue: "1");
							await callback(new KeyValuePair<string, ExchangeTrade>(symbol.Item1, trade));
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

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            Dictionary<int, Tuple<string, long>> messageIdToSymbol = new Dictionary<int, Tuple<string, long>>();
            return await ConnectWebSocketAsync(string.Empty, (_socket, msg) =>
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
            // {"asks":[["0.01021997",22.83117932],["0.01022000",82.3204],["0.01022480",140],["0.01023054",241.06436945],["0.01023057",140]],"bids":[["0.01020233",164.195],["0.01020232",66.22565096],["0.01020200",5],["0.01020010",66.79296968],["0.01020000",490.19563761]],"isFrozen":"0","seq":147171861}
            JToken token = await MakeJsonRequestAsync<JToken>("/public?command=returnOrderBook&currencyPair=" + marketSymbol + "&depth=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100)
        {
            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/public?command=returnOrderBook&currencyPair=all&depth=" + maxCount);
            foreach (JProperty token in obj.Children())
            {
                ExchangeOrderBook book = new ExchangeOrderBook();
                foreach (JArray array in token.First["asks"])
                {
                    var depth = new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() };
                    book.Asks[depth.Price] = depth;
                }
                foreach (JArray array in token.First["bids"])
                {
                    var depth = new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() };
                    book.Bids[depth.Price] = depth;
                }
                books.Add(new KeyValuePair<string, ExchangeOrderBook>(token.Name, book));
            }
            return books;
        }

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
		{
			List<ExchangeTrade> trades = new List<ExchangeTrade>();
			//https://docs.poloniex.com/#returnorderbook note poloniex limit = 1000
			int requestLimit = (limit == null || limit < 1 || limit > 1000) ? 1000 : (int)limit;
			string url = "/public?command=returnTradeHistory&currencyPair=" + marketSymbol + "&limit=" + requestLimit ;

			//JToken obj = await MakeJsonRequestAsync<JToken>($"/aggTrades?symbol={marketSymbol}&limit={maxRequestLimit}");
			JToken obj = await MakeJsonRequestAsync<JToken>(url);

			//JToken obj = await MakeJsonRequestAsync<JToken>("/public/trades/" + marketSymbol + "?limit=" + maxRequestLimit + "?sort=DESC");
			if(obj.HasValues) { //
				foreach(JToken token in obj) {
					var trade = token.ParseTrade("amount", "rate", "type", "date", TimestampType.Iso8601, "globalTradeID");
					trades.Add(trade);
				}
			}
			return trades;
		}

		protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            // [{"globalTradeID":245321705,"tradeID":11501281,"date":"2017-10-20 17:39:17","type":"buy","rate":"0.01022188","amount":"0.00954454","total":"0.00009756"},...]
            ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
            {
                Callback = callback,
                EndDate = endDate,
                MillisecondGranularity = false,
                ParseFunction = (JToken token) => token.ParseTrade("amount", "rate", "type", "date", TimestampType.Iso8601, "globalTradeID"),
                StartDate = startDate,
                MarketSymbol = marketSymbol,
                TimestampFunction = (DateTime dt) => ((long)CryptoUtility.UnixTimestampFromDateTimeSeconds(dt)).ToStringInvariant(),
                Url = "/public?command=returnTradeHistory&currencyPair=[marketSymbol]&start={0}&end={1}"
            };
            await state.ProcessHistoricalTrades();
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // https://poloniex.com/public?command=returnChartData&currencyPair=BTC_XMR&start=1405699200&end=9999999999&period=14400
            // [{"date":1405699200,"high":0.0045388,"low":0.00403001,"open":0.00404545,"close":0.00435873,"volume":44.34555992,"quoteVolume":10311.88079097,"weightedAverage":0.00430043}]
            string url = "/public?command=returnChartData&currencyPair=" + marketSymbol;
            if (startDate != null)
            {
                url += "&start=" + (long)startDate.Value.UnixTimestampFromDateTimeSeconds();
            }
            url += "&end=" + (endDate == null ? long.MaxValue.ToStringInvariant() : ((long)endDate.Value.UnixTimestampFromDateTimeSeconds()).ToStringInvariant());
            url += "&period=" + periodSeconds.ToStringInvariant();
            JToken token = await MakeJsonRequestAsync<JToken>(url);
            List<MarketCandle> candles = new List<MarketCandle>();
            foreach (JToken candle in token)
            {
                candles.Add(this.ParseCandle(candle, marketSymbol, periodSeconds, "open", "high", "low", "close", "date", TimestampType.UnixSeconds, "quoteVolume", "volume", "weightedAverage"));
            }
            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JToken result = await MakePrivateAPIRequestAsync("returnCompleteBalances");
            foreach (JProperty child in result.Children())
            {
                decimal amount = child.Value["available"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts[child.Name] = amount;
                }
            }
            return amounts;
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
            decimal orderPrice = await ClampOrderPrice(order.MarketSymbol, order.Price);

            List<object> orderParams = new List<object>
            {
                "currencyPair", order.MarketSymbol,
                "rate", orderPrice.ToStringInvariant(),
                "amount", orderAmount.ToStringInvariant()
            };
            foreach (KeyValuePair<string, object> kv in order.ExtraParameters)
            {
                orderParams.Add(kv.Key);
                orderParams.Add(kv.Value);
            }

            JToken result = await MakePrivateAPIRequestAsync(order.IsBuy ? (order.IsMargin ? "marginBuy" : "buy") : (order.IsMargin ? "marginSell" : "sell"), orderParams);
            ExchangeOrderResult exchangeOrderResult = ParsePlacedOrder(result);
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

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
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

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
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

            JToken token = await MakePrivateAPIRequestAsync("withdraw", paramsList.ToArray());
            ExchangeWithdrawalResponse resp = new ExchangeWithdrawalResponse { Message = token["response"].ToStringInvariant() };
            return resp;
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
