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
using System.Security;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeKucoinAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://openapi-v2.kucoin.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://push1.kucoin.com/endpoint";

        public ExchangeKucoinAPI()
        {
            RequestContentType = "application/json";
            NonceStyle = NonceStyle.UnixMilliseconds;

            MarketSymbolSeparator = "-";
            RateLimit = new RateGate(20, TimeSpan.FromSeconds(60.0));
        }

        public override string PeriodSecondsToString(int seconds)
        {
            switch (seconds)
            {
                case 60: return "1min";
                case 180: return "3min";
                case 300: return "5min";
                case 900: return "15min";
                case 1800: return "30min";
                case 3600: return "1hour";
                case 7200: return "2hour";
                case 14400: return "4hour";
                case 21600: return "6hour";
                case 28800: return "8hour";
                case 43200: return "12hour";
                case 86400: return "1D";
                case 604800: return "1W";
                default: throw new ArgumentException($"{nameof(seconds)} must be 60, 180, 300, 900, 1800, 3600, 7200, 14400, 21600, 28800, 43200, 86400, 604800");
            }
        }

        #region ProcessRequest

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.AddHeader("KC-API-KEY", PublicApiKey.ToUnsecureString());
                request.AddHeader("KC-API-TIMESTAMP", payload["nonce"].ToStringInvariant());
                request.AddHeader("KC-API-PASSPHRASE", Passphrase.ToUnsecureString());

                var endpoint = request.RequestUri.PathAndQuery;
                //For Gets, Deletes, no need to add the parameters in JSON format
                var message = "";
                var sig = "";
                if (request.Method == "GET" || request.Method == "DELETE")
                {
                    //Request will be a querystring
                    message = string.Format("{0}{1}{2}", payload["nonce"], request.Method, endpoint);
                    sig = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString(), true);
                }
                else if (request.Method == "POST")
                {
                    message = string.Format("{0}{1}{2}{3}", payload["nonce"], request.Method, endpoint, CryptoUtility.GetJsonForPayload(payload, true));
                    sig = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString(), true);
                }
                request.AddHeader("KC-API-SIGN", sig);
            }

            if (request.Method == "POST")
            {
                string msg = CryptoUtility.GetJsonForPayload(payload, true);
                byte[] content = msg.ToBytesUTF8();
                await request.WriteAllAsync(content, 0, content.Length);
            }
        }

        protected override async Task OnGetNonceOffset()
        {
            try
            {
                JToken token = await MakeJsonRequestAsync<JToken>("/timestamp");
                NonceOffset = CryptoUtility.UtcNow - CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["data"].ConvertInvariant<long>());
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
            // [{"symbol":"REQ-ETH","quoteMaxSize":"99999999","enableTrading":true,"priceIncrement":"0.0000001","baseMaxSize":"1000000","baseCurrency":"REQ","quoteCurrency":"ETH","market":"ETH","quoteIncrement":"0.0000001","baseMinSize":"1","quoteMinSize":"0.00001","name":"REQ-ETH","baseIncrement":"0.0001"}, ... ]
            JToken marketSymbolTokens = await MakeJsonRequestAsync<JToken>("/symbols");
            foreach (JToken marketSymbolToken in marketSymbolTokens) symbols.Add(marketSymbolToken["symbol"].ToStringInvariant());
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // [{"symbol":"REQ-ETH","quoteMaxSize":"99999999","enableTrading":true,"priceIncrement":"0.0000001","baseMaxSize":"1000000","baseCurrency":"REQ","quoteCurrency":"ETH","market":"ETH","quoteIncrement":"0.0000001","baseMinSize":"1","quoteMinSize":"0.00001","name":"REQ-ETH","baseIncrement":"0.0001"}, ... ]
            JToken marketSymbolTokens = await MakeJsonRequestAsync<JToken>("/symbols");
            foreach (JToken marketSymbolToken in marketSymbolTokens)
            {
                ExchangeMarket market = new ExchangeMarket()
                {
                    MarketSymbol = marketSymbolToken["symbol"].ToStringInvariant(),
                    BaseCurrency = marketSymbolToken["baseCurrency"].ToStringInvariant(),
                    QuoteCurrency = marketSymbolToken["quoteCurrency"].ToStringInvariant(),
                    MinTradeSize = marketSymbolToken["baseMinSize"].ConvertInvariant<decimal>(),
                    MinTradeSizeInQuoteCurrency = marketSymbolToken["quoteMinSize"].ConvertInvariant<decimal>(),
                    MaxTradeSize = marketSymbolToken["baseMaxSize"].ConvertInvariant<decimal>(),
                    MaxTradeSizeInQuoteCurrency = marketSymbolToken["quoteMaxSize"].ConvertInvariant<decimal>(),
                    QuantityStepSize = marketSymbolToken["baseIncrement"].ConvertInvariant<decimal>(),
                    PriceStepSize = marketSymbolToken["priceIncrement"].ConvertInvariant<decimal>(),
                    IsActive = marketSymbolToken["enableTrading"].ConvertInvariant<bool>(),
                };
                markets.Add(market);
            }
            return markets;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/market/orderbook/level2_" + maxCount + "?symbol=" + marketSymbol);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, asks: "asks", bids: "bids", maxCount: maxCount);
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            //{ "code":"200000","data":{ "sequence":"1550467754497","bestAsk":"0.000277","size":"107.3627934","price":"0.000276","bestBidSize":"2062.7337015","time":1551735305135,"bestBid":"0.0002741","bestAskSize":"223.177"} }
            JToken token = await MakeJsonRequestAsync<JToken>("/market/orderbook/level1?symbol=" + marketSymbol);
            return this.ParseTicker(token, marketSymbol);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            // [ { "coinType": "KCS", "trading": true, "lastDealPrice": 4500, "buy": 4120, "sell": 4500, "coinTypePair": "BTC", "sort": 0, "feeRate": 0.001, "volValue": 324866889, "high": 6890, "datetime": 1506051488000, "vol": 5363831663913, "low": 4500, "changeRate": -0.3431  }, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/market/allTickers");
            foreach (JToken tick in token)
            {
                string marketSymbol = tick["symbol"].ToStringInvariant();
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, ParseTickers(tick, marketSymbol)));
            }
            return tickers;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // [0]-Timestamp [1]-OrderType [2]-Price [3]-Amount [4]-Volume
            // [[1506037604000,"SELL",5210,48600633397,2532093],... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/orders?status=active&symbol=" + marketSymbol);
            foreach (JToken trade in token)
            {
                trades.Add(trade.ParseTrade("size", "price", "side", "time", TimestampType.UnixMilliseconds));
            }
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            JToken token = await MakeJsonRequestAsync<JToken>("/market/histories?symbol=" + marketSymbol + (startDate == null ? string.Empty : "&since=" + startDate.Value.UnixTimestampFromDateTimeMilliseconds()));
            foreach (JObject trade in token)
            {
                trades.Add(trade.ParseTrade("size", "price", "side", "time", TimestampType.UnixMilliseconds));
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
            payload.Add("type", periodString);
            payload.Add("startAt", (long)startDate.Value.UnixTimestampFromDateTimeSeconds());        // the nonce is milliseconds, this is seconds without decimal
            payload.Add("endAt", (long)endDate.Value.UnixTimestampFromDateTimeSeconds());            // the nonce is milliseconds, this is seconds without decimal
            var addPayload = CryptoUtility.GetFormForPayload(payload, false);

            // The results of this Kucoin API call are also a mess. 6 different arrays (c,t,v,h,l,o) with the index of each shared for the candle values
            // It doesn't use their standard error format...
            JToken token = await MakeJsonRequestAsync<JToken>("/market/candles?" + addPayload, null, payload);
            if (token != null && token.HasValues && token[0].ToStringInvariant() != null)
            {
                int childCount = token.Count();
                for (int i = 0; i < childCount; i++)
                {
                    candles.Add(new MarketCandle
                    {
                        ExchangeName = this.Name,
                        Name = marketSymbol,
                        PeriodSeconds = periodSeconds,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(token[i][0].ConvertInvariant<long>()).DateTime,
                        OpenPrice = token[i][1].ConvertInvariant<decimal>(),
                        ClosePrice = token[i][2].ConvertInvariant<decimal>(),
                        HighPrice = token[i][3].ConvertInvariant<decimal>(),
                        LowPrice = token[i][4].ConvertInvariant<decimal>(),
                        BaseCurrencyVolume = token[i][5].ConvertInvariant<double>(),
                        QuoteCurrencyVolume = token[i][6].ConvertInvariant<double>()
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
            var payload = await GetNoncePayloadAsync();
            payload["status"] = "done";
            if (string.IsNullOrWhiteSpace(marketSymbol))
            {

            }
            else
            {
                payload["symbol"] = marketSymbol;
            }

            JToken token = await MakeJsonRequestAsync<JToken>("/orders?" + CryptoUtility.GetFormForPayload(payload, false, false), null, payload);
            if (token != null && token.HasValues)
            {
                foreach (JToken order in token["items"])
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
            payload["status"] = "active";
            if (marketSymbol != null && marketSymbol != "")
            {
                payload["symbol"] = marketSymbol;
            }

            JToken token = await MakeJsonRequestAsync<JToken>("/orders?&" + CryptoUtility.GetFormForPayload(payload, false), null, payload);
            if (token != null && token.HasValues)
            {
                foreach (JToken order in token["items"])
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

            var ordertmp = orders?.Where(o => o.OrderId == orderId).FirstOrDefault();
            return ordertmp;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var payload = await GetNoncePayloadAsync();
            payload["clientOid"] = Guid.NewGuid();
            payload["size"] = order.Amount;
            payload["price"] = order.Price;
            payload["symbol"] = order.MarketSymbol;
            payload["side"] = order.IsBuy ? "buy" : "sell";
            order.ExtraParameters.CopyTo(payload);

            // {"orderOid": "596186ad07015679730ffa02" }
            JToken token = await MakeJsonRequestAsync<JToken>("/orders", null, payload, "POST");
            return new ExchangeOrderResult() { OrderId = token["orderId"].ToStringInvariant() };       // this is different than the oid created when filled
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

            JToken token = await MakeJsonRequestAsync<JToken>("/orders/" + orderId, null, payload, "DELETE");
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
			//{
			//  "id":"5c24c5da03aa673885cd67aa",
			//  "type":"message",
			//  "topic":"/market/match:BTC-USDT",
			//  "subject":"trade.l3match",
			//  "sn":1545896669145,
			//  "data":{
			//    "sequence":"1545896669145",
			//    "symbol":"BTC-USDT",
			//    "side":"buy",
			//    "size":"0.01022222000000000000",
			//    "price":"0.08200000000000000000",
			//    "takerOrderId":"5c24c5d903aa6772d55b371e",
			//    "time":"1545913818099033203",
			//    "type":"match",
			//    "makerOrderId":"5c2187d003aa677bd09d5c93",
			//    "tradeId":"5c24c5da03aa673885cd67aa"
			//  }
			//}
            var websocketUrlToken = GetWebsocketBulletToken();
			return ConnectWebSocket(
                    $"?token={websocketUrlToken}", (_socket, msg) =>

					{
                        JToken token = JToken.Parse(msg.ToStringFromUTF8());
                        if (token["type"].ToStringInvariant() == "message")
                        {
                            var dataToken = token["data"];
							var marketSymbol = token["data"]["symbol"].ToStringInvariant();
                            var trade = dataToken.ParseTrade(amountKey: "size", priceKey: "price", typeKey: "side",
                                timestampKey: "time", TimestampType.UnixNanoseconds); // idKey: "tradeId");
																					   // one day, if ExchangeTrade.Id is converted to string, then the above can be uncommented
							callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
                        }
						else if (token["type"].ToStringInvariant() == "error")
						{
							Logger.Info(token["data"].ToStringInvariant());
						}
						return Task.CompletedTask;
                    }, async (_socket) =>
                    {
						List<string> marketSymbolsList = new List<string>(marketSymbols == null || marketSymbols.Length == 0 ? 
							await GetMarketSymbolsAsync() : marketSymbols);
						StringBuilder symbolsSB = new StringBuilder();
						var id = DateTime.UtcNow.Ticks; // just needs to be a "Unique string to mark the request"
						int tunnelInt = 0;
						while (marketSymbolsList.Count > 0)
						{ // can only subscribe to 100 symbols per session (started w/ API 2.0)
							var nextBatch = marketSymbolsList.GetRange(index: 0, count: 100);
							marketSymbolsList.RemoveRange(index: 0, count: 100);
							// create a new tunnel
							await _socket.SendMessageAsync(new
							{
								id = id++,
								type = "openTunnel",
								newTunnelId = $"bt{tunnelInt}",
								response = "true",
							});
							// wait for tunnel to be created
							await Task.Delay(millisecondsDelay: 1000);
							// subscribe to Match Execution Data
							await _socket.SendMessageAsync(new
							{
								id = id++,
								type = "subscribe",
								topic = $"/market/match:{ string.Join(",", nextBatch)}",
								tunnelId = $"bt{tunnelInt}",
								privateChannel = "false", //Adopted the private channel or not. Set as false by default.
								response = "true",
							});
							tunnelInt++;
						}
                    }
                );
        }

        #endregion Websockets

        #region Private Functions

        private ExchangeTicker ParseTicker(JToken token, string symbol)
        {
            //            //Get Ticker
            //            {
            //                "sequence": "1550467636704",
            //    "bestAsk": "0.03715004",
            //    "size": "0.17",
            //    "price": "0.03715005",
            //    "bestBidSize": "3.803",
            //    "bestBid": "0.03710768",
            //    "bestAskSize": "1.788",
            //    "time": 1550653727731

            //}
            return this.ParseTicker(token, symbol, "bestAsk", "bestBid", "price", "bestAskSize");
        }
        private ExchangeTicker ParseTickers(JToken token, string symbol)
        {
            //      {
            //          "symbol": "LOOM-BTC",
            //  "buy": "0.00001191",
            //  "sell": "0.00001206",
            //  "changeRate": "0.057",
            //  "changePrice": "0.00000065",
            //  "high": "0.0000123",
            //  "low": "0.00001109",
            //  "vol": "45161.5073",
            //  "last": "0.00001204"
            //},
            return this.ParseTicker(token, symbol, "sell", "buy", "last", "vol");
        }

        // { "oid": "59e59b279bd8d31d093d956e", "type": "SELL", "userOid": null, "coinType": "KCS", "coinTypePair": "BTC", "direction": "SELL","price": 0.1,"dealAmount": 0,"pendingAmount": 100, "createdAt": 1508219688000, "updatedAt": 1508219688000 }
        private ExchangeOrderResult ParseOpenOrder(JToken token)
        {
            ExchangeOrderResult order = new ExchangeOrderResult()
            {
                OrderId = token["id"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant().Equals("BUY"),
                Price = token["price"].ConvertInvariant<decimal>(),
                AveragePrice = token["price"].ConvertInvariant<decimal>(),
                OrderDate = DateTimeOffset.FromUnixTimeMilliseconds(token["createdAt"].ConvertInvariant<long>()).DateTime
            };

            // Amount and Filled are returned as Sold and Pending, so we'll adjust
            order.AmountFilled = token["dealSize"].ConvertInvariant<decimal>();
            order.Amount = token["size"].ConvertInvariant<decimal>() + order.AmountFilled;

            if (order.Amount == order.AmountFilled) order.Result = ExchangeAPIOrderResult.Filled;
            else if (order.AmountFilled == 0m) order.Result = ExchangeAPIOrderResult.Pending;
            else order.Result = ExchangeAPIOrderResult.FilledPartially;

            return order;
        }

        // {"createdAt": 1508219588000, "amount": 92.79323381, "dealValue": 0.00927932, "dealPrice": 0.0001, "fee": 1e-8,"feeRate": 0, "oid": "59e59ac49bd8d31d09f85fa8", "orderOid": "59e59ac39bd8d31d093d956a", "coinType": "KCS", "coinTypePair": "BTC", "direction": "BUY", "dealDirection": "BUY" }
        private ExchangeOrderResult ParseCompletedOrder(JToken token)
        {
            ExchangeOrderResult order = new ExchangeOrderResult()
            {
                OrderId = token["id"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant().Equals("BUY"),
                Amount = token["size"].ConvertInvariant<decimal>(),
                AmountFilled = token["dealSize"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                AveragePrice = token["price"].ConvertInvariant<decimal>(),
                //Message = string.Format("Original Order ID: {0}", token["orderOid"].ToStringInvariant()),           // each new order is given an order ID. As it is filled, possibly across multipl orders, a new oid is created. Here we put the orginal orderid
                Fees = decimal.Parse(token["fee"].ToStringInvariant(), System.Globalization.NumberStyles.Float),     // returned with exponent so have to parse
                OrderDate = DateTimeOffset.FromUnixTimeMilliseconds(token["createdAt"].ConvertInvariant<long>()).DateTime

            };
            if (token["cancelExist"].ToStringInvariant().ToUpper() == "TRUE")
            {
                order.Result = ExchangeAPIOrderResult.Canceled;
            }
            else
            {
                order.Result = ExchangeAPIOrderResult.Filled;
            }
            return order;
        }

        private async Task<Dictionary<string, decimal>> OnGetAmountsInternalAsync(bool includeFreezeBalance)
        {
            //            {
            //                "id": "5bd6e9216d99522a52e458d6",
            //    "currency": "BTC",
            //    "type": "trade",
            //    "balance": "1234356",
            //    "available": "1234356",
            //    "holds": "0"
            //}]

            ///api/v1/accounts
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/accounts", payload: await GetNoncePayloadAsync());
            foreach (var ob in obj)
            {
                if (ob["type"].ToStringInvariant().ToLower() == "trade")
                {
                    amounts.Add(ob["currency"].ToStringInvariant(), ob[(includeFreezeBalance ? "balance" : "available")].ConvertInvariant<decimal>());
                }
            }

            //amounts.Add(obj["currency"].ToStringInvariant(), obj["available"].ConvertInvariant<decimal>());
            //amounts.Add(obj["type"].ToStringInvariant(), obj["trade"].ToStringInvariant());
            return amounts;
        }

        private string GetWebsocketBulletToken()
        {
			Dictionary<string, object> payload = new Dictionary<string, object>()
			{
				["code"] = "200000",
				["data"] = new { instanceServers = new[] { new Dictionary<string, object>()
				  { ["pingInterval"] = 50000,
					["endpoint"] = "wss://push1-v2.kucoin.net/endpoint",
					["protocol"] = "websocket",
					["encrypt"] = "true",
					["pingTimeout"] = 10000, } } },
				//["token"] = "vYNlCtbz4XNJ1QncwWilJnBtmmfe4geLQDUA62kKJsDChc6I4bRDQc73JfIrlFaVYIAE0Gv2",
			};
			var jsonRequestTask = MakeJsonRequestAsync<JToken>("/bullet-public", 
				baseUrl: BaseUrl, payload: payload, requestMethod: "POST");
            // wait for one second before timing out so we don't hold up the thread
            jsonRequestTask.Wait(TimeSpan.FromSeconds(1));
            var result = jsonRequestTask.Result;
			// in the future, they may introduce new server endpoints, possibly for load balancing
			this.BaseUrlWebSocket = result["instanceServers"][0]["endpoint"].ToStringInvariant();
			return result["token"].ToStringInvariant();
        }

        #endregion Private Functions
    }

    public partial class ExchangeName { public const string Kucoin = "Kucoin"; }
}
