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
    public sealed partial class ExchangeBitstampAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.bitstamp.net/api/v2";

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
            SymbolIsUppercase = false;
            SymbolSeparator = string.Empty;
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

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
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
                await CryptoUtility.WritePayloadFormToRequestAsync(request, payload);
            }
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
            JToken token = await MakeBitstampRequestAsync("/ticker/" + symbol);
            return this.ParseTicker(token, symbol, "ask", "bid", "last", "volume", null, "timestamp", TimestampType.UnixSeconds);
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            JToken token = await MakeBitstampRequestAsync("/order_book/" + symbol);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            // [{"date": "1513387997", "tid": "33734815", "price": "0.01724547", "type": "1", "amount": "5.56481714"}]
            JToken token = await MakeBitstampRequestAsync("/transactions/" + symbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            foreach (JToken trade in token)
            {
                trades.Add(trade.ParseTrade("amount", "price", "type", "date", TimestampType.UnixSeconds, "tid", "0"));
            }
            callback(trades);
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            string url = "/balance/";
            var payload = await GetNoncePayloadAsync();
            var responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return ExtractDictionary(responseObject, "balance");
        }


        protected override async Task<Dictionary<string, decimal>> OnGetFeesAsync()
        {
            string url = "/balance/";
            var payload = await GetNoncePayloadAsync();
            var responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return ExtractDictionary(responseObject, "fee");
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            string url = "/balance/";
            var payload = await GetNoncePayloadAsync();
            var responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
            return ExtractDictionary(responseObject, "available");
        }

        private static Dictionary<string, decimal> ExtractDictionary(JObject responseObject, string key)
        {
            var result = new Dictionary<string, decimal>();
            var suffix = $"_{key}";
            foreach (var property in responseObject)
            {
                if (property.Key.Contains(suffix))
                {
                    decimal value = property.Value.ConvertInvariant<decimal>();
                    if (value == 0)
                    {
                        continue;
                    }

                    result.Add(property.Key.Replace(suffix, "").Trim(), value);
                }
            }
            return result;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string action = order.IsBuy ? "buy" : "sell";
            string market = order.OrderType == OrderType.Market ? "/market" : "";
            string url = $"/{action}{market}/{order.Symbol}/";
            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            if (order.OrderType != OrderType.Market)
            {
                payload["price"] = order.Price.ToStringInvariant();
            }

            payload["amount"] = order.Amount.ToStringInvariant();
            foreach (var kv in order.ExtraParameters)
            {
                payload["price"] = order.Price.ToStringInvariant();
            }

            payload["amount"] = order.RoundAmount().ToStringInvariant();
            order.ExtraParameters.CopyTo(payload);

            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");
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
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["id"] = orderId;
            JObject result = await MakeJsonRequestAsync<JObject>(url, null, payload, "POST");

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
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols 
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/open_orders/all/" : "/open_orders/" + symbol;
            string url = "/open_orders/all/";
            JArray result = await MakeJsonRequestAsync<JArray>(url, null, await GetNoncePayloadAsync(), "POST");
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
                    OrderDate = token["datetime"].ToDateTimeInvariant(),
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
            // TODO: Bitstamp bug: bad request if url contains symbol, so temporarily using url for all symbols
            // string url = string.IsNullOrWhiteSpace(symbol) ? "/user_transactions/" : "/user_transactions/" + symbol;
            string url = "/user_transactions/";
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync(), "POST");
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
                    OrderDate = transaction["datetime"].ToDateTimeInvariant(),
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
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["id"] = orderId;
            await MakeJsonRequestAsync<JToken>("/cancel_order/", null, payload, "POST");
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
            switch (withdrawalRequest.Currency)
            {
                case "BTC":
                    // use old API for Bitcoin withdraw
                    baseurl = "https://www.bitstamp.net/api/";
                    url = "/bitcoin_withdrawal/";
                    break;
                default:
                    // this will work for some currencies and fail for others, caller must be aware of the supported currencies
                    url = "/" + withdrawalRequest.Currency.ToLowerInvariant() + "_withdrawal/";
                    break;
            }

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["address"] = withdrawalRequest.Address.ToStringInvariant();
            payload["amount"] = withdrawalRequest.Amount.ToStringInvariant();
            payload["destination_tag"] = withdrawalRequest.AddressTag.ToStringInvariant();

            JObject responseObject = await MakeJsonRequestAsync<JObject>(url, baseurl, payload, "POST");
            CheckJsonResponse(responseObject);
            return new ExchangeWithdrawalResponse
            {
                Id = responseObject["id"].ToStringInvariant(),
                Message = responseObject["message"].ToStringInvariant(),
                Success = responseObject["success"].ConvertInvariant<bool>()
            };
        }
    }

    public partial class ExchangeName { public const string Bitstamp = "Bitstamp"; }
}
