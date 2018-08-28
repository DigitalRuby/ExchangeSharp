/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography;

namespace ExchangeSharp
{
    public sealed partial class ExchangeHuobiAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.huobipro.com";
        public string BaseUrlV1 { get; set; } = "https://api.huobipro.com/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.huobipro.com/ws";
        public string PrivateUrlV1 { get; set; } = "https://api.huobipro.com/v1";

        public bool IsMargin { get; set; }
        public string SubType { get; set; }

        private long webSocketId = 0;

        public ExchangeHuobiAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMilliseconds;
            SymbolSeparator = string.Empty;
            SymbolIsUppercase = false;
        }

        public override string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            if (symbol.Length < 6)
            {
                throw new ArgumentException("Invalid symbol " + symbol);
            }
            else if (symbol.Length == 6)
            {
                return ExchangeSymbolToGlobalSymbolWithSeparator(symbol.Substring(0, 3) + GlobalSymbolSeparator + symbol.Substring(3, 3), GlobalSymbolSeparator);
            }
            return ExchangeSymbolToGlobalSymbolWithSeparator(symbol.Substring(3) + GlobalSymbolSeparator + symbol.Substring(0, 3), GlobalSymbolSeparator);
        }

        public override string PeriodSecondsToString(int seconds)
        {
            return CryptoUtility.SecondsToPeriodStringLong(seconds);
        }

        #region ProcessRequest 

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                if (request.Method == "POST")
                {
                    request.AddHeader("content-type", "application/json");
                    payload.Remove("nonce");
                    var msg = CryptoUtility.GetJsonForPayload(payload);
                    await CryptoUtility.WriteToRequestAsync(request, msg);
                }
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // must sort case sensitive
                var dict = new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["Timestamp"] = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(payload["nonce"].ConvertInvariant<long>()).ToString("s"),
                    ["AccessKeyId"] = PublicApiKey.ToUnsecureString(),
                    ["SignatureMethod"] = "HmacSHA256",
                    ["SignatureVersion"] = "2"
                };

                if (method == "GET")
                {
                    foreach (var kv in payload)
                    {
                        dict.Add(kv.Key, kv.Value);
                    }
                }

                string msg = CryptoUtility.GetFormForPayload(dict, false, false, false);
                string toSign = $"{method}\n{url.Host}\n{url.Path}\n{msg}";

                // calculate signature
                var sign = CryptoUtility.SHA256SignBase64(toSign, PrivateApiKey.ToBytesUTF8()).UrlEncode();

                // append signature to end of message
                msg += $"&Signature={sign}";

                url.Query = msg;
            }
            return url.Uri;
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
             {
                "status"
            :
                "ok", "data"
            :
                [{
                    "base-currency": "btc",
                    "quote-currency": "usdt",
                    "price-precision": 2,
                    "amount-precision": 4,
                    "symbol-partition": "main"
                }, {
                    "base-currency": "bch",
                    "quote-currency": "usdt",
                    "price-precision": 2,
                    "amount-precision": 4,
                    "symbol-partition": "main"
                }, 
             
             */
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken allSymbols = await MakeJsonRequestAsync<JToken>("/common/symbols", BaseUrlV1, null);
            foreach (var symbol in allSymbols)
            {
                var marketCurrency = symbol["base-currency"].ToStringLowerInvariant();
                var baseCurrency = symbol["quote-currency"].ToStringLowerInvariant();
                var price_precision = symbol["price-precision"].ConvertInvariant<double>();
                var priceStepSize = Math.Pow(10, -price_precision);
                var amount_precision = symbol["amount-precision"].ConvertInvariant<double>();
                var quantityStepSize = Math.Pow(10, -amount_precision);

                var market = new ExchangeMarket()
                {
                    MarketCurrency = marketCurrency,
                    BaseCurrency = baseCurrency,
                    MarketName = marketCurrency + baseCurrency,
                    IsActive = true,
                };

                market.PriceStepSize = priceStepSize.ConvertInvariant<decimal>();
                market.QuantityStepSize = quantityStepSize.ConvertInvariant<decimal>();
                market.MinPrice = market.PriceStepSize.Value;
                market.MinTradeSize = market.QuantityStepSize.Value;

                markets.Add(market);
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            /*
             {{
              "status": "ok",
              "ch": "market.naseth.detail.merged",
              "ts": 1525136582460,
              "tick": {
                "amount": 1614089.3164448638,
                "open": 0.014552,
                "close": 0.013308,
                "high": 0.015145,
                "id": 6442118070,
                "count": 74643,
                "low": 0.013297,
                "version": 6442118070,
                "ask": [
                  0.013324,
                  0.0016
                ],
                "vol": 22839.223396720725,
                "bid": [
                  0.013297,
                  3192.2322
                ]
              }
            }}
             */
            JToken ticker = await MakeJsonRequestAsync<JToken>("/market/detail/merged?symbol=" + symbol);
            return this.ParseTicker(ticker["tick"], symbol, "ask", "bid", "close", "amount", "vol", "ts", TimestampType.UnixMillisecondsDouble, idKey: "id");
        }

        protected override Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            throw new NotImplementedException("Too many pairs and this exchange does not support a single call to get all the tickers");
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols)
        {
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
            {
                /*
{"id":"id1","status":"ok","subbed":"market.btcusdt.trade.detail","ts":1527574853489}


{{
  "ch": "market.btcusdt.trade.detail",
  "ts": 1527574905759,
  "tick": {
    "id": 8232977476,
    "ts": 1527574905623,
    "data": [
      {
        "amount": 0.3066,
        "ts": 1527574905623,
        "id": 82329774765058180723,
        "price": 7101.81,
        "direction": "buy"
      }
    ]
  }
}}
                 */
                var str = msg.ToStringFromUTF8Gzip();
                JToken token = JToken.Parse(str);

                if (token["status"] != null)
                {
                    return;
                }
                else if (token["ping"] != null)
                {
                    await _socket.SendMessageAsync(str.Replace("ping", "pong"));
                    return;
                }

                var ch = token["ch"].ToStringInvariant();
                var sArray = ch.Split('.');
                var symbol = sArray[1];

                var tick = token["tick"];
                var id = tick["id"].ConvertInvariant<long>();

                var data = tick["data"];
                var trades = ParseTradesWebSocket(data);
                foreach (var trade in trades)
                {
                    trade.Id = id;
                    callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
                }
            }, async (_socket) =>
            {
                if (symbols == null || symbols.Length == 0)
                {
                    symbols = (await GetSymbolsAsync()).ToArray();
                }
                foreach (string symbol in symbols)
                {
                    long id = System.Threading.Interlocked.Increment(ref webSocketId);
                    string channel = $"market.{symbol}.trade.detail";
                    await _socket.SendMessageAsync(new { sub = channel, id = "id" + id.ToStringInvariant() });
                }
            });
        }

        protected override IWebSocket OnGetOrderBookDeltasWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols)
        {
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
            {
                /*
{{
  "id": "id1",
  "status": "ok",
  "subbed": "market.btcusdt.depth.step0",
  "ts": 1526749164133
}}


{{
  "ch": "market.btcusdt.depth.step0",
  "ts": 1526749254037,
  "tick": {
    "bids": [
      [
        8268.3,
        0.101
      ],
      [
        8268.29,
        0.8248
      ],
      
    ],
    "asks": [
      [
        8275.07,
        0.1961
      ],
	  
      [
        8337.1,
        0.5803
      ]
    ],
    "ts": 1526749254016,
    "version": 7664175145
  }
}}
                 */
                var str = msg.ToStringFromUTF8Gzip();
                JToken token = JToken.Parse(str);

                if (token["status"] != null)
                {
                    return;
                }
                else if (token["ping"] != null)
                {
                    await _socket.SendMessageAsync(str.Replace("ping", "pong"));
                    return;
                }
                var ch = token["ch"].ToStringInvariant();
                var sArray = ch.Split('.');
                var symbol = sArray[1].ToStringInvariant();
                ExchangeOrderBook book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token["tick"], maxCount: maxCount);
                book.Symbol = symbol;
                callback(book);
            }, async (_socket) =>
            {
                if (symbols == null || symbols.Length == 0)
                {
                    symbols = (await GetSymbolsAsync()).ToArray();
                }
                foreach (string symbol in symbols)
                {
                    long id = System.Threading.Interlocked.Increment(ref webSocketId);
                    var normalizedSymbol = NormalizeSymbol(symbol);
                    string channel = $"market.{normalizedSymbol}.depth.step0";
                    await _socket.SendMessageAsync(new { sub = channel, id = "id" + id.ToStringInvariant() });
                }
            });
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            /*
             {
  "status": "ok",
  "ch": "market.btcusdt.depth.step0",
  "ts": 1489472598812,
  "tick": {
    "id": 1489464585407,
    "ts": 1489464585407,
    "bids": [
      [7964, 0.0678], // [price, amount]
      [7963, 0.9162],
      [7961, 0.1],
      [7960, 12.8898],
      [7958, 1.2],
      [7955, 2.1009],
      [7954, 0.4708],
      [7953, 0.0564],
      [7951, 2.8031],
      [7950, 13.7785],
      [7949, 0.125],
      [7948, 4],
      [7942, 0.4337],
      [7940, 6.1612],
      [7936, 0.02],
      [7935, 1.3575],
      [7933, 2.002],
      [7932, 1.3449],
      [7930, 10.2974],
      [7929, 3.2226]
    ],
    "asks": [
      [7979, 0.0736],
      [7980, 1.0292],
      [7981, 5.5652],
      [7986, 0.2416],
      [7990, 1.9970],
      [7995, 0.88],
             */
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken obj = await MakeJsonRequestAsync<JToken>("/market/depth?symbol=" + symbol + "&type=step0", BaseUrl, null);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(obj["tick"], sequence: "ts", maxCount: maxCount);
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            /*
            {
              "status": "ok",
              "ch": "market.btcusdt.kline.1day",
              "ts": 1499223904680,
              “data”: [
            {
                "id": 1499184000,
                "amount": 37593.0266,
                "count": 0,
                "open": 1935.2000,
                "close": 1879.0000,
                "low": 1856.0000,
                "high": 1940.0000,
                "vol": 71031537.97866500
              },
             */

            List<MarketCandle> candles = new List<MarketCandle>();
            string url = "/market/history/kline?symbol=" + symbol;
            if (limit != null)
            {
                // default is 150, max: 2000
                url += "&size=" + (limit.Value.ToStringInvariant());
            }
            string periodString = PeriodSecondsToString(periodSeconds);
            url += "&period=" + periodString;
            JToken allCandles = await MakeJsonRequestAsync<JToken>(url, BaseUrl, null);
            foreach (var token in allCandles)
            {
                candles.Add(this.ParseCandle(token, symbol, periodSeconds, "open", "high", "low", "close", "id", TimestampType.UnixSeconds, null, "vol"));
            }

            candles.Reverse();
            return candles;
        }

        #endregion

        #region Private APIs

        private async Task<Dictionary<string, string>> OnGetAccountsAsync()
        {
            /*
            {[
  {
    "id": 3274515,
    "type": "spot",
    "subtype": "",
    "state": "working"
  },
  {
    "id": 4267855,
    "type": "margin",
    "subtype": "btcusdt",
    "state": "working"
  },
  {
    "id": 3544747,
    "type": "margin",
    "subtype": "ethusdt",
    "state": "working"
  },
  {
    "id": 3274640,
    "type": "otc",
    "subtype": "",
    "state": "working"
  }
]}
 */
            Dictionary<string, string> accounts = new Dictionary<string, string>();
            var payload = await GetNoncePayloadAsync();
            JToken data = await MakeJsonRequestAsync<JToken>("/account/accounts", PrivateUrlV1, payload);
            foreach (var acc in data)
            {
                string key = acc["type"].ToStringInvariant() + "_" + acc["subtype"].ToStringInvariant();
                accounts.Add(key, acc["id"].ToStringInvariant());
            }
            return accounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            /*
             
  "status": "ok",
  "data": {
    "id": 3274515,
    "type": "spot",
    "state": "working",
    "list": [
      {
        "currency": "usdt",
        "type": "trade",
        "balance": "0.000045000000000000"
      },
      {
        "currency": "eth",
        "type": "frozen",
        "balance": "0.000000000000000000"
      },
      {
        "currency": "eth",
        "type": "trade",
        "balance": "0.044362165000000000"
      },
      {
        "currency": "eos",
        "type": "trade",
        "balance": "16.467000000000000000"
      },
             */
            var account_id = await GetAccountID();
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/account/accounts/{account_id}/balance", PrivateUrlV1, payload);
            var list = token["list"];
            foreach (var item in list)
            {
                var balance = item["balance"].ConvertInvariant<decimal>();
                if (balance == 0m)
                    continue;

                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            var account_id = await GetAccountID();

            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/account/accounts/{account_id}/balance", PrivateUrlV1, payload);
            var list = token["list"];
            foreach (var item in list)
            {
                var balance = item["balance"].ConvertInvariant<decimal>();
                if (balance == 0m)
                    continue;
                var type = item["type"].ToStringInvariant();
                if (type != "trade")
                    continue;

                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            /*
             {{
              "status": "ok",
              "data": {
                "id": 3908501445,
                "symbol": "naseth",
                "account-id": 3274515,
                "amount": "0.050000000000000000",
                "price": "0.000001000000000000",
                "created-at": 1525100546601,
                "type": "buy-limit",
                "field-amount": "0.0",
                "field-cash-amount": "0.0",
                "field-fees": "0.0",
                "finished-at": 1525100816771,
                "source": "api",
                "state": "canceled",
                "canceled-at": 1525100816399
              }
            }}
             */
            var payload = await GetNoncePayloadAsync();
            JToken data = await MakeJsonRequestAsync<JToken>($"/order/orders/{orderId}", PrivateUrlV1, payload);
            return ParseOrder(data);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            if (symbol == null) { throw new APIException("symbol cannot be null"); }

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = await GetNoncePayloadAsync();
            payload.Add("symbol", symbol);
            payload.Add("states", "partial-canceled,filled,canceled");
            if (afterDate != null)
            {
                payload.Add("start-date", afterDate.Value.ToString("yyyy-MM-dd"));
            }
            JToken data = await MakeJsonRequestAsync<JToken>("/order/orders", PrivateUrlV1, payload);
            foreach (var prop in data)
            {
                orders.Add(ParseOrder(prop));
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            if (symbol == null) { throw new APIException("symbol cannot be null"); }

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = await GetNoncePayloadAsync();
            payload.Add("symbol", symbol);
            payload.Add("states", "pre-submitted,submitting,submitted,partial-filled");
            JToken data = await MakeJsonRequestAsync<JToken>("/order/orders", PrivateUrlV1, payload);
            foreach (var prop in data)
            {
                orders.Add(ParseOrder(prop));
            }
            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var account_id = await GetAccountID(order.IsMargin, order.Symbol);

            var payload = await GetNoncePayloadAsync();
            payload.Add("account-id", account_id);
            payload.Add("symbol", order.Symbol);
            payload.Add("type", order.IsBuy ? "buy" : "sell");
            payload.Add("source", order.IsMargin ? "margin-api" : "api");

            decimal outputQuantity = await ClampOrderQuantity(order.Symbol, order.Amount);
            decimal outputPrice = await ClampOrderPrice(order.Symbol, order.Price);

            payload["amount"] = outputQuantity.ToStringInvariant();

            if (order.OrderType == OrderType.Market)
            {
                payload["type"] += "-market";
            }
            else
            {
                payload["type"] += "-limit";
                payload["price"] = outputPrice.ToStringInvariant();
            }

            order.ExtraParameters.CopyTo(payload);

            JToken obj = await MakeJsonRequestAsync<JToken>("/order/orders/place", PrivateUrlV1, payload, "POST");
            order.Amount = outputQuantity;
            order.Price = outputPrice;
            return ParsePlaceOrder(obj, order);
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            var payload = await GetNoncePayloadAsync();
            await MakeJsonRequestAsync<JToken>($"/order/orders/{orderId}/submitcancel", PrivateUrlV1, payload, "POST");
        }

        protected override Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            throw new NotImplementedException("Huobi does not provide a deposit API");
        }

        protected override Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            throw new NotImplementedException("Huobi does not provide a deposit API");

            /*
            var payload = await GetNoncePayloadAsync();
            payload.Add("need_new", forceRegenerate ? 1 : 0);
            payload.Add("method", "GetDepositAddress");
            payload.Add("coinName", symbol);
            payload["method"] = "POST";
            // "return":{"address": 1UHAnAWvxDB9XXETsi7z483zRRBmcUZxb3,"processed_amount": 1.00000000,"server_time": 1437146228 }
            JToken token = await MakeJsonRequestAsync<JToken>("/", PrivateUrlV1, payload, "POST");
            return new ExchangeDepositDetails
            {
                Address = token["address"].ToStringInvariant(),
                Symbol = symbol
            };
            */
        }

        protected override Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            throw new NotImplementedException("Huobi does not provide a withdraw API");
        }


