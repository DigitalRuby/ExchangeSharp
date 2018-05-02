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
    public sealed class ExchangeOkexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.okex.com/api/v1";
        public override string Name => ExchangeName.Okex;

        public string BaseUrlV2 { get; set; } = "https://www.okex.com/v2";

        public ExchangeOkexAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).ToLowerInvariant().Replace('-', '_');
        }

        #region ProcessRequest
        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                payload.Remove("nonce");
                payload["api_key"] = PublicApiKey.ToUnsecureString();
                var msg = GetFormForPayload(payload, false);
                msg = string.Join("&", new SortedSet<string>(msg.Split('&'), StringComparer.Ordinal));
                var sign = msg + "&secret_key=" + PrivateApiKey.ToUnsecureString();
                sign = CryptoUtility.MD5Sign(sign);
                msg += "&sign=" + sign;

                WriteFormToRequest(request, msg);
            }
        }

        private async Task<Tuple<JToken, string>> MakeRequestOkexAsync(string symbol, string subUrl)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty));
            CheckError(obj);
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
            if (ReadCache("GetSymbolsMetadata", out List<ExchangeMarket> markets))
            {
                return markets;
            }

            markets = new List<ExchangeMarket>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/markets/products", BaseUrlV2);
            CheckError(obj);
            JToken allSymbols = obj["data"];
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

            WriteCache("GetSymbolsMetadata", TimeSpan.FromMinutes(60.0), markets);

            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var data = await MakeRequestOkexAsync(symbol, "/ticker.do?symbol=$SYMBOL$");
            return ParseTicker(data.Item2, data.Item1);
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            var token = await MakeRequestOkexAsync(symbol, "/depth.do?symbol=$SYMBOL$");
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JArray ask in token.Item1["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = ask[1].ConvertInvariant<decimal>(), Price = ask[0].ConvertInvariant<decimal>() });
            }
            book.Asks.Sort((a1, a2) => a1.Price.CompareTo(a2.Price));
            foreach (JArray bid in token.Item1["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = bid[1].ConvertInvariant<decimal>(), Price = bid[0].ConvertInvariant<decimal>() });
            }
            return book;
        }

        protected override async Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            List<ExchangeTrade> allTrades = new List<ExchangeTrade>();
            var trades = await MakeRequestOkexAsync(symbol, "/trades.do?symbol=$SYMBOL$");
            foreach (JToken trade in trades.Item1)
            {
                // [ { "date": "1367130137", "date_ms": "1367130137000", "price": 787.71, "amount": 0.003, "tid": "230433", "type": "sell" } ]
                allTrades.Add(new ExchangeTrade
                {
                    Amount = trade["amount"].ConvertInvariant<decimal>(),
                    Price = trade["price"].ConvertInvariant<decimal>(),
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(trade["date_ms"].ConvertInvariant<long>()),
                    Id = trade["tid"].ConvertInvariant<long>(),
                    IsBuy = trade["type"].ToStringInvariant() == "buy"
                });
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
            symbol = NormalizeSymbol(symbol);
            string url = "/kline.do?symbol=" + symbol;
            if (startDate != null)
            {
                url += "&since=" + (long)startDate.Value.UnixTimestampFromDateTimeMilliseconds();
            }
            if (limit != null)
            {
                url += "&size=" + (limit.Value.ToStringInvariant());
            }
            string periodString = CryptoUtility.SecondsToPeriodStringLong(periodSeconds);
            url += "&type=" + periodString;
            JToken obj = await MakeJsonRequestAsync<JToken>(url);
            CheckError(obj);
            foreach (JArray array in obj)
            {
                candles.Add(new MarketCandle
                {
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(array[0].ConvertInvariant<long>()),
                    OpenPrice = array[1].ConvertInvariant<decimal>(),
                    HighPrice = array[2].ConvertInvariant<decimal>(),
                    LowPrice = array[3].ConvertInvariant<decimal>(),
                    ClosePrice = array[4].ConvertInvariant<decimal>(),
                    BaseVolume = array[5].ConvertInvariant<double>(),
                    ExchangeName = Name,
                    Name = symbol,
                    PeriodSeconds = periodSeconds,
                });
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
            var payload = GetNoncePayload();
            JToken token = await MakeJsonRequestAsync<JToken>("/userinfo.do", BaseUrl, payload, "POST");
            CheckError(token);
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
            var payload = GetNoncePayload();
            JToken token = await MakeJsonRequestAsync<JToken>("/userinfo.do", BaseUrl, payload, "POST");
            CheckError(token);
            var funds = token["info"]["funds"];
            var free = funds["free"];

            return ParseAmounts(funds["free"], amounts);
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);
            Dictionary<string, object> payload = GetNoncePayload();
            payload["symbol"] = symbol;
            payload["type"] = (order.IsBuy ? "buy" : "sell");

            // Okex has strict rules on which prices and quantities are allowed. They have to match the rules defined in the market definition.
            decimal outputQuantity = ClampOrderQuantity(symbol, order.Amount);
            decimal outputPrice = ClampOrderPrice(symbol, order.Price);

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

            JToken obj = await MakeJsonRequestAsync<JToken>("/trade.do", BaseUrl, payload, "POST");
            CheckError(obj);

            order.Amount = outputQuantity;
            order.Price = outputPrice;
            return ParsePlaceOrder(obj, order);
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            Dictionary<string, object> payload = GetNoncePayload();
            if (string.IsNullOrEmpty(symbol))
            {
                throw new InvalidOperationException("Okex cancel order request requires symbol");
            }
            payload["symbol"] = NormalizeSymbol(symbol);
            payload["order_id"] = orderId;
            JObject result = await MakeJsonRequestAsync<JObject>("/cancel_order.do", BaseUrl, payload, "POST");
            CheckError(result);
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = GetNoncePayload();
            if (string.IsNullOrEmpty(symbol))
            {
                throw new InvalidOperationException("Okex single order details request requires symbol");
            }
            payload["symbol"] = NormalizeSymbol(symbol);
            payload["order_id"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/order_info.do", BaseUrl, payload, "POST");
            CheckError(token);
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
            Dictionary<string, object> payload = GetNoncePayload();

            payload["symbol"] = symbol;
            // if order_id is -1, then return all unfilled orders, otherwise return the order specified
            payload["order_id"] = -1;
            JToken token = await MakeJsonRequestAsync<JToken>("/order_info.do", BaseUrl, payload, "POST");
            CheckError(token);
            foreach (JToken order in token["orders"])
            {
                orders.Add(ParseOrder(order));
            }

            return orders;
        }
        #endregion

        #region Private Functions
        private void CheckError(JToken result)
        {
            if (result != null && !(result is JArray) && result["error_code"] != null)
            {
                // throw new APIException(result["error_code"].ToStringInvariant() + ", see https://www.okex.com/rest_request.html error codes");
                throw new APIException(result["error_code"].ToStringInvariant() + ", see https://github.com/okcoin-okex/API-docs-OKEx.com/tree/master/API-For-Spot-CN error codes");
            }
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data)
        {
            //{"date":"1518043621","ticker":{"high":"0.01878000","vol":"1911074.97335534","last":"0.01817627","low":"0.01813515","buy":"0.01817626","sell":"0.01823447"}}

            JToken ticker = data["ticker"];
            decimal last = ticker["last"].ConvertInvariant<decimal>();
            decimal vol = ticker["vol"].ConvertInvariant<decimal>();
            return new ExchangeTicker
            {
                Ask = ticker["sell"].ConvertInvariant<decimal>(),
                Bid = ticker["buy"].ConvertInvariant<decimal>(),
                Last = last,
                Volume = new ExchangeVolume
                {
                    BaseVolume = vol,
                    BaseSymbol = symbol,
                    ConvertedVolume = vol * last,
                    ConvertedSymbol = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(data["date"].ConvertInvariant<long>())
                }
            };
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
        #endregion
    }
}
