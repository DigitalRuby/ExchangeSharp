/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed class ExchangeBitstampAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.bitstamp.net/api/v2";
        public override string Name => ExchangeName.Bitstamp;

        /// <summary>
        /// Bitstamp private API requires a customer id. Internally this is secured in the PassPhrase property.
        /// </summary>
        public string CustomerId
        {
            get { return Passphrase.ToUnsecureString(); }
            set { Passphrase = value.ToSecureString(); }
        }

        /// <summary>
        /// In order to use private functions of the API, you must set CustomerId by calling constructor with parameter,
        /// or setting it later in the ExchangeBitstampAPI object.
        /// </summary>
        public ExchangeBitstampAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMilliseconds;
        }

        /// <summary>
        /// In order to use private functions of the API, you must set CustomerId by calling this constructor with parameter,
        /// or setting it later in the ExchangeBitstampAPI object.
        /// </summary>
        /// <param name="customerId">Customer Id can be found by the link "https://www.bitstamp.net/account/balance/"</param>
        public ExchangeBitstampAPI(string customerId) : this()
        {
            CustomerId = customerId;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                if (string.IsNullOrWhiteSpace(CustomerId))
                {
                    throw new APIException("Customer ID is not set for Bitstamp");
                }

                // messageToSign = nonce + customer_id + api_key
                string apiKey = PublicApiKey.ToUnsecureString();
                string messageToSign = payload["nonce"].ToStringInvariant() + CustomerId + apiKey;
                string signature = CryptoUtility.SHA256Sign(messageToSign, PrivateApiKey.ToUnsecureString()).ToUpperInvariant();
                payload["signature"] = signature;
                payload["key"] = apiKey;
                WritePayloadToRequest(request, payload);
            }
        }

        private JToken CheckError(JToken token)
        {
            if (token == null)
            {
                throw new APIException("Null result");
            }
            if (token is JObject && token["status"] != null && token["status"].ToStringInvariant().Equals("error"))
            {
                throw new APIException(token["reason"].ToStringInvariant());
            }
            if (token is JObject && token["error"] != null)
            {
                throw new APIException(token["error"].ToStringInvariant());
            }
            return token;
        }

        private async Task<JToken> MakeBitstampRequestAsync(string subUrl)
        {
            JToken token = await MakeJsonRequestAsync<JToken>(subUrl);
            if (!(token is JArray) && token["error"] != null)
            {
                throw new APIException(token["error"].ToStringInvariant());
            }
            return token;
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace("/", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            foreach (JToken token in (await MakeBitstampRequestAsync("/trading-pairs-info")))
            {
                symbols.Add(token["url_symbol"].ToStringInvariant());
            }
            return symbols;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            // {"high": "0.10948945", "last": "0.10121817", "timestamp": "1513387486", "bid": "0.10112165", "vwap": "0.09958913", "volume": "9954.37332614", "low": "0.09100000", "ask": "0.10198408", "open": "0.10250028"}
            symbol = NormalizeSymbol(symbol);
            JToken token = await MakeBitstampRequestAsync("/ticker/" + symbol);
            return new ExchangeTicker
            {
                Ask = token["ask"].ConvertInvariant<decimal>(),
                Bid = token["bid"].ConvertInvariant<decimal>(),
                Last = token["last"].ConvertInvariant<decimal>(),
                Volume = new ExchangeVolume
                {
                    BaseVolume = token["volume"].ConvertInvariant<decimal>(),
                    BaseSymbol = symbol,
                    ConvertedVolume = token["volume"].ConvertInvariant<decimal>() * token["last"].ConvertInvariant<decimal>(),
                    ConvertedSymbol = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(token["timestamp"].ConvertInvariant<long>())
                }
            };
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JToken token = await MakeBitstampRequestAsync("/order_book/" + symbol);
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JArray ask in token["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = ask[1].ConvertInvariant<decimal>(), Price = ask[0].ConvertInvariant<decimal>() });
            }
            foreach (JArray bid in token["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = bid[1].ConvertInvariant<decimal>(), Price = bid[0].ConvertInvariant<decimal>() });
            }
            return book;
        }

        protected override async Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            // [{"date": "1513387997", "tid": "33734815", "price": "0.01724547", "type": "1", "amount": "5.56481714"}]
            symbol = NormalizeSymbol(symbol);
            JToken token = await MakeBitstampRequestAsync("/transactions/" + symbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            foreach (JToken trade in token)
            {
                trades.Add(new ExchangeTrade
                {
                    Amount = trade["amount"].ConvertInvariant<decimal>(),
                    Id = trade["tid"].ConvertInvariant<long>(),
                    IsBuy = trade["type"].ToStringInvariant() == "0",
                    Price = trade["price"].ConvertInvariant<decimal>(),
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(trade["date"].ConvertInvariant<long>())
                });
            }
            callback(trades);
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            string url = "/balance/";
            var payload = GetNoncePayload();
            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            CheckError(responseObject);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
            foreach (var property in responseObject)
            {
                if (property.Key.Contains("_balance"))
                {
                    decimal balance = property.Value.ConvertInvariant<decimal>();
                    if (balance == 0) { continue; }
                    balances.Add(property.Key.Replace("_balance", "").Trim(), balance);
                }
            }
            return balances;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            string url = "/balance/";
            var payload = GetNoncePayload();
            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            CheckError(responseObject);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
            foreach (var property in responseObject)
            {
                if (property.Key.Contains("_available"))
                {
                    decimal balance = property.Value.ConvertInvariant<decimal>();
                    if (balance == 0) { continue; }
                    balances.Add(property.Key.Replace("_available", "").Trim(), balance);
                }
            }
            return balances;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);

            string action = order.IsBuy ? "buy" : "sell";
            string market = order.OrderType == OrderType.Market ? "/market" : "";
            string url = $"/{action}{market}/{symbol}/";
            Dictionary<string, object> payload = GetNoncePayload();

            if (order.OrderType != OrderType.Market)
            {
                payload["price"] = order.Price.ToStringInvariant();
            }

            payload["amount"] = order.Amount.ToStringInvariant();
            foreach (var kv in order.ExtraParameters)
            {
                payload[kv.Key] = kv.Value;
            }

            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            CheckError(responseObject);
            return new ExchangeOrderResult
            {
                OrderDate = DateTime.UtcNow,
                OrderId = responseObject["id"].ToStringInvariant(),
                IsBuy = order.IsBuy,
                Symbol = order.Symbol
            };
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            //{
            //    "status": "Finished",
            //    "id": 1022694747,
            //    "transactions": [
            //    {
            //        "fee": "0.000002",
            //        "bch": "0.00882714",
            //        "price": "0.12120000",
            //        "datetime": "2018-02-24 14:15:29.133824",
            //        "btc": "0.0010698493680000",
            //        "tid": 56293144,
            //        "type": 2
            //    }]
            //}
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }
            string url = "/order_status/";
            Dictionary<string, object> payload = GetNoncePayload();
            payload["id"] = orderId;
            JObject result = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            CheckError(result);

            // status can be 'In Queue', 'Open' or 'Finished'
            JArray transactions = result["transactions"] as JArray;
            // empty transaction array means that order is InQueue or Open and AmountFilled == 0
            // return empty order in this case. no any additional info available at this point
            if (!transactions.Any()) { return new ExchangeOrderResult() { OrderId = orderId }; }
            JObject first = transactions.First() as JObject;
            List<string> excludeStrings = new List<string>() { "tid", "price", "fee", "datetime", "type", "btc", "usd", "eur" };

            string baseCurrency;
            string marketCurrency = first.Properties().FirstOrDefault(p => !excludeStrings.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.Name;
            if (string.IsNullOrWhiteSpace(marketCurrency))
            {
                // the only 2 cases are BTC-USD and BTC-EUR
                marketCurrency = "btc";
                excludeStrings.RemoveAll(s => s.Equals("usd") || s.Equals("eur"));
                baseCurrency = first.Properties().FirstOrDefault(p => !excludeStrings.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.Name;
            }
            else
            {
                excludeStrings.RemoveAll(s => s.Equals("usd") || s.Equals("eur") || s.Equals("btc"));
                excludeStrings.Add(marketCurrency);
                baseCurrency = first.Properties().FirstOrDefault(p => !excludeStrings.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.Name;
            }
            string _symbol = $"{marketCurrency}-{baseCurrency}";

            decimal amountFilled = 0, spentBaseCurrency = 0, price = 0;

            foreach (var t in transactions)
            {
                int type = t["type"].ConvertInvariant<int>();
                if (type != 2) { continue; }
                spentBaseCurrency += t[baseCurrency].ConvertInvariant<decimal>();
                amountFilled += t[marketCurrency].ConvertInvariant<decimal>();
                //set price only one time
                if (price == 0)
                {
                    price = t["price"].ConvertInvariant<decimal>();
                }
            }

            // No way to know if order IsBuy, Amount, OrderDate
            return new ExchangeOrderResult()
            {
                AmountFilled = amountFilled,
                Symbol = _symbol,
                AveragePrice = spentBaseCurrency / amountFilled,
                Price = price,
            };
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            symbol = NormalizeSymbol(symbol);
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols 
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/open_orders/all/" : "/open_orders/" + symbol;
            string url = "/open_orders/all/";
            JArray result = await MakeJsonRequestAsync<JArray>(url, null, GetNoncePayload(), "POST");
            CheckError(result);
            foreach (JToken token in result)
            {
                //This request doesn't give info about amount filled, use GetOrderDetails(orderId)
                string tokenSymbol = token["currency_pair"].ToStringLowerInvariant().Replace("/", "");
                if (!string.IsNullOrWhiteSpace(tokenSymbol) && !string.IsNullOrWhiteSpace(symbol) && !tokenSymbol.Equals(symbol, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                orders.Add(new ExchangeOrderResult()
                {
                    OrderId = token["id"].ToStringInvariant(),
                    OrderDate = token["datetime"].ConvertInvariant<DateTime>(),
                    IsBuy = token["type"].ConvertInvariant<int>() == 0,
                    Price = token["price"].ConvertInvariant<decimal>(),
                    Amount = token["amount"].ConvertInvariant<decimal>(),
                    Symbol = tokenSymbol ?? symbol
                });
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            symbol = NormalizeSymbol(symbol);
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/user_transactions/" : "/user_transactions/" + symbol;
            string url = "/user_transactions/";
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, GetNoncePayload(), "POST");
            CheckError(result);
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            foreach (var transaction in result as JArray)
            {
                int type = transaction["type"].ConvertInvariant<int>();
                // only type 2 is order transaction type, so we discard all other transactions
                if (type != 2) { continue; }

                string tradingPair = ((JObject)transaction).Properties().FirstOrDefault(p =>
                    !p.Name.Equals("order_id", StringComparison.InvariantCultureIgnoreCase)
                    && p.Name.Contains("_"))?.Name.Replace("_", "-");
                if (!string.IsNullOrWhiteSpace(tradingPair) && !string.IsNullOrWhiteSpace(symbol) && !NormalizeSymbol(tradingPair).Equals(symbol))
                {
                    continue;
                }
                string marketCurrency, baseCurrency;
                baseCurrency = tradingPair.Trim().Substring(tradingPair.Length - 3).ToLowerInvariant();
                marketCurrency = tradingPair.Trim().ToLowerInvariant().Replace(baseCurrency, "").Replace("-", "").Replace("_", "");

                decimal resultMarketCurrency = transaction[marketCurrency].ConvertInvariant<decimal>();
                ExchangeOrderResult order = new ExchangeOrderResult()
                {
                    OrderId = transaction["order_id"].ToStringInvariant(),
                    IsBuy = resultMarketCurrency > 0,
                    Symbol = NormalizeSymbol(tradingPair),
                    OrderDate = transaction["datetime"].ConvertInvariant<DateTime>(),
                    AmountFilled = Math.Abs(resultMarketCurrency),
                    AveragePrice = Math.Abs(transaction[baseCurrency].ConvertInvariant<decimal>() / resultMarketCurrency)
                };
                orders.Add(order);
            }
            // at this point one transaction transformed into one order, we need to consolidate parts into order
            // group by order id  
            var groupings = orders.GroupBy(o => o.OrderId);
            foreach (var group in groupings)
            {
                decimal spentBaseCurrency = group.Sum(o => o.AveragePrice * o.AmountFilled);
                ExchangeOrderResult order = group.First();
                order.AmountFilled = group.Sum(o => o.AmountFilled);
                order.AveragePrice = spentBaseCurrency / order.AmountFilled;
                order.Price = order.AveragePrice;
                orders.Add(order);
            }

            return orders;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new APIException("OrderId is needed for canceling order");
            }
            Dictionary<string, object> payload = GetNoncePayload();
            payload["id"] = orderId;
            JToken obj = await MakeJsonRequestAsync<JToken>("/cancel_order/", null, payload, "POST");
            CheckError(obj);
        }

        /// <summary>
        /// Function to withdraw from Bitsamp exchange. At the moment only XRP is supported.
        /// </summary>
        /// <param name="withdrawalRequest"></param>
        /// <returns></returns>
        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            string baseurl = null;
            string url;
            switch (withdrawalRequest.Symbol)
            {
                case "BTC":
                    // use old API for Bitcoin withdraw
                    baseurl = "https://www.bitstamp.net/api/";
                    url = "/bitcoin_withdrawal/";
                    break;
                default:
                    // this will work for some currencies and fail for others, caller must be aware of the supported currencies
                    url = "/" + withdrawalRequest.Symbol.ToLowerInvariant() + "_withdrawal/";
                    break;
            }

            Dictionary<string, object> payload = GetNoncePayload();
            payload["address"] = withdrawalRequest.Address.ToStringInvariant();
            payload["amount"] = withdrawalRequest.Amount.ToStringInvariant();
            payload["destination_tag"] = withdrawalRequest.AddressTag.ToStringInvariant();

            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, baseurl, payload, "POST");
            CheckError(responseObject);
            return new ExchangeWithdrawalResponse
            {
                Id = responseObject["id"].ToStringInvariant(),
                Message = responseObject["message"].ToStringInvariant(),
                Success = responseObject["success"].ConvertInvariant<bool>()
            };
        }
    }
}