#endregion

        #region Private Functions

        protected override JToken CheckJsonResponse(JToken result)
        {
            if (result == null || (result["status"] != null && result["status"].ToStringInvariant() != "ok"))
            {
                throw new APIException((result["err-msg"] != null ? result["err-msg"].ToStringInvariant() : "Unknown Error"));
            }
            return result["data"] ?? result;
        }

        private ExchangeOrderResult ParsePlaceOrder(JToken token, ExchangeOrderRequest order)
        {
            /*
              {
                  "status": "ok",
                  "data": "59378"
                }
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = order.Amount,
                Price = order.Price,
                IsBuy = order.IsBuy,
                OrderId = token.ToStringInvariant(),
                Symbol = order.Symbol
            };
            result.AveragePrice = result.Price;
            result.Result = ExchangeAPIOrderResult.Pending;

            return result;
        }

        private ExchangeAPIOrderResult ParseState(string state)
        {
            switch (state)
            {
                case "pre-submitted":
                case "submitting":
                case "submitted":
                    return ExchangeAPIOrderResult.Pending;
                case "partial-filled":
                    return ExchangeAPIOrderResult.FilledPartially;
                case "filled":
                    return ExchangeAPIOrderResult.Filled;
                case "partial-canceled":
                case "canceled":
                    return ExchangeAPIOrderResult.Canceled;
                default:
                    return ExchangeAPIOrderResult.Unknown;
            }
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            ExchangeOrderResult result = new ExchangeOrderResult()
            {
                OrderId = token["id"].ToStringInvariant(),
                Symbol = token["symbol"].ToStringInvariant(),
                Amount = token["amount"].ConvertInvariant<decimal>(),
                AmountFilled = token["field-amount"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["created-at"].ConvertInvariant<long>()),
                IsBuy = token["type"].ToStringInvariant().StartsWith("buy"),
                Result = ParseState(token["state"].ToStringInvariant()),
            };

            if (result.Price == 0 && result.AmountFilled != 0m)
            {
                var amountCash = token["field-cash-amount"].ConvertInvariant<decimal>();
                result.Price = amountCash / result.AmountFilled;
            }

            return result;
        }

        private IEnumerable<ExchangeTrade> ParseTradesWebSocket(JToken token)
        {
            var trades = new List<ExchangeTrade>();
            foreach (var t in token)
            {
                trades.Add(t.ParseTrade("amount", "price", "direction", "ts", TimestampType.UnixMilliseconds, "id"));
            }

            return trades;
        }

        private async Task<string> GetAccountID(bool isMargin = false, string subtype = "")
        {
            var accounts = await OnGetAccountsAsync();
            var key = "spot_";
            if (isMargin)
            {
                key = "margin_" + subtype;
            }
            var account_id = accounts[key];
            return account_id;
        }
        #endregion
    }

    public partial class ExchangeName { public const string Huobi = "Huobi"; }
}
