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

namespace ExchangeSharp
{
    public sealed class ExchangeHuobiAPI : ExchangeAPI
    {
        public override string Name => ExchangeName.Huobi;
        public override string BaseUrl { get; set; } = "https://api.huobipro.com";
        public string BaseUrlV1 { get; set; } = "https://api.huobipro.com/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.huobipro.com/ws";
        public string PrivateUrlV1 { get; set; } = "https://api.huobipro.com/v1";
        public string AccountType { get; set; } = "spot";

        public ExchangeHuobiAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixSecondsString;   // not used, see below
            SymbolSeparator = string.Empty;
            SymbolIsUppercase = false;
            SymbolIsReversed = true;
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

        #region ProcessRequest 

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                if (request.Method == "POST")
                {
                    request.ContentType = "application/json";

                    payload.Remove("nonce");
                    var msg = GetJsonForPayload(payload);
                    WriteFormToRequest(request, msg);
                }
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                if (!payload.ContainsKey("method"))
                {
                    return url.Uri;
                }
                string method = payload["method"].ToStringInvariant();
                payload.Remove("method");

                var dict = new Dictionary<string, object>
                {
                    ["Timestamp"] = DateTime.UtcNow.ToString("s"),
                    ["AccessKeyId"] = PublicApiKey.ToUnsecureString(),
                    ["SignatureMethod"] = "HmacSHA256",
                    ["SignatureVersion"] = "2"
                };

                string msg = null;
                if (method == "GET")
                {
                    dict = dict.Concat(payload).ToDictionary(x => x.Key, x => x.Value);
                }

                msg = GetFormForPayload(dict, false);

                // must sort case sensitive
                msg = string.Join("&", new SortedSet<string>(msg.Split('&'), StringComparer.Ordinal));

                StringBuilder sb = new StringBuilder();
                sb.Append(method).Append("\n")
                    .Append(url.Host).Append("\n")
                    .Append(url.Path).Append("\n")
                    .Append(msg);

                var sig = CryptoUtility.SHA256SignBase64(sb.ToString(), PrivateApiKey.SecureStringToBytes());
                msg += "&Signature=" + Uri.EscapeDataString(sig);

                url.Query = msg;
            }
            return url.Uri;
        }

        #endregion

        #region Public APIs

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace("-", "").Replace("/", "").Replace("_", "").ToLowerInvariant();
        }

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
            if (ReadCache("GetSymbolsMetadata", out List<ExchangeMarket> markets))
            {
                return markets;
            }

            markets = new List<ExchangeMarket>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/common/symbols", BaseUrlV1, null, "GET");
            CheckError(obj);
            JToken allSymbols = obj["data"];
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

            WriteCache("GetSymbolsMetadata", TimeSpan.FromMinutes(60.0), markets);

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
            symbol = NormalizeSymbol(symbol);
            JToken obj = await MakeJsonRequestAsync<JToken>("/market/detail/merged?symbol=" + symbol);
            CheckError(obj);
            var tick = obj["tick"];
            var ts = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(obj["ts"].ConvertInvariant<double>());

            return ParseTicker(symbol, ts, tick);
        }


        protected override Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            throw new NotImplementedException("Too many pairs and this exchange does not support a single call to get all the tickers");
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
            symbol = NormalizeSymbol(symbol);
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken obj = await MakeJsonRequestAsync<JToken>("/market/depth?symbol=" + symbol + "&type=step0", BaseUrl, null, "GET");
            CheckError(obj);
            var tick = obj["tick"];
            foreach (var prop in tick["asks"]) orders.Asks.Add(new ExchangeOrderPrice() { Price = prop[0].ConvertInvariant<decimal>(), Amount = prop[1].ConvertInvariant<decimal>() });
            foreach (var prop in tick["bids"]) orders.Bids.Add(new ExchangeOrderPrice() { Price = prop[0].ConvertInvariant<decimal>(), Amount = prop[1].ConvertInvariant<decimal>() });

            return orders;
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
            symbol = NormalizeSymbol(symbol);
            string url = "/market/history/kline?symbol=" + symbol;
            if (limit != null)
            {
                // default is 150, max: 2000
                url += "&size=" + (limit.Value.ToStringInvariant());
            }
            string periodString = CryptoUtility.SecondsToPeriodStringLong(periodSeconds);
            url += "&period=" + periodString;
            JToken obj = await MakeJsonRequestAsync<JToken>(url, BaseUrl, null, "GET");
            CheckError(obj);
            JToken allCandles = obj["data"];
            var ts = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(obj["ts"].ConvertInvariant<long>());
            foreach (var array in allCandles)
            {
                candles.Add(new MarketCandle
                {
                    ClosePrice = array["close"].ConvertInvariant<decimal>(),
                    ExchangeName = Name,
                    HighPrice = array["high"].ConvertInvariant<decimal>(),
                    LowPrice = array["low"].ConvertInvariant<decimal>(),
                    Name = symbol,
                    OpenPrice = array["open"].ConvertInvariant<decimal>(),
                    PeriodSeconds = periodSeconds,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(array["id"].ConvertInvariant<long>()),
                    BaseVolume = array["vol"].ConvertInvariant<double>() / array["close"].ConvertInvariant<double>(),
                    ConvertedVolume = array["vol"].ConvertInvariant<double>(),
                    WeightedAverage = 0m
                });
            }

            candles.Reverse();

            return candles;
        }

        #endregion

        #region Private APIs

        private async Task<Dictionary<string, string>> OnGetAccountsAsync()
        {
            if (ReadCache("GetAccounts", out Dictionary<string, string> accounts))
            {
                return accounts;
            }
            accounts = new Dictionary<string, string>();
            var payload = GetNoncePayload();
            payload["method"] = "GET";
            JToken token = await MakeJsonRequestAsync<JToken>("/account/accounts", PrivateUrlV1, payload, "GET");
            token = CheckError(token);
            var data = token["data"];
            foreach (var acc in data)
            {
                accounts.Add(acc["type"].ToStringInvariant(), acc["id"].ToStringInvariant());
            }

            WriteCache("GetAccounts", TimeSpan.FromMinutes(60.0), accounts);

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
            var accounts = await OnGetAccountsAsync();
            var account_id = accounts[AccountType];

            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = GetNoncePayload();
            payload["method"] = "GET";
            JToken token = await MakeJsonRequestAsync<JToken>($"/account/accounts/{account_id}/balance", PrivateUrlV1, payload, "GET");
            token = CheckError(token);
            var list = token["data"]["list"];
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
            var accounts = await OnGetAccountsAsync();
            var account_id = accounts[AccountType];

            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = GetNoncePayload();

            JToken token = await MakeJsonRequestAsync<JToken>($"/account/accounts/{account_id}/balance", PrivateUrlV1, payload, "GET");
            token = CheckError(token);
            var list = token["data"]["list"];
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
            var payload = GetNoncePayload();
            payload.Add("method", "GET");
            JToken token = await MakeJsonRequestAsync<JToken>($"/order/orders/{orderId}", PrivateUrlV1, payload, "GET");
            CheckError(token);
            var data = token["data"];
            return ParseOrder(data);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            if (symbol == null) { throw new APIException("symbol cannot be null"); }

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = GetNoncePayload();
            payload.Add("method", "GET");
            payload.Add("symbol", symbol);
            payload.Add("states", "partial-canceled,filled,canceled");
            if (afterDate != null)
            {
                payload.Add("start-date", afterDate.Value.ToString("yyyy-MM-dd"));
            }
            JToken token = await MakeJsonRequestAsync<JToken>("/order/orders", PrivateUrlV1, payload, "GET");
            CheckError(token);
            var data = token["data"];
            foreach (var prop in data)
                orders.Add(ParseOrder(prop));
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            if (symbol == null) { throw new APIException("symbol cannot be null"); }

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = GetNoncePayload();
            payload.Add("method", "GET");
            payload.Add("symbol", symbol);
            payload.Add("states", "pre-submitted,submitting,submitted,partial-filled");
            JToken token = await MakeJsonRequestAsync<JToken>("/order/orders", PrivateUrlV1, payload, "GET");
            CheckError(token);
            var data = token["data"];
            foreach (var prop in data)
                orders.Add(ParseOrder(prop));
            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);

            var accounts = await OnGetAccountsAsync();
            var account_id = accounts[AccountType];

            var payload = GetNoncePayload();
            payload.Add("account-id", account_id);
            payload.Add("symbol", symbol);
            payload.Add("type", order.IsBuy ? "buy" : "sell");
            payload.Add("source", "api"); // margin-api

            decimal outputQuantity = ClampOrderQuantity(symbol, order.Amount);
            decimal outputPrice = ClampOrderPrice(symbol, order.Price);

            if (order.OrderType == OrderType.Market)
            {
                payload["type"] += "-market";

                // TODO: Fix later once Okex fixes this on their end
                throw new NotSupportedException("Huobi confuses price with amount while sending a market order, so market orders are disabled for now");

                /*
                if (order.IsBuy)
                {
                    // for market buy orders, the price is to total amount you want to buy, 
                    // and it must be higher than the current price of 0.01 BTC (minimum buying unit), 0.1 LTC or 0.01 ETH
                    payload["amount"] = outputQuantity;
                }
                else
                {
                    // For market buy roders, the amount is not required
                    payload["amount"] = outputQuantity;
                }*/
            }
            else
            {
                payload["type"] += "-limit";
                payload["price"] = outputPrice.ToStringInvariant();
                payload["amount"] = outputQuantity.ToStringInvariant();
            }

            payload["method"] = "POST";
            JToken obj = await MakeJsonRequestAsync<JToken>("/order/orders/place", PrivateUrlV1, payload, "POST");
            CheckError(obj);

            order.Amount = outputQuantity;
            order.Price = outputPrice;
            return ParsePlaceOrder(obj, order);
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            var payload = GetNoncePayload();
            payload["method"] = "POST";
            JToken token = await MakeJsonRequestAsync<JToken>($"/order/orders/{orderId}/submitcancel", PrivateUrlV1, payload, "POST");
            CheckError(token);
        }

        protected override Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            throw new NotImplementedException("Huobi does not provide a deposit API");
        }

        protected override Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            throw new NotImplementedException("Huobi does not provide a deposit API");

            /*
            var payload = GetNoncePayload();
            payload.Add("need_new", forceRegenerate ? 1 : 0);
            payload.Add("method", "GetDepositAddress");
            payload.Add("coinName", symbol);
            // "return":{"address": 1UHAnAWvxDB9XXETsi7z483zRRBmcUZxb3,"processed_amount": 1.00000000,"server_time": 1437146228 }
            JToken token = await MakeJsonRequestAsync<JToken>("/", PrivateUrlV1, payload, "POST");
            token = CheckError(token);
            return new ExchangeDepositDetails()
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

        private JToken CheckError(JToken result)
        {
            if (result == null || (result["status"] != null && result["status"].Value<string>() != "ok"))
            {
                throw new APIException((result["err-msg"] != null ? result["err-msg"].ToStringInvariant() : "Unknown Error"));
            }
            return result;
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
                OrderId = token["data"].ToStringInvariant(),
                Symbol = order.Symbol
            };
            result.AveragePrice = result.Price;
            result.Result = ExchangeAPIOrderResult.Pending;

            return result;
        }

        private ExchangeTicker ParseTicker(string symbol, DateTime ts, JToken token)
        {
            return new ExchangeTicker
            {
                Ask = token["ask"].First.ConvertInvariant<decimal>(),
                Bid = token["bid"].First.ConvertInvariant<decimal>(),
                Last = token["close"].ConvertInvariant<decimal>(),
                Id = token["id"].ToStringInvariant(),
                Volume = new ExchangeVolume
                {
                    ConvertedVolume = token["vol"].ConvertInvariant<decimal>(),
                    BaseVolume = token["amount"].ConvertInvariant<decimal>(),
                    ConvertedSymbol = symbol,
                    BaseSymbol = symbol,
                    Timestamp = ts,
                }
            };
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

            return result;
        }

        #endregion
    }
}