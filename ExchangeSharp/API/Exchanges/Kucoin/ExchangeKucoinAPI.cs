/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeKucoinAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.kucoin.com/v1";

        public ExchangeKucoinAPI()
        {
            RequestContentType = "x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMillisecondsString;
            SymbolSeparator = "-";
        }

        public override string PeriodSecondsToString(int seconds)
        {
            switch (seconds)
            {
                case 60: return "1";
                case 300: return "5";
                case 900: return "15";
                case 1800: return "30";
                case 3600: return "60";
                case 86400: return "D";
                case 604800: return "W";
                default: throw new ArgumentException($"{nameof(seconds)} must be 60, 300, 900, 1800, 3600, 86400, 604800");
            }
        }

        #region ProcessRequest 

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.AddHeader("KC-API-KEY", PublicApiKey.ToUnsecureString());
                request.AddHeader("KC-API-NONCE", payload["nonce"].ToStringInvariant());

                var endpoint = request.RequestUri.AbsolutePath;
                var message = string.Format("{0}/{1}/{2}", endpoint, payload["nonce"], CryptoUtility.GetFormForPayload(payload, false));
                var sig = CryptoUtility.SHA256Sign(Convert.ToBase64String(message.ToBytesUTF8()), PrivateApiKey.ToUnsecureString());

                request.AddHeader("KC-API-SIGNATURE", sig);

                if (request.Method == "POST")
                {
                    string msg = CryptoUtility.GetFormForPayload(payload, false);
                    byte[] content = msg.ToBytesUTF8();
                    await request.WriteAllAsync(content, 0, content.Length);
                }
            }
        }

        protected override async Task OnGetNonceOffset()
        {
            try
            {
                JToken token = await MakeJsonRequestAsync<JToken>("/open/tick");
                NonceOffset = DateTime.UtcNow - CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["timestamp"].ConvertInvariant<long>());
            }
            catch
            {
            }
        }

        #endregion

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            List<string> symbols = new List<string>();
            // [ { "withdrawMinFee": 100000, "withdrawMinAmount": 200000, "withdrawFeeRate": 0.001, "confirmationCount": 12, "name": "Bitcoin", "tradePrecision": 7, "coin": "BTC","infoUrl": null, "enableWithdraw": true, "enableDeposit": true, "depositRemark": "", "withdrawRemark": ""  } ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/market/open/coins");
            foreach (JToken currency in token) currencies.Add(currency["coin"].ToStringInvariant(), new ExchangeCurrency()
            {
                Name = currency["coin"].ToStringInvariant(),
                FullName = currency["name"].ToStringInvariant(),
                WithdrawalEnabled = currency["enableWithdraw"].ConvertInvariant<bool>(),
                DepositEnabled = currency["enableDepost"].ConvertInvariant<bool>(),
                TxFee = currency["withdrawFeeRate"].ConvertInvariant<decimal>(),
                MinConfirmations = currency["confirmationCount"].ConvertInvariant<int>(),
            });
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            // [ { "coinType": "KCS", "trading": true, "lastDealPrice": 4500,"buy": 4120, "sell": 4500, "coinTypePair": "BTC", "sort": 0,"feeRate": 0.001,"volValue": 324866889, "high": 6890, "datetime": 1506051488000, "vol": 5363831663913, "low": 4500, "changeRate": -0.3431 }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/market/open/symbols");
            foreach (JToken symbol in token) symbols.Add(symbol["coinType"].ToStringInvariant() + "-" + symbol["coinTypePair"].ToStringInvariant());        // they don't put it together for ya...
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // [ { "coinType": "KCS", "trading": true, "lastDealPrice": 4500,"buy": 4120, "sell": 4500, "coinTypePair": "BTC", "sort": 0,"feeRate": 0.001,"volValue": 324866889, "high": 6890, "datetime": 1506051488000, "vol": 5363831663913, "low": 4500, "changeRate": -0.3431 }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/market/open/symbols");
            foreach (JToken symbol in token)
            {
                ExchangeMarket market = new ExchangeMarket()
                {
                    IsActive = symbol["trading"].ConvertInvariant<bool>(),
                    MarketCurrency = symbol["coinType"].ToStringInvariant(),
                    BaseCurrency = symbol["coinTypePair"].ToStringInvariant(),
                };
                market.MarketName = market.MarketCurrency + "-" + market.BaseCurrency;
                markets.Add(market);
            }
            return markets;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/open/orders?symbol=" + symbol + "&limit=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, asks: "SELL", bids: "BUY", maxCount: maxCount);
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            // { "coinType": "KCS","trading": true,"lastDealPrice": 5040,"buy": 5000,"sell": 5040, "coinTypePair": "BTC","sort": 0,"feeRate": 0.001,"volValue": 308140577,"high": 6890, "datetime": 1506050394000, "vol": 5028739175025, "low": 5040, "changeRate": -0.2642 }
            JToken token = await MakeJsonRequestAsync<JToken>("/" + symbol + "/open/tick");
            return this.ParseTicker(token, symbol);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            // [ { "coinType": "KCS", "trading": true, "lastDealPrice": 4500, "buy": 4120, "sell": 4500, "coinTypePair": "BTC", "sort": 0, "feeRate": 0.001, "volValue": 324866889, "high": 6890, "datetime": 1506051488000, "vol": 5363831663913, "low": 4500, "changeRate": -0.3431  }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/open/tick");
            foreach (JToken tick in token)
            {
                string symbol = tick["coinType"].ToStringInvariant() + "-" + tick["coinTypePair"].ToStringInvariant();
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ParseTicker(tick, symbol)));
            }
            return tickers;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // [0]-Timestamp [1]-OrderType [2]-Price [3]-Amount [4]-Volume
            // [[1506037604000,"SELL",5210,48600633397,2532093],... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/open/deal-orders?symbol=" + symbol);
            foreach (JToken trade in token)
            {
                trades.Add(trade.ParseTrade(3, 2, 1, 0, TimestampType.UnixMilliseconds));
            }
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            JToken token = await MakeJsonRequestAsync<JToken>("/open/deal-orders?symbol=" + symbol + (startDate == null ? string.Empty : "&since=" + startDate.Value.UnixTimestampFromDateTimeMilliseconds()));
            foreach (JArray trade in token)
            {
                trades.Add(trade.ParseTrade(3, 2, 1, 0, TimestampType.UnixMilliseconds));
            }
            var rc = callback?.Invoke(trades);
        }

        /// <summary>
        /// This is a private call on Kucoin and therefore requires an API Key + API Secret. Calling this without authorization will cause an exception
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            List<MarketCandle> candles = new List<MarketCandle>();

            string periodString = PeriodSecondsToString(periodSeconds);
            endDate = endDate ?? DateTime.UtcNow;
            startDate = startDate ?? DateTime.UtcNow.AddDays(-1);

            // this is a little tricky. The call is private, but not a POST. We need the payload for the sig, but also for the uri
            // so, we'll do both... This is the only ExchangeAPI public call (private on Kucoin) like this.
            var payload = await GetNoncePayloadAsync();
            payload.Add("symbol", symbol);
            payload.Add("resolution", periodString);
            payload.Add("from", (long)startDate.Value.UnixTimestampFromDateTimeSeconds());        // the nonce is milliseconds, this is seconds without decimal
            payload.Add("to", (long)endDate.Value.UnixTimestampFromDateTimeSeconds());            // the nonce is milliseconds, this is seconds without decimal  
            var addPayload = CryptoUtility.GetFormForPayload(payload, false);

            // The results of this Kucoin API call are also a mess. 6 different arrays (c,t,v,h,l,o) with the index of each shared for the candle values
            // It doesn't use their standard error format...
            JToken token = await MakeJsonRequestAsync<JToken>("/open/chart/history?" + addPayload, null, payload);
            if (token != null && token.HasValues && token["s"].ToStringInvariant() == "ok")
            {
                int childCount = token["c"].Count();
                for (int i = 0; i < childCount; i++)
                {
                    candles.Add(new MarketCandle
                    {
                        ExchangeName = this.Name,
                        Name = symbol,
                        PeriodSeconds = periodSeconds,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(token["t"][i].ConvertInvariant<long>()).DateTime,
                        ClosePrice = token["c"][i].ConvertInvariant<decimal>(),
                        HighPrice = token["h"][i].ConvertInvariant<decimal>(),
                        LowPrice = token["l"][i].ConvertInvariant<decimal>(),
                        OpenPrice = token["o"][i].ConvertInvariant<decimal>(),
                        ConvertedVolume = token["v"][i].ConvertInvariant<double>(),
                        BaseVolume = token["v"][i].ConvertInvariant<double>() * token["c"][i].ConvertInvariant<double>()
                    });
                }
            }
            return candles;
        }

        #endregion

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JToken token = await MakeJsonRequestAsync<JToken>("/account/balances", null, await GetNoncePayloadAsync());
            foreach (JToken child in token["datas"])
            {
                decimal amount = child["balance"].ConvertInvariant<decimal>() + child["freezeBalance"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts.Add(child["coinType"].ToStringInvariant(), amount);
                }
            }

            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/account/balances", null, await GetNoncePayloadAsync());
            foreach (JToken child in obj["datas"])
            {
                decimal amount = child["balance"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts.Add(child["coinType"].ToStringInvariant(), amount);
                }
            }
            return amounts;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            // "datas": [ {"createdAt": 1508219588000, "amount": 92.79323381, "dealValue": 0.00927932, "dealPrice": 0.0001, "fee": 1e-8,"feeRate": 0, "oid": "59e59ac49bd8d31d09f85fa8", "orderOid": "59e59ac39bd8d31d093d956a", "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "dealDirection": "BUY" }, ... ]
            var payload = await GetNoncePayloadAsync();
            if (symbol != null)
            {
                payload["symbol"] = symbol;
            }

            JToken token = await MakeJsonRequestAsync<JToken>("/order/dealt?" + CryptoUtility.GetFormForPayload(payload, false), null, payload);
            if (token != null && token.HasValues)
            {
                foreach (JToken order in token["datas"]) orders.Add(ParseCompletedOrder(order));
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            // { "SELL": [{ "oid": "59e59b279bd8d31d093d956e", "type": "SELL", "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "SELL","price": 0.1,"dealAmount": 0,"pendingAmount": 100, "createdAt": 1508219688000, "updatedAt": 1508219688000 } ... ],
            //   "BUY":  [{ "oid": "59e42bf09bd8d374c9956caa", "type": "BUY",  "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "price": 0.00009727,"dealAmount": 31.14503, "pendingAmount": 16.94827, "createdAt": 1508125681000, "updatedAt": 1508125681000 } ... ]
            var payload = await GetNoncePayloadAsync();
            if (symbol != null)
            {
                payload["symbol"] = symbol;
            }

            JToken token = await MakeJsonRequestAsync<JToken>("/order/active-map?" + CryptoUtility.GetFormForPayload(payload, false), null, payload);
            if (token != null && token.HasValues)
            {
                foreach (JToken order in token["BUY"]) orders.Add(ParseOpenOrder(order));
                foreach (JToken order in token["SELL"]) orders.Add(ParseOpenOrder(order));
            }
            return orders;
        }



        /// <summary>
        /// Kucoin does not support retrieving Orders by ID. This uses the GetCompletedOrderDetails and GetOpenOrderDetails filtered by orderId
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            var orders = await GetCompletedOrderDetailsAsync(symbol);
            orders = orders.Concat(await GetOpenOrderDetailsAsync(symbol)).ToList();

            return orders?.Where(o => o.OrderId == orderId).FirstOrDefault();
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var payload = await GetNoncePayloadAsync();
            payload["amount"] = order.Amount;
            payload["price"] = order.Price;
            payload["symbol"] = order.Symbol;
            payload["type"] = order.IsBuy ? "BUY" : "SELL";
            order.ExtraParameters.CopyTo(payload);

            // {"orderOid": "596186ad07015679730ffa02" }
            JToken token = await MakeJsonRequestAsync<JToken>("/order?" + CryptoUtility.GetFormForPayload(payload, false), null, payload, "POST");
            return new ExchangeOrderResult() { OrderId = token["orderOid"].ToStringInvariant() };       // this is different than the oid created when filled
        }

        /// <summary>
        /// Must pass the Original Order ID returned from PlaceOrder, not the OrderID returned from GetOrder
        /// </summary>
        /// <param name="orderId">The Original Order Id return from Place Order</param>
        /// <returns></returns>
        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            // Find order detail
            ExchangeOrderResult order = await GetOrderDetailsAsync(orderId, symbol);

            // There is no order to be cancelled
            if (order == null)
            {
                return;
            }

            var payload = await GetNoncePayloadAsync();
            payload["orderOid"] = order.OrderId;
            payload["symbol"] = order.Symbol;
            payload["type"] = order.IsBuy ? "BUY" : "SELL";
            JToken token = await MakeJsonRequestAsync<JToken>("/cancel-order?" + CryptoUtility.GetFormForPayload(payload, false), null, payload, "POST");
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            // { "oid": "598aeb627da3355fa3e851ca", "address": "598aeb627da3355fa3e851ca", "context": null, "userOid": "5969ddc96732d54312eb960e", "coinType": "KCS", "createdAt": 1502276446000, "deletedAt": null, "updatedAt": 1502276446000,    "lastReceivedAt": 1502276446000   }
            JToken token = await MakeJsonRequestAsync<JToken>("/account/" + symbol + "/wallet/address", null, await GetNoncePayloadAsync());
            if (token != null && token.HasValues)
            {
                return new ExchangeDepositDetails()
                {
                    Symbol = symbol,
                    Address = token["address"].ToStringInvariant(),
                    AddressTag = token["userOid"].ToStringInvariant()           // this isn't in their documentation, but is how it's being used on other interfaces
                };
            }
            return null;
        }

        /// <summary>
        /// Kucoin doesn't support withdraws to Cryptonight currency addresses (No Address Tag paramater)
        /// </summary>
        /// <param name="withdrawalRequest"></param>
        /// <returns></returns>
        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = true };
            var payload = await GetNoncePayloadAsync();
            payload["address"] = withdrawalRequest.Address;
            payload["amount"] = withdrawalRequest.Amount;

            JToken token = await MakeJsonRequestAsync<JToken>("/account/" + withdrawalRequest.Currency + "/withdraw/apply", null, payload, "POST");
            // no data is returned. Check error will throw exception on failure
            return response;
        }

        #endregion

        #region Private Functions

        private ExchangeTicker ParseTicker(JToken token, string symbol)
        {
            return this.ParseTicker(token, symbol, "sell", "buy", "lastDealPrice", "vol", "volValue", "datetime", TimestampType.UnixMilliseconds, "coinType", "coinTypePair");
        }

        // { "oid": "59e59b279bd8d31d093d956e", "type": "SELL", "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "SELL","price": 0.1,"dealAmount": 0,"pendingAmount": 100, "createdAt": 1508219688000, "updatedAt": 1508219688000 }
        private ExchangeOrderResult ParseOpenOrder(JToken token)
        {
            ExchangeOrderResult order = new ExchangeOrderResult()
            {
                OrderId = token["oid"].ToStringInvariant(),
                Symbol = token["coinType"].ToStringInvariant() + "-" + token["coinTypePair"].ToStringInvariant(),
                IsBuy = token["direction"].ToStringInvariant().Equals("BUY"),
                Price = token["price"].ConvertInvariant<decimal>(),
                AveragePrice = token["price"].ConvertInvariant<decimal>(),
                OrderDate = DateTimeOffset.FromUnixTimeMilliseconds(token["createdAt"].ConvertInvariant<long>()).DateTime
            };

            // Amount and Filled are returned as Sold and Pending, so we'll adjust
            order.AmountFilled = token["dealAmount"].ConvertInvariant<decimal>();
            order.Amount = token["pendingAmount"].ConvertInvariant<decimal>() + order.AmountFilled;

            if (order.Amount == order.AmountFilled) order.Result = ExchangeAPIOrderResult.Filled;
            else if (order.AmountFilled == 0m) order.Result = ExchangeAPIOrderResult.Pending;
            else order.Result = ExchangeAPIOrderResult.FilledPartially;

            return order;
        }

    
        // {"createdAt": 1508219588000, "amount": 92.79323381, "dealValue": 0.00927932, "dealPrice": 0.0001, "fee": 1e-8,"feeRate": 0, "oid": "59e59ac49bd8d31d09f85fa8", "orderOid": "59e59ac39bd8d31d093d956a", "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "dealDirection": "BUY" }
        private ExchangeOrderResult ParseCompletedOrder(JToken token)
        {
            return new ExchangeOrderResult()
            {
                OrderId = token["oid"].ToStringInvariant(),                                     
                Symbol = token["coinType"].ToStringInvariant() + "-" + token["coinTypePair"].ToStringInvariant(),
                IsBuy = token["direction"].ToStringInvariant().Equals("BUY"),
                Amount = token["amount"].ConvertInvariant<decimal>(),
                AmountFilled = token["amount"].ConvertInvariant<decimal>(),
                Price = token["dealPrice"].ConvertInvariant<decimal>(),
                AveragePrice = token["dealPrice"].ConvertInvariant<decimal>(),
                Message = string.Format("Original Order ID: {0}", token["orderOid"].ToStringInvariant()),           // each new order is given an order ID. As it is filled, possibly across multipl orders, a new oid is created. Here we put the orginal orderid
                Fees = decimal.Parse(token["fee"].ToStringInvariant(), System.Globalization.NumberStyles.Float),     // returned with exponent so have to parse
                OrderDate = DateTimeOffset.FromUnixTimeMilliseconds(token["createdAt"].ConvertInvariant<long>()).DateTime,
                Result = ExchangeAPIOrderResult.Filled
            };
        }

        #endregion
    }

    public partial class ExchangeName { public const string Kucoin = "Kucoin"; }
}
