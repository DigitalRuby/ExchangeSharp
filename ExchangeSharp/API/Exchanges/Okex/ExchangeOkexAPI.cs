/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeOkexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.okex.com/api/v1";
        public string BaseUrlV2 { get; set; } = "https://www.okex.com/v2/spot";
        public override string BaseUrlWebSocket { get; set; } = "wss://real.okex.com:10441/websocket";

        public ExchangeOkexAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            SymbolSeparator = "_";
            SymbolIsUppercase = false;
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

        private async Task<Tuple<JToken, string>> MakeRequestOkexAsync(string symbol, string subUrl, string baseUrl = null)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty), baseUrl);
            return new Tuple<JToken, string>(obj, symbol);
        }
        #endregion

        #region Public APIs
        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            var m = await GetSymbolsMetadataAsync();
            return m.Select(x => x.MarketName);
        }
        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            /*
             {"code":0,"data":[{"baseCurrency":1,"collect":"0","isMarginOpen":false,"listDisplay": 0,
    "marginRiskPreRatio": 0,
    "marginRiskRatio": 0,
    "marketFrom": 103,
    "maxMarginLeverage": 0,
    "maxPriceDigit": 8,
    "maxSizeDigit": 6,
    "minTradeSize": 0.00100000,
    "online": 1,
    "productId": 12,
    "quoteCurrency": 0,
    "quoteIncrement": 1E-8,
    "quotePrecision": 4,
    "sort": 10013,
    "symbol": "ltc_btc"
},
             */
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken allSymbols = await MakeJsonRequestAsync<JToken>("/markets/products", BaseUrlV2);
            foreach (JToken symbol in allSymbols)
            {
                var marketName = symbol["symbol"].ToStringLowerInvariant();
                string[] pieces = marketName.Split('_');
                var market = new ExchangeMarket
                {
                    MarketName = marketName,
                    IsActive = symbol["online"].ConvertInvariant<bool>(),
                    BaseCurrency = pieces[1],
                    MarketCurrency = pieces[0],
                };

                var quotePrecision = symbol["quotePrecision"].ConvertInvariant<double>();
                var quantityStepSize = Math.Pow(10, -quotePrecision);
                market.QuantityStepSize = quantityStepSize.ConvertInvariant<decimal>();
                var maxSizeDigit = symbol["maxSizeDigit"].ConvertInvariant<double>();
                var maxTradeSize = Math.Pow(10, maxSizeDigit);
                market.MaxTradeSize = maxTradeSize.ConvertInvariant<decimal>() - 1.0m;
                market.MinTradeSize = symbol["minTradeSize"].ConvertInvariant<decimal>();

                market.PriceStepSize = symbol["quoteIncrement"].ConvertInvariant<decimal>();
                market.MinPrice = market.PriceStepSize.Value;
                var maxPriceDigit = symbol["maxPriceDigit"].ConvertInvariant<double>();
                var maxPrice = Math.Pow(10, maxPriceDigit);
                market.MaxPrice = maxPrice.ConvertInvariant<decimal>() - 1.0m;

                markets.Add(market);
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var data = await MakeRequestOkexAsync(symbol, "/ticker.do?symbol=$SYMBOL$");
            return ParseTicker(data.Item2, data.Item1);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            var data = await MakeRequestOkexAsync(null, "/markets/index-tickers?limit=100000000", BaseUrlV2);
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            string symbol;
            foreach (JToken token in data.Item1)
            {
                symbol = token["symbol"].ToStringInvariant();
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ParseTickerV2(symbol, token)));
            }
            return tickers;
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols)
        {
            /*
            {[
              {
                "binary": 0,
                "channel": "addChannel",
                "data": {
                  "result": true,
                  "channel": "ok_sub_spot_btc_usdt_deals"
                }
              }
            ]}


            {[
              {
                "binary": 0,
                "channel": "ok_sub_spot_btc_usdt_deals",
                "data": [
                  [
                    "335599480",
                    "7396",
                    "0.0031002",
                    "20:23:51",
                    "bid"
                  ],
                  [
                    "335599497",
                    "7395.9153",
                    "0.0031",
                    "20:23:51",
                    "bid"
                  ],
                  [
                    "335599499",
                    "7395.7889",
                    "0.00409436",
                    "20:23:51",
                    "ask"
                  ],

                ]
              }
            ]}
            */

            return ConnectWebSocketOkex(async (_socket) =>
            {
                symbols = await AddSymbolsToChannel(_socket, "ok_sub_spot_{0}_deals", symbols);
            }, (_socket, symbol, sArray, token) =>
            {
                IEnumerable<ExchangeTrade> trades = ParseTradesWebSocket(token);
                foreach (var trade in trades)
                {
                    callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
                }
                return Task.CompletedTask;
            });
        }

        protected override IWebSocket OnGetOrderBookDeltasWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols)
        {
            /*
{[
  {
    "binary": 0,
    "channel": "addChannel",
    "data": {
      "result": true,
      "channel": "ok_sub_spot_bch_btc_depth_5"
    }
  }
]}



{[
  {
    "data": {
      "asks": [
        [
          "8364.1163",
          "0.005"
        ],
  
      ],
      "bids": [
        [
          "8335.99",
          "0.01837999"
        ],
        [
          "8335.9899",
          "0.06"
        ],
      ],
      "timestamp": 1526734386064
    },
    "binary": 0,
    "channel": "ok_sub_spot_btc_usdt_depth_20"
  }
]}
                 
                 */

            return ConnectWebSocketOkex(async (_socket) =>
            {
                symbols = await AddSymbolsToChannel(_socket, $"ok_sub_spot_{{0}}_depth_{maxCount}", symbols);
            }, (_socket, symbol, sArray, token) =>
            {
                ExchangeOrderBook book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, sequence: "timestamp", maxCount: maxCount);
                book.Symbol = symbol;
                callback(book);
                return Task.CompletedTask;
            });
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            var token = await MakeRequestOkexAsync(symbol, "/depth.do?symbol=$SYMBOL$");
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token.Item1, maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> allTrades = new List<ExchangeTrade>();
            var trades = await MakeRequestOkexAsync(symbol, "/trades.do?symbol=$SYMBOL$");
            foreach (JToken trade in trades.Item1)
            {
                // [ { "date": "1367130137", "date_ms": "1367130137000", "price": 787.71, "amount": 0.003, "tid": "230433", "type": "sell" } ]
                allTrades.Add(trade.ParseTrade("amount", "price", "type", "date_ms", TimestampType.UnixMilliseconds, "tid"));
            }
            callback(allTrades);
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
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
            string url = "/kline.do?symbol=" + symbol;
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
                candles.Add(this.ParseCandle(token, symbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixMilliseconds, 5));
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
            payload["symbol"] = order.Symbol;
            payload["type"] = (order.IsBuy ? "buy" : "sell");

            // Okex has strict rules on which prices and quantities are allowed. They have to match the rules defined in the market definition.
            decimal outputQuantity = await ClampOrderQuantity(order.Symbol, order.Amount);
            decimal outputPrice = await ClampOrderPrice(order.Symbol, order.Price);

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

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            if (symbol.Length == 0)
            {
                throw new InvalidOperationException("Okex cancel order request requires symbol");
            }
            payload["symbol"] = symbol;
            payload["order_id"] = orderId;
            await MakeJsonRequestAsync<JToken>("/cancel_order.do", BaseUrl, payload, "POST");
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            if (symbol.Length == 0)
            {
                throw new InvalidOperationException("Okex single order details request requires symbol");
            }
            payload["symbol"] = symbol;
            payload["order_id"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/order_info.do", BaseUrl, payload, "POST");
            foreach (JToken order in token["orders"])
            {
                orders.Add(ParseOrder(order));
            }

            // only return the first
            return orders[0];
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            payload["symbol"] = symbol;
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

        private ExchangeTicker ParseTicker(string symbol, JToken data)
        {
            //{"date":"1518043621","ticker":{"high":"0.01878000","vol":"1911074.97335534","last":"0.01817627","low":"0.01813515","buy":"0.01817626","sell":"0.01823447"}}
            return this.ParseTicker(data["ticker"], symbol, "sell", "buy", "last", "vol", null, "date", TimestampType.UnixSeconds);
        }

        private ExchangeTicker ParseTickerV2(string symbol, JToken ticker)
        {
            // {"buy":"0.00001273","change":"-0.00000009","changePercentage":"-0.70%","close":"0.00001273","createdDate":1527355333053,"currencyId":535,"dayHigh":"0.00001410","dayLow":"0.00001174","high":"0.00001410","inflows":"19.52673814","last":"0.00001273","low":"0.00001174","marketFrom":635,"name":{},"open":"0.00001282","outflows":"52.53715678","productId":535,"sell":"0.00001284","symbol":"you_btc","volume":"5643177.15601228"}
            return this.ParseTicker(ticker, symbol, "sell", "buy", "last", "volume", null, "createdDate", TimestampType.UnixMilliseconds);
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
                Symbol = order.Symbol
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
                case 4:
                    return ExchangeAPIOrderResult.PendingCancel;
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
                Symbol = token["symbol"].ToStringInvariant(),
                Result = ParseOrderStatus(token["status"].ConvertInvariant<int>()),
            };

            return result;
        }

        private IEnumerable<ExchangeTrade> ParseTradesWebSocket(JToken token)
        {
            var trades = new List<ExchangeTrade>();
            foreach (var t in token)
            {
                var timeOffset = TimeSpan.FromHours(-8); //China time to utc, no DST correction needed
                var ts = TimeSpan.Parse(t[3].ToStringInvariant()) + timeOffset;
                if (ts < TimeSpan.FromHours(0)) ts += TimeSpan.FromHours(24);
                var dt = DateTime.UtcNow.Date.Add(ts);
                var trade = new ExchangeTrade()
                {
                    Id = t[0].ConvertInvariant<long>(),
                    Price = t[1].ConvertInvariant<decimal>(),
                    Amount = t[2].ConvertInvariant<decimal>(),
                    Timestamp = dt,
                    IsBuy = t[4].ToStringInvariant().EqualsWithOption("bid"),
                };
                trades.Add(trade);
            }

            return trades;
        }

        private IWebSocket ConnectWebSocketOkex(Func<IWebSocket, Task> connected, Func<IWebSocket, string, string[], JToken, Task> callback, int symbolArrayIndex = 3)
        {
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                token = token[0];
                var channel = token["channel"].ToStringInvariant();
                if (channel.EqualsWithOption("addChannel"))
                {
                    return;
                }
                else if (channel.EqualsWithOption("login"))
                {
                    if (token["data"] != null && token["data"]["result"] != null && token["data"]["result"].ConvertInvariant<bool>())
                    {
                        await callback(_socket, "login", null, null);
                    }
                }
                else
                {
                    var sArray = channel.Split('_');
                    string symbol = sArray[symbolArrayIndex] + SymbolSeparator + sArray[symbolArrayIndex + 1];
                    await callback(_socket, symbol, sArray, token["data"]);
                }
            }, async (_socket) =>
            {
                await connected(_socket);
            });
        }

        private IWebSocket ConnectPrivateWebSocketOkex(Func<IWebSocket, Task> connected, Func<IWebSocket, string, string[], JToken, Task> callback, int symbolArrayIndex = 3)
        {
            return ConnectWebSocketOkex(async (_socket) =>
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

        private async Task<string[]> AddSymbolsToChannel(IWebSocket socket, string channelFormat, string[] symbols, bool useJustFirstSymbol = false)
        {
            if (symbols == null || symbols.Length == 0)
            {
                symbols = (await GetSymbolsAsync()).ToArray();
            }
            foreach (string symbol in symbols)
            {
                string normalizedSymbol = NormalizeSymbol(symbol);
                if (useJustFirstSymbol)
                {
                    normalizedSymbol = normalizedSymbol.Substring(0, normalizedSymbol.IndexOf(SymbolSeparator[0]));
                }
                string channel = string.Format(channelFormat, normalizedSymbol);
                await socket.SendMessageAsync(new { @event = "addChannel", channel });
            }
            return symbols;
        }

        #endregion
    }

    public partial class ExchangeName { public const string Okex = "Okex"; }
}
