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
        public override string BaseUrlWebSocket { get; set; } = "wss://push1.kucoin.com/endpoint";

        public ExchangeKucoinAPI()
        {
            RequestContentType = "x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMillisecondsString;
            MarketSymbolSeparator = "-";
            RateLimit = new RateGate(20, TimeSpan.FromSeconds(60.0));
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
                NonceOffset = CryptoUtility.UtcNow - CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["timestamp"].ConvertInvariant<long>());
            }
            catch
            {
            }
        }

        #endregion ProcessRequest

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
                MinWithdrawalSize = currency["withdrawMinAmount"].ConvertInvariant<decimal>(),
            });
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            // [ { "coinType": "KCS", "trading": true, "lastDealPrice": 4500,"buy": 4120, "sell": 4500, "coinTypePair": "BTC", "sort": 0,"feeRate": 0.001,"volValue": 324866889, "high": 6890, "datetime": 1506051488000, "vol": 5363831663913, "low": 4500, "changeRate": -0.3431 }, ... ]
            JToken marketSymbolTokens = await MakeJsonRequestAsync<JToken>("/market/open/symbols");
            foreach (JToken marketSymbolToken in marketSymbolTokens) symbols.Add(marketSymbolToken["coinType"].ToStringInvariant() + "-" + marketSymbolToken["coinTypePair"].ToStringInvariant());        // they don't put it together for ya...
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // [ { "coinType": "ETH", "trading": true, "symbol": "ETH-BTC", "lastDealPrice": 0.03169122, "buy": 0.03165041, "sell": 0.03168714, "change": -0.00004678, "coinTypePair": "BTC", "sort": 100, "feeRate": 0.001, "volValue": 121.99939218, "plus": true, "high": 0.03203444, "datetime": 1539730948000, "vol": 3847.9028281, "low": 0.03153312, "changeRate": -0.0015 }, ... ]
            JToken marketSymbolTokens = await MakeJsonRequestAsync<JToken>("/market/open/symbols");
            foreach (JToken marketSymbolToken in marketSymbolTokens)
            {
                ExchangeMarket market = new ExchangeMarket()
                {
                    IsActive = marketSymbolToken["trading"].ConvertInvariant<bool>(),
                    BaseCurrency = marketSymbolToken["coinType"].ToStringInvariant(),
                    QuoteCurrency = marketSymbolToken["coinTypePair"].ToStringInvariant(),
                    MarketSymbol = marketSymbolToken["symbol"].ToStringInvariant()
                };
                markets.Add(market);
            }
            return markets;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/open/orders?symbol=" + marketSymbol + "&limit=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, asks: "SELL", bids: "BUY", maxCount: maxCount);
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            // { "coinType": "KCS","trading": true,"lastDealPrice": 5040,"buy": 5000,"sell": 5040, "coinTypePair": "BTC","sort": 0,"feeRate": 0.001,"volValue": 308140577,"high": 6890, "datetime": 1506050394000, "vol": 5028739175025, "low": 5040, "changeRate": -0.2642 }
            JToken token = await MakeJsonRequestAsync<JToken>("/" + marketSymbol + "/open/tick");
            return this.ParseTicker(token, marketSymbol);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            // [ { "coinType": "KCS", "trading": true, "lastDealPrice": 4500, "buy": 4120, "sell": 4500, "coinTypePair": "BTC", "sort": 0, "feeRate": 0.001, "volValue": 324866889, "high": 6890, "datetime": 1506051488000, "vol": 5363831663913, "low": 4500, "changeRate": -0.3431  }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/open/tick");
            foreach (JToken tick in token)
            {
                string marketSymbol = tick["coinType"].ToStringInvariant() + "-" + tick["coinTypePair"].ToStringInvariant();
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, ParseTicker(tick, marketSymbol)));
            }
            return tickers;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // [0]-Timestamp [1]-OrderType [2]-Price [3]-Amount [4]-Volume
            // [[1506037604000,"SELL",5210,48600633397,2532093],... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/open/deal-orders?symbol=" + marketSymbol);
            foreach (JToken trade in token)
            {
                trades.Add(trade.ParseTrade(3, 2, 1, 0, TimestampType.UnixMilliseconds));
            }
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            JToken token = await MakeJsonRequestAsync<JToken>("/open/deal-orders?symbol=" + marketSymbol + (startDate == null ? string.Empty : "&since=" + startDate.Value.UnixTimestampFromDateTimeMilliseconds()));
            foreach (JArray trade in token)
            {
                trades.Add(trade.ParseTrade(3, 2, 1, 0, TimestampType.UnixMilliseconds));
            }
            var rc = callback?.Invoke(trades);
        }

        /// <summary>
        /// This is a private call on Kucoin and therefore requires an API Key + API Secret. Calling this without authorization will cause an exception
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            List<MarketCandle> candles = new List<MarketCandle>();

            string periodString = PeriodSecondsToString(periodSeconds);
            endDate = endDate ?? CryptoUtility.UtcNow;
            startDate = startDate ?? CryptoUtility.UtcNow.AddDays(-1);

            var payload = new Dictionary<string, object>();
            payload.Add("symbol", marketSymbol);
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
                        Name = marketSymbol,
                        PeriodSeconds = periodSeconds,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(token["t"][i].ConvertInvariant<long>()).DateTime,
                        ClosePrice = token["c"][i].ConvertInvariant<decimal>(),
                        HighPrice = token["h"][i].ConvertInvariant<decimal>(),
                        LowPrice = token["l"][i].ConvertInvariant<decimal>(),
                        OpenPrice = token["o"][i].ConvertInvariant<decimal>(),
                        BaseCurrencyVolume = token["v"][i].ConvertInvariant<double>(),
                        QuoteCurrencyVolume = token["v"][i].ConvertInvariant<double>() * token["c"][i].ConvertInvariant<double>()
                    });
                }
            }
            return candles;
        }

        #endregion Public APIs

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            return await OnGetAmountsInternalAsync(true);
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            return await OnGetAmountsInternalAsync(false);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            // "datas": [ {"createdAt": 1508219588000, "amount": 92.79323381, "dealValue": 0.00927932, "dealPrice": 0.0001, "fee": 1e-8,"feeRate": 0, "oid": "59e59ac49bd8d31d09f85fa8", "orderOid": "59e59ac39bd8d31d093d956a", "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "dealDirection": "BUY" }, ... ]
            var payload = await GetNoncePayloadAsync();
            if (string.IsNullOrWhiteSpace(marketSymbol))
            {
                payload["limit"] = 100;
            }
            else
            {
                payload["symbol"] = marketSymbol;
                payload["limit"] = 20;
            }

            JToken token = await MakeJsonRequestAsync<JToken>("/order/dealt?" + CryptoUtility.GetFormForPayload(payload, false), null, payload);
            if (token != null && token.HasValues)
            {
                foreach (JToken order in token["datas"])
                {
                    orders.Add(ParseCompletedOrder(order));
                }
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            // { "SELL": [{ "oid": "59e59b279bd8d31d093d956e", "type": "SELL", "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "SELL","price": 0.1,"dealAmount": 0,"pendingAmount": 100, "createdAt": 1508219688000, "updatedAt": 1508219688000 } ... ],
            //   "BUY":  [{ "oid": "59e42bf09bd8d374c9956caa", "type": "BUY",  "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "price": 0.00009727,"dealAmount": 31.14503, "pendingAmount": 16.94827, "createdAt": 1508125681000, "updatedAt": 1508125681000 } ... ]
            var payload = await GetNoncePayloadAsync();
            if (marketSymbol != null)
            {
                payload["symbol"] = marketSymbol;
            }

            JToken token = await MakeJsonRequestAsync<JToken>("/order/active-map?" + CryptoUtility.GetFormForPayload(payload, false), null, payload);
            if (token != null && token.HasValues)
            {
                foreach (JToken order in token["BUY"])
                {
                    orders.Add(ParseOpenOrder(order));
                }
                foreach (JToken order in token["SELL"])
                {
                    orders.Add(ParseOpenOrder(order));
                }
            }
            return orders;
        }

        /// <summary>
        /// Kucoin does not support retrieving Orders by ID. This uses the GetCompletedOrderDetails and GetOpenOrderDetails filtered by orderId
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            var orders = await GetCompletedOrderDetailsAsync(marketSymbol);
            orders = orders.Concat(await GetOpenOrderDetailsAsync(marketSymbol)).ToList();

            return orders?.Where(o => o.OrderId == orderId).FirstOrDefault();
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var payload = await GetNoncePayloadAsync();
            payload["amount"] = order.Amount;
            payload["price"] = order.Price;
            payload["symbol"] = order.MarketSymbol;
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
        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            // Find order detail
            ExchangeOrderResult order = await GetOrderDetailsAsync(orderId, marketSymbol);

            // There is no order to be cancelled
            if (order == null)
            {
                return;
            }

            var payload = await GetNoncePayloadAsync();
            payload["orderOid"] = order.OrderId;
            payload["symbol"] = order.MarketSymbol;
            payload["type"] = order.IsBuy ? "BUY" : "SELL";
            JToken token = await MakeJsonRequestAsync<JToken>("/cancel-order?" + CryptoUtility.GetFormForPayload(payload, false), null, payload, "POST");
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            // { "oid": "598aeb627da3355fa3e851ca", "address": "598aeb627da3355fa3e851ca", "context": null, "userOid": "5969ddc96732d54312eb960e", "coinType": "KCS", "createdAt": 1502276446000, "deletedAt": null, "updatedAt": 1502276446000,    "lastReceivedAt": 1502276446000   }
            JToken token = await MakeJsonRequestAsync<JToken>("/account/" + currency + "/wallet/address", null, await GetNoncePayloadAsync());
            if (token != null && token.HasValues)
            {
                return new ExchangeDepositDetails()
                {
                    Currency = currency,
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

        #endregion Private APIs

        #region Websockets

        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
        {
            var websocketUrlToken = GetWebsocketBulletToken();
            return ConnectWebSocket(
                    $"?bulletToken={websocketUrlToken}&format=json&resource=api", (_socket, msg) =>
                                  {
                                      JToken token = JToken.Parse(msg.ToStringFromUTF8());
                                      if (token["type"].ToStringInvariant() == "message")
                                      {
                                          var dataToken = token["data"];
                                          var marketSymbol = dataToken["symbol"].ToStringInvariant();
                                          ExchangeTicker ticker = this.ParseTicker(dataToken, marketSymbol, "sell", "buy", "lastDealPrice", "vol", "volValue", "datetime", TimestampType.UnixMilliseconds);
                                          callback(new List<KeyValuePair<string, ExchangeTicker>>() { new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker) });
                                      }

                                      return Task.CompletedTask;
                                  }, async (_socket) =>
                                     {
                                         //need to subscribe to tickers one by one
                                         marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? (await GetMarketSymbolsAsync()).ToArray() : marketSymbols;
                                         var id = DateTime.UtcNow.Ticks;
                                         foreach (var marketSymbol in marketSymbols)
                                         {
                                             // subscribe to tick topic
                                             await _socket.SendMessageAsync(new { id = id++, type = "subscribe", topic = $"/market/{marketSymbol}_TICK" });
                                         }
                                     }
                );
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
        {
            // {{
            //   "data": {
            //     "price": 1.38E-06,
            //     "count": 1769.0,
            //     "oid": "5bda52817eab5a0e09e21398",
            //     "time": 1541034625000,
            //     "volValue": 0.00244122,
            //     "direction": "BUY"
            //   },
            //   "topic": "/trade/CHSB-BTC_HISTORY",
            //   "type": "message",
            //   "seq": 32750070023237
            // }}
            var websocketUrlToken = GetWebsocketBulletToken();
            return ConnectWebSocket(
                    $"?bulletToken={websocketUrlToken}&format=json&resource=api", (_socket, msg) =>
                    {
                        JToken token = JToken.Parse(msg.ToStringFromUTF8());
                        if (token["type"].ToStringInvariant() == "message")
                        {
                            var dataToken = token["data"];
                            var marketSymbol = token["topic"].ToStringInvariant().Split('/', '_')[2]; // /trade/CHSB-BTC_HISTORY
                            var trade = dataToken.ParseTrade(amountKey: "count", priceKey: "price", typeKey: "direction",
                                timestampKey: "time", TimestampType.UnixMilliseconds); // idKey: "oid");
                                                                                       // one day, if ExchangeTrade.Id is converted to string, then the above can be uncommented
                            callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
                        }
                        return Task.CompletedTask;
                    }, async (_socket) =>
                    {
                        //need to subscribe to trade history one by one
                        marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? (await GetMarketSymbolsAsync()).ToArray() : marketSymbols;
                        var id = DateTime.UtcNow.Ticks;
                        foreach (var marketSymbol in marketSymbols)
                        {
                            // subscribe to trade history topic
                            await _socket.SendMessageAsync(new { id = id++, type = "subscribe", topic = $"/trade/{marketSymbol}_HISTORY" });
                        }
                    }
                );
        }

        #endregion Websockets

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
                MarketSymbol = token["coinType"].ToStringInvariant() + "-" + token["coinTypePair"].ToStringInvariant(),
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
                MarketSymbol = token["coinType"].ToStringInvariant() + "-" + token["coinTypePair"].ToStringInvariant(),
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

        private async Task<Dictionary<string, decimal>> OnGetAmountsInternalAsync(bool includeFreezeBalance)
        {
            // {"success":true,"code":"OK","msg":"Operation succeeded.","timestamp":1538680663395,"data":{"total":201,"datas":[{"coinType":"KCS","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"VET","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"AXPR","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"EPRX","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"ETH","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"NEO","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"IHT","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"TMT","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"FOTA","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"NANO","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"ABT","balanceStr":"0.0","freezeBalance":0.0,"balance":0.0,"freezeBalanceStr":"0.0"},{"coinType":"BTC","balanceStr":"3.364E-5","freezeBalance":0.0,"balance":3.364E-5,"freezeBalanceStr":"0.0"}],"currPageNo":1,"limit":12,"pageNos":17}}
            // Kucoin API docs are wrong, these are wrapped in datas element maybe with total counter
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            bool foundOne = true;
            for (int i = 1; foundOne; i++)
            {
                foundOne = false;
                JToken obj = await MakeJsonRequestAsync<JToken>($"/account/balances?page=${i.ToStringInvariant()}&limit=20", null, await GetNoncePayloadAsync());
                foreach (JToken child in obj["datas"])
                {
                    foundOne = true;
                    decimal amount = child["balance"].ConvertInvariant<decimal>() + (includeFreezeBalance ? child["freezeBalance"].ConvertInvariant<decimal>() : 0);
                    if (amount > 0m)
                    {
                        amounts.Add(child["coinType"].ToStringInvariant(), amount);
                    }
                }

                // check if we have hit max count
                if (obj["total"] != null && obj["total"].ConvertInvariant<int>() <= amounts.Count)
                {
                    break;
                }
            }
            return amounts;
        }

        private string GetWebsocketBulletToken()
        {
            var jsonRequestTask = MakeJsonRequestAsync<JToken>("/bullet/usercenter/loginUser?protocol=websocket&encrypt=true", BaseUrl);
            //wait for one second before timing out so we don't hold up the thread
            jsonRequestTask.Wait(TimeSpan.FromSeconds(1));
            var result = jsonRequestTask.Result;
            return result["bulletToken"].ToString();
        }

        #endregion Private Functions
    }

    public partial class ExchangeName { public const string Kucoin = "Kucoin"; }
}