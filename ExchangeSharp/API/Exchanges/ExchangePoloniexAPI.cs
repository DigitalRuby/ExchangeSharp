﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    public sealed class ExchangePoloniexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://poloniex.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://api2.poloniex.com";
        public override string Name => ExchangeName.Poloniex;

        static ExchangePoloniexAPI()
        {
            // load withdrawal field counts
            var fieldCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var sr = new StringReader(Resources.ExchangeSharpResources.PoloniexWithdrawalFields))
            {
                sr.ReadLine(); // eat the header
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] split = line.Split(',');
                    if (split.Length == 2)
                    {
                        int.TryParse(split[1], out int count);
                        fieldCount[split[0]] = count;
                    }
                }
            }
            WithdrawalFieldCount = fieldCount;

            ExchangeGlobalCurrencyReplacements[typeof(ExchangePoloniexAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("STR", "XLM")
            };
        }

        public ExchangePoloniexAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            SymbolSeparator = "_";
            SymbolIsReversed = false;
        }

        /// <summary>
        /// Number of fields Poloniex provides for withdrawals since specifying
        /// extra content in the API request won't be rejected and may cause withdraweal to get stuck.
        /// </summary>
        public static IReadOnlyDictionary<string, int> WithdrawalFieldCount { get; set; }

        private void CheckError(JObject json)
        {
            if (json == null)
            {
                throw new APIException("No response from server");
            }
            JToken error = json["error"];
            if (error != null)
            {
                throw new APIException(error.ToStringInvariant());
            }
        }

        private void CheckError(JToken result)
        {
            if (result == null)
            {
                throw new APIException("No result");
            }
            else if (!(result is JArray) && result["error"] != null)
            {
                throw new APIException(result["error"].ToStringInvariant());
            }
        }

        private async Task<JToken> MakePrivateAPIRequestAsync(string command, IReadOnlyList<object> parameters = null)
        {
            Dictionary<string, object> payload = GetNoncePayload();
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

        /// <summary>Parses an order which has not been filled.</summary>
        /// <param name="result">The JToken to parse.</param>
        /// <returns>ExchangeOrderResult with the open order and how much is remaining to fill</returns>
        public ExchangeOrderResult ParseOpenOrder(JToken result)
        {
            ExchangeOrderResult order = new ExchangeOrderResult
            {
                Amount = result["startingAmount"].ConvertInvariant<decimal>(),
                IsBuy = result["type"].ToStringLowerInvariant() != "sell",
                OrderDate = ConvertDateTimeInvariant(result["date"]),
                OrderId = result["orderNumber"].ToStringInvariant(),
                Price = result["rate"].ConvertInvariant<decimal>(),
                Result = ExchangeAPIOrderResult.Pending,
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
                    if (!string.IsNullOrWhiteSpace(parsedSymbol))
                    {
                        order.Symbol = parsedSymbol;
                    }

                    if (!string.IsNullOrWhiteSpace(order.Symbol))
                    {
                        order.FeesCurrency = ParseFeesCurrency(order.IsBuy, order.Symbol);
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
                    order.OrderDate = ConvertDateTimeInvariant(trade["date"]);
                }

                // fee is a percentage taken from the traded amount rounded to 8 decimals
                order.Fees += CalculateFees(tradeAmt, tradeRate, order.IsBuy, trade["fee"].ConvertInvariant<decimal>());
            }

            // Poloniex does not provide a way to get the original price
            order.Price = order.AveragePrice;
        }

        private static decimal CalculateFees(decimal tradeAmt, decimal tradeRate, bool isBuy, decimal fee)
        {
            decimal amount = isBuy ? tradeAmt * fee : tradeAmt * tradeRate * fee;
            return Math.Round(amount, 8, MidpointRounding.AwayFromZero);
        }

        private void ParseCompletedOrderDetails(List<ExchangeOrderResult> orders, JArray trades, string symbol)
        {
            IEnumerable<string> orderNumsInTrades = trades.Select(x => x["orderNumber"].ToStringInvariant()).Distinct();
            foreach (string orderNum in orderNumsInTrades)
            {
                IEnumerable<JToken> tradesForOrder = trades.Where(x => x["orderNumber"].ToStringInvariant() == orderNum);
                ExchangeOrderResult order = new ExchangeOrderResult { OrderId = orderNum, Symbol = symbol };

                ParseOrderTrades(tradesForOrder, order);
                order.Price = order.AveragePrice;
                order.Result = ExchangeAPIOrderResult.Filled;
                orders.Add(order);
            }
        }

        private ExchangeTicker ParseTickerWebSocket(string symbol, JToken token)
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
            return new ExchangeTicker
            {
                Ask = token[2].ConvertInvariant<decimal>(),
                Bid = token[3].ConvertInvariant<decimal>(),
                Last = token[1].ConvertInvariant<decimal>(),
                Volume = new ExchangeVolume
                {
                    BaseVolume = token[5].ConvertInvariant<decimal>(),
                    BaseSymbol = symbol,
                    ConvertedVolume = token[6].ConvertInvariant<decimal>(),
                    ConvertedSymbol = symbol,
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string form = GetFormForPayload(payload);
                request.Headers["Key"] = PublicApiKey.ToUnsecureString();
                request.Headers["Sign"] = CryptoUtility.SHA512Sign(form, PrivateApiKey.ToUnsecureString());
                request.Method = "POST";
                WriteFormToRequest(request, form);
            }
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).ToUpperInvariant().Replace('-', '_');
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
                    IsEnabled = true,
                    MinConfirmations = kvp.Value["minConf"].ConvertInvariant<int>(),
                    Name = kvp.Key,
                    TxFee = kvp.Value["txFee"].ConvertInvariant<decimal>(),
                };

                string disabled = kvp.Value["disabled"].ToStringInvariant();
                string delisted = kvp.Value["delisted"].ToStringInvariant();
                string frozen = kvp.Value["frozen"].ToStringInvariant();
                if (string.Equals(disabled, "1") || string.Equals(delisted, "1") || string.Equals(frozen, "1"))
                {
                    currency.IsEnabled = false;
                }

                currencies[currency.Name] = currency;
            }

            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            var tickers = await GetTickersAsync();
            foreach (var kv in tickers)
            {
                symbols.Add(kv.Key);
            }
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
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
                var market = new ExchangeMarket { MarketName = kvp.Key, IsActive = false };

                string isFrozen = kvp.Value["isFrozen"].ToStringInvariant();
                if (string.Equals(isFrozen, "0"))
                {
                    market.IsActive = true;
                }

                string[] pairs = kvp.Key.Split('_');
                if (pairs.Length == 2)
                {
                    market.BaseCurrency = pairs[0];
                    market.MarketCurrency = pairs[1];
                    market.PriceStepSize = StepSize;
                    market.QuantityStepSize = StepSize;
                    market.MinPrice = StepSize;
                    market.MinTradeSize = minTradeSize;
                }

                markets.Add(market);
            }

            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            IEnumerable<KeyValuePair<string, ExchangeTicker>> tickers = await GetTickersAsync();
            foreach (var kv in tickers)
            {
                if (kv.Key == symbol)
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
            JObject obj = await MakeJsonRequestAsync<JObject>("/public?command=returnTicker");
            CheckError(obj);
            foreach (JProperty prop in obj.Children())
            {
                string symbol = prop.Name;
                JToken values = prop.Value;
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, new ExchangeTicker
                {
                    Ask = values["lowestAsk"].ConvertInvariant<decimal>(),
                    Bid = values["highestBid"].ConvertInvariant<decimal>(),
                    Id = values["id"].ToStringInvariant(),
                    Last = values["last"].ConvertInvariant<decimal>(),
                    Volume = new ExchangeVolume
                    {
                        BaseVolume = values["baseVolume"].ConvertInvariant<decimal>(),
                        BaseSymbol = symbol,
                        ConvertedVolume = values["quoteVolume"].ConvertInvariant<decimal>(),
                        ConvertedSymbol = symbol,
                        Timestamp = DateTime.UtcNow
                    }
                }));
            }
            return tickers;
        }

        public override IDisposable GetTickersWebSocket(System.Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            if (callback == null)
            {
                return null;
            }
            Dictionary<string, string> idsToSymbols = new Dictionary<string, string>();
            return ConnectWebSocket(string.Empty, (msg, _socket) =>
            {
                try
                {
                    JToken token = JToken.Parse(msg);
                    if (token[0].ConvertInvariant<int>() == 1002)
                    {
                        if (token is JArray outerArray && outerArray.Count > 2 && outerArray[2] is JArray array && array.Count > 9 &&
                            idsToSymbols.TryGetValue(array[0].ToStringInvariant(), out string symbol))
                        {
                            callback.Invoke(new List<KeyValuePair<string, ExchangeTicker>>
                            {
                                new KeyValuePair<string, ExchangeTicker>(symbol, ParseTickerWebSocket(symbol, array))
                            });
                        }
                    }
                }
                catch
                {
                }
            }, (_socket) =>
            {
                var tickers = GetTickers();
                foreach (var ticker in tickers)
                {
                    idsToSymbols[ticker.Value.Id] = ticker.Key;
                }
                // subscribe to ticker channel
                _socket.SendMessage("{\"command\":\"subscribe\",\"channel\":1002}");
            });
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            // {"asks":[["0.01021997",22.83117932],["0.01022000",82.3204],["0.01022480",140],["0.01023054",241.06436945],["0.01023057",140]],"bids":[["0.01020233",164.195],["0.01020232",66.22565096],["0.01020200",5],["0.01020010",66.79296968],["0.01020000",490.19563761]],"isFrozen":"0","seq":147171861}
            symbol = NormalizeSymbol(symbol);
            ExchangeOrderBook book = new ExchangeOrderBook();
            JObject obj = await MakeJsonRequestAsync<JObject>("/public?command=returnOrderBook&currencyPair=" + symbol + "&depth=" + maxCount);
            CheckError(obj);
            foreach (JArray array in obj["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() });
            }
            foreach (JArray array in obj["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() });
            }
            return book;
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100)
        {
            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            JObject obj = await MakeJsonRequestAsync<JObject>("/public?command=returnOrderBook&currencyPair=all&depth=" + maxCount);
            CheckError(obj);
            foreach (JProperty token in obj.Children())
            {
                ExchangeOrderBook book = new ExchangeOrderBook();
                foreach (JArray array in token.First["asks"])
                {
                    book.Asks.Add(new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() });
                }
                foreach (JArray array in token.First["bids"])
                {
                    book.Bids.Add(new ExchangeOrderPrice { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() });
                }
                books.Add(new KeyValuePair<string, ExchangeOrderBook>(token.Name, book));
            }
            return books;
        }

        protected override async Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            // [{"globalTradeID":245321705,"tradeID":11501281,"date":"2017-10-20 17:39:17","type":"buy","rate":"0.01022188","amount":"0.00954454","total":"0.00009756"},...]
            // https://poloniex.com/public?command=returnTradeHistory&currencyPair=BTC_LTC&start=1410158341&end=1410499372
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/public?command=returnTradeHistory&currencyPair=" + symbol;
            string url;
            DateTime timestamp;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&start=" + (long)CryptoUtility.UnixTimestampFromDateTimeSeconds(sinceDateTime.Value) + "&end=" +
                        (long)CryptoUtility.UnixTimestampFromDateTimeSeconds(sinceDateTime.Value.AddDays(1.0));
                }
                JArray obj = await MakeJsonRequestAsync<JArray>(url);
                if (obj == null || obj.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = ConvertDateTimeInvariant(obj[0]["date"]).AddSeconds(1.0);
                }
                foreach (JToken child in obj.Children())
                {
                    timestamp = ConvertDateTimeInvariant(child["date"]);
                    trades.Add(new ExchangeTrade
                    {
                        Amount = child["amount"].ConvertInvariant<decimal>(),
                        Price = child["rate"].ConvertInvariant<decimal>(),
                        Timestamp = timestamp,
                        Id = child["globalTradeID"].ConvertInvariant<long>(),
                        IsBuy = child["type"].ToStringInvariant() == "buy"
                    });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                if (!callback(trades))
                {
                    break;
                }
                trades.Clear();
                if (sinceDateTime == null)
                {
                    break;
                }
                Task.Delay(2000).Wait();
            }
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // https://poloniex.com/public?command=returnChartData&currencyPair=BTC_XMR&start=1405699200&end=9999999999&period=14400
            // [{"date":1405699200,"high":0.0045388,"low":0.00403001,"open":0.00404545,"close":0.00435873,"volume":44.34555992,"quoteVolume":10311.88079097,"weightedAverage":0.00430043}]
            symbol = NormalizeSymbol(symbol);
            string url = "/public?command=returnChartData&currencyPair=" + symbol;
            if (startDate != null)
            {
                url += "&start=" + (long)startDate.Value.UnixTimestampFromDateTimeSeconds();
            }
            url += "&end=" + (endDate == null ? long.MaxValue : (long)endDate.Value.UnixTimestampFromDateTimeSeconds());
            url += "&period=" + periodSeconds;
            JToken token = await MakeJsonRequestAsync<JToken>(url);
            CheckError(token);
            List<MarketCandle> candles = new List<MarketCandle>();
            foreach (JToken candle in token)
            {
                candles.Add(new MarketCandle
                {
                    ClosePrice = candle["close"].ConvertInvariant<decimal>(),
                    ExchangeName = Name,
                    HighPrice = candle["high"].ConvertInvariant<decimal>(),
                    LowPrice = candle["low"].ConvertInvariant<decimal>(),
                    OpenPrice = candle["open"].ConvertInvariant<decimal>(),
                    Name = symbol,
                    PeriodSeconds = periodSeconds,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(candle["date"].ConvertInvariant<long>()),
                    BaseVolume = candle["volume"].ConvertInvariant<double>(),
                    ConvertedVolume = candle["quoteVolume"].ConvertInvariant<double>(),
                    WeightedAverage = candle["weightedAverage"].ConvertInvariant<decimal>()
                });
            }

            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JToken result = await MakePrivateAPIRequestAsync("returnCompleteBalances");
            CheckError(result);
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
            CheckError(result);
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

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == OrderType.Market)
            {
                throw new NotSupportedException("Order type " + order.OrderType + " not supported");
            }

            string symbol = NormalizeSymbol(order.Symbol);

            decimal orderAmount = ClampOrderQuantity(symbol, order.Amount);
            decimal orderPrice = ClampOrderPrice(symbol, order.Price);

            List<object> orderParams = new List<object>
            {
                "currencyPair", symbol,
                "rate", orderPrice.ToStringInvariant(),
                "amount", orderAmount.ToStringInvariant()
            };
            foreach (KeyValuePair<string, object> kv in order.ExtraParameters)
            {
                orderParams.Add(kv.Key);
                orderParams.Add(kv.Value);
            }

            JToken result = await MakePrivateAPIRequestAsync(order.IsBuy ? "buy" : "sell", orderParams);
            CheckError(result);
            ExchangeOrderResult exchangeOrderResult = ParsePlacedOrder(result);
            exchangeOrderResult.Symbol = symbol;
            exchangeOrderResult.FeesCurrency = ParseFeesCurrency(order.IsBuy, symbol);
            return exchangeOrderResult;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            symbol = NormalizeSymbol(symbol);
            if (string.IsNullOrWhiteSpace(symbol))
            {
                symbol = "all";
            }

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JToken result = await MakePrivateAPIRequestAsync("returnOpenOrders", new object[] { "currencyPair", symbol });
            CheckError(result);
            if (symbol == "all")
            {
                foreach (JProperty prop in result)
                {
                    if (prop.Value is JArray array)
                    {
                        foreach (JToken token in array)
                        {
                            orders.Add(ParseOpenOrder(token));
                        }
                    }
                }
            }
            else if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    orders.Add(ParseOpenOrder(token));
                }
            }

            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            JToken result = await MakePrivateAPIRequestAsync("returnOrderTrades", new object[] { "orderNumber", orderId });
            try
            {
                CheckError(result);
            }
            catch (APIException e)
            {
                if (e.Message.Equals("Order not found, or you are not the person who placed it.", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                throw;
            }

            JArray resultArray = result as JArray;
            if (result != null && result.HasValues)
            {
                string tickerSymbol = result[0]["currencyPair"].ToStringInvariant();
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

            return null;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            symbol = string.IsNullOrWhiteSpace(symbol) ? "all" : NormalizeSymbol(symbol);

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            afterDate = afterDate ?? DateTime.UtcNow.Subtract(TimeSpan.FromDays(365.0));
            long afterTimestamp = (long)afterDate.Value.UnixTimestampFromDateTimeSeconds();
            JToken result = await MakePrivateAPIRequestAsync("returnTradeHistory", new object[] { "currencyPair", symbol, "limit", 10000, "start", afterTimestamp });
            CheckError(result);
            if (symbol != "all")
            {
                ParseCompletedOrderDetails(orders, result as JArray, symbol);
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

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            JToken token = await MakePrivateAPIRequestAsync("cancelOrder", new object[] { "orderNumber", long.Parse(orderId) });
            CheckError(token);
            if (token["success"] == null || token["success"].ConvertInvariant<int>() != 1)
            {
                throw new APIException("Failed to cancel order, success was not 1");
            }
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            // If we have an address tag, verify that Polo lets you specify it as part of the withdrawal
            if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
            {
                if (!WithdrawalFieldCount.TryGetValue(withdrawalRequest.Symbol, out int fieldCount) || fieldCount == 0)
                {
                    throw new APIException($"Coin {withdrawalRequest.Symbol} has unknown withdrawal field count. Please manually verify the number of fields allowed during a withdrawal (Address + Tag = 2) and add it to PoloniexWithdrawalFields.csv before calling Withdraw");
                }
                else if (fieldCount == 1)
                {
                    throw new APIException($"Coin {withdrawalRequest.Symbol} only allows an address to be specified and address tag {withdrawalRequest.AddressTag} was provided.");
                }
                else if (fieldCount > 2)
                {
                    throw new APIException("More than two fields on a withdrawal is unsupported.");
                }
            }

            var paramsList = new List<object> { "currency", NormalizeSymbol(withdrawalRequest.Symbol), "amount", withdrawalRequest.Amount, "address", withdrawalRequest.Address };
            if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
            {
                paramsList.Add("paymentId");
                paramsList.Add(withdrawalRequest.AddressTag);
            }

            JToken token = await MakePrivateAPIRequestAsync("withdraw", paramsList.ToArray());
            CheckError(token);

            ExchangeWithdrawalResponse resp = new ExchangeWithdrawalResponse { Message = token["response"].ToStringInvariant() };
            return resp;
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            symbol = NormalizeSymbol(symbol);

            // Never reuse IOTA addresses
            if (symbol.Equals("MIOTA", StringComparison.OrdinalIgnoreCase))
            {
                forceRegenerate = true;
            }

            IReadOnlyDictionary<string, ExchangeCurrency> currencies = GetCurrencies();
            var depositAddresses = new Dictionary<string, ExchangeDepositDetails>(StringComparer.OrdinalIgnoreCase);
            if (!forceRegenerate && !(await TryFetchExistingAddresses(symbol, currencies, depositAddresses)))
            {
                return null;
            }

            if (!depositAddresses.TryGetValue(symbol, out var depositDetails))
            {
                depositDetails = await CreateDepositAddress(symbol, currencies);
            }

            return depositDetails;
        }

        /// <summary>Gets the deposit history for a symbol</summary>
        /// <param name="symbol">(ignored) The symbol to check.</param>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            JToken result = await MakePrivateAPIRequestAsync("returnDepositsWithdrawals",
                new object[]
                {
                    "start", DateTime.MinValue.UnixTimestampFromDateTimeSeconds(),
                    "end", DateTime.UtcNow.UnixTimestampFromDateTimeSeconds()
                });

            CheckError(result);

            var transactions = new List<ExchangeTransaction>();

            foreach (JToken token in result["deposits"])
            {
                var deposit = new ExchangeTransaction
                {
                    Symbol = token["currency"].ToStringUpperInvariant(),
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

        private async Task<bool> TryFetchExistingAddresses(string symbol, IReadOnlyDictionary<string, ExchangeCurrency> currencies, Dictionary<string, ExchangeDepositDetails> depositAddresses)
        {
            JToken result = await MakePrivateAPIRequestAsync("returnDepositAddresses");
            CheckError(result);

            foreach (JToken jToken in result)
            {
                var token = (JProperty)jToken;
                var details = new ExchangeDepositDetails { Symbol = token.Name };

                if (!TryPopulateAddressAndTag(symbol, currencies, details, token.Value.ToStringInvariant()))
                {
                    return false;
                }

                depositAddresses[details.Symbol] = details;
            }

            return true;
        }

        private static bool TryPopulateAddressAndTag(string symbol, IReadOnlyDictionary<string, ExchangeCurrency> currencies, ExchangeDepositDetails details, string address)
        {
            if (currencies.TryGetValue(symbol, out ExchangeCurrency coin))
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
        /// <param name="symbol">Symbol to create an address for</param>
        /// <param name="currencies">Lookup of existing currencies</param>
        /// <returns>ExchangeDepositDetails with an address or a BaseAddress/AddressTag pair.</returns>
        private async Task<ExchangeDepositDetails> CreateDepositAddress(string symbol, IReadOnlyDictionary<string, ExchangeCurrency> currencies)
        {
            JToken result = await MakePrivateAPIRequestAsync("generateNewAddress", new object[] { "currency", symbol });
            CheckError(result);

            var details = new ExchangeDepositDetails
            {
                Symbol = symbol,
            };

            if (!TryPopulateAddressAndTag(symbol, currencies, details, result["response"].ToStringInvariant()))
            {
                return null;
            }

            return details;
        }
    }
}
