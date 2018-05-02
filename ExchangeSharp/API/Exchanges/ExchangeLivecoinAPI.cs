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
using System.Globalization;
using System.Net;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public sealed class ExchangeLivecoinAPI : ExchangeAPI
    {
        public override string Name => ExchangeName.Livecoin;
        public override string BaseUrl { get; set; } = "https://api.livecoin.net";

        public ExchangeLivecoinAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMillisecondsString;
            SymbolSeparator = "/";
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace('_', '/').Replace('-', '/');
        }

        #region ProcessRequest 

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.Headers["API-key"] = PublicApiKey.ToUnsecureString();
                request.Headers["Sign"] = CryptoUtility.SHA256Sign(request.RequestUri.Query.Length > 1 ? request.RequestUri.Query.Substring(1) : request.RequestUri.Query, PrivateApiKey.ToUnsecureString()).ToUpper();
            }
        }

        #endregion

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            // "info": [ {"name": "Bitcoin", "symbol": "BTC", "walletStatus": "down","withdrawFee": 0.0004,"minDepositAmount": 0, "minWithdrawAmount": 0.002 }, ... ]
            // walletStatus - normal - Wallet online, delayed - Wallet delayed(no new block for 1 - 2 hours), blocked - Out of sync(no new block for at least 2 hours), blocked_long - No new block for at least 24h(Out of sync), down - Wallet temporary offline,  delisted - Asset will be delisted soon, withdraw your funds., closed_cashin - Only withdrawal is available, closed_cashout - Only deposit is available
            JToken token = await MakeJsonRequestAsync<JToken>("/info/coinInfo");
            token = CheckError(token);
            if (token != null)
            {
                foreach (JToken currency in token["info"])
                {
                    currencies.Add(currency["symbol"].ToStringInvariant(), new ExchangeCurrency()
                    {
                        Name = currency["symbol"].ToStringInvariant(),
                        FullName = currency["name"].ToStringInvariant(),
                        IsEnabled = currency["walletStatus"].ToStringInvariant().Equals("normal"),
                        TxFee = currency["withdrawFee"].ConvertInvariant<decimal>()
                    });
                }
            }
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            // {"success": true,"minBtcVolume": 0.0005,"restrictions": [{"currencyPair": "BTC/USD","priceScale": 5}, ... ]}
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/restrictions");
            token = CheckError(token);
            foreach (JToken market in token["restrictions"]) symbols.Add(market["currencyPair"].ToStringInvariant());
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // {"success": true,"minBtcVolume": 0.0005,"restrictions": [{"currencyPair": "BTC/USD","priceScale": 5}, ... ]}
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/restrictions");
            token = CheckError(token);
            foreach (JToken market in token["restrictions"])
            {
                var split = market["currencyPair"].ToStringInvariant().Split('/');
                markets.Add(new ExchangeMarket()
                {
                    MarketName = market["currencyPair"].ToStringInvariant(),
                    BaseCurrency = split[0],
                    MarketCurrency = split[1],
                    IsActive = true
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/ticker?currencyPair=" + symbol);
            token = CheckError(token);
            return ParseTicker(token);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/ticker");
            token = CheckError(token);
            foreach (JToken tick in token) tickers.Add(new KeyValuePair<string, ExchangeTicker>(tick["symbol"].ToStringInvariant(), ParseTicker(tick)));
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/order_book?currencyPair=" + symbol + "&depth=" + maxCount.ToStringInvariant());
            token = CheckError(token);
            if (token != null)
            {
                foreach (JToken order in token["asks"])
                {
                    orders.Asks.Add(new ExchangeOrderPrice()
                    {
                        Price = order[0].ConvertInvariant<decimal>(),
                        Amount = order[1].ConvertInvariant<decimal>()
                    });
                }
                foreach (JToken order in token["bids"])
                {
                    orders.Bids.Add(new ExchangeOrderPrice()
                    {
                        Price = order[0].ConvertInvariant<decimal>(),
                        Amount = order[1].ConvertInvariant<decimal>()
                    });
                }
            }
            return orders;
        }

        /// <summary>
        /// Returns trades from the last minute
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/last_trades?currencyPair=" + symbol);
            token = CheckError(token);
            foreach (JToken trade in token) trades.Add(ParseTrade(trade));

            return trades;
        }

        /// <summary>
        /// Max returns is trades from the last hour only
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="symbol"></param>
        /// <param name="sinceDateTime"></param>
        /// <returns></returns>
        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            symbol = NormalizeSymbol(symbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // Not directly supported so we'll return what they have and filter if necessary
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/last_trades?currencyPair=" + symbol + "&minutesOrHour=false");
            token = CheckError(token);
            foreach (JToken trade in token)
            {
                ExchangeTrade rc = ParseTrade(trade);
                if (sinceDateTime != null) { if (rc.Timestamp > sinceDateTime) trades.Add(rc); }
                else trades.Add(rc);
            }
            callback?.Invoke(trades);
        }

        /// <summary>
        /// Yobit Livecoin support GetCandles. It is possible to get all trades since startdate (filter by enddate if needed) and then aggregate into MarketCandles by periodSeconds 
        /// TODO: Aggregate Livecoin Trades into Candles. 
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private APIs
        // Livecoin has both a client_order and client_trades interface. The trades is page-based at 100 (default can be increased)
        // The two calls seem redundant, but orders seem to include more data. 
        // The following uses the client oders call

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            // [{"type": "total","currency": "USD","value": 20},{"type": "available","currency": "USD","value": 10},{"type": "trade","currency": "USD","value": 10},{"type": "available_withdrawal","currency": "USD","value": 10}, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/balances", null, GetNoncePayload(), "GET");
            token = CheckError(token);
            foreach (JToken child in token)
            {
                if (child.Value<string>("type") == "total")
                {
                    decimal amount = child["value"].ConvertInvariant<decimal>();
                    if (amount > 0m) amounts.Add(child["currency"].ToStringInvariant(), amount);
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            // [{"type": "total","currency": "USD","value": 20},{"type": "available","currency": "USD","value": 10},{"type": "trade","currency": "USD","value": 10},{"type": "available_withdrawal","currency": "USD","value": 10}, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/balances", null, GetNoncePayload(), "GET");
            token = CheckError(token);
            foreach (JToken child in token)
            {
                if (child.Value<string>("type") == "trade")
                {
                    decimal amount = child["value"].ConvertInvariant<decimal>();
                    if (amount > 0m) amounts.Add(child["currency"].ToStringInvariant(), amount);
                }
            }

            return amounts;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/order?orderId=" + orderId, null, GetNoncePayload(), "GET");
            token = CheckError(token);
            return ParseOrder(token);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            symbol = NormalizeSymbol(symbol);
            // We can increase the number of orders returned by including a limit parameter if desired
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = GetNoncePayload();
            payload.Add("openClosed", "CLOSED");   // returns both closed and cancelled
            if (symbol != null) payload.Add("currencyPair", symbol);
            if (afterDate != null) payload.Add("issuedFrom", ((DateTime)afterDate).UnixTimestampFromDateTimeMilliseconds());

            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/client_orders?" + GetFormForPayload(payload, false), null, GetNoncePayload(), "GET");
            token = CheckError(token);
            foreach (JToken order in token["data"]) orders.Add(ParseClientOrder(order));
            return orders;
        }

        /// <summary>
        /// Limited to the last 100 open orders
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            symbol = NormalizeSymbol(symbol);
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = GetNoncePayload();
            payload.Add("openClosed", "OPEM"); 
            if (symbol != null) payload.Add("currencyPair", symbol);

            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/client_orders?" + GetFormForPayload(payload, false), null, GetNoncePayload(), "GET");
            token = CheckError(token);
            foreach (JToken order in token["data"]) orders.Add(ParseClientOrder(order));
            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string ordertype = "/exchange/";
            if (order.OrderType == OrderType.Market) ordertype += order.IsBuy ? "buymarket" : "sellmarket";
            else ordertype += order.IsBuy ? "buylimit" : "selllimit";

            //{ "success": true, "added": true, "orderId": 4912
            JToken token = await MakeJsonRequestAsync<JToken>(string.Format("{0}?currencyPair={1}&price={2}&quantity={3}", ordertype, order.Symbol, order.Price, order.Amount), null, GetNoncePayload(), "POST");
            token = CheckError(token);
            return new ExchangeOrderResult() { OrderId = token["orderId"].ToStringInvariant(), Result = ExchangeAPIOrderResult.Pending };
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            // can only cancel limit orders, which kinda makes sense, but we also need the currency pair, which requires a lookup
            var order = await OnGetOrderDetailsAsync(orderId);
            if (order != null)
            {
                // { "success": true,"cancelled": true,"message": null,"quantity": 0.0005,"tradeQuantity": 0}
                JToken token = await MakeJsonRequestAsync<JToken>("/exchange/cancel_limit?currencyPair=" + order.Symbol + "&orderId=" + orderId, null, GetNoncePayload(), "GET");
                CheckError(token); // will throw exception on error
            }
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            List<ExchangeTransaction> deposits = new List<ExchangeTransaction>();

            var payload = GetNoncePayload();
            payload.Add("start", DateTime.UtcNow.AddYears(-1).UnixTimestampFromDateTimeMilliseconds());       // required. Arbitrarily going with 1 year
            payload.Add("end", DateTime.UtcNow.UnixTimestampFromDateTimeMilliseconds());                      // also required
            payload.Add("types", "DEPOSIT,WITHDRAWAL");  // opting to return both deposits and withdraws. 

            // We can also include trades and orders with this call (which makes 3 ways to return the same data)
            JToken token = await MakeJsonRequestAsync<JToken>("/exchange/payment/history/transactions?" + GetFormForPayload(payload, false), null, GetNoncePayload(), "GET");
            token = CheckError(token);
            foreach (JToken tx in token) deposits.Add(ParseTransaction(tx));

            return deposits;
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            symbol = NormalizeSymbol(symbol);
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/get/address?" + "currency=" + symbol, BaseUrl, GetNoncePayload(), "GET");
            token = CheckError(token);
            if (token != null && token.HasValues && token["currency"].Value<string>() == symbol && token["wallet"].Value<string>() != null)
            {
                ExchangeDepositDetails address = new ExchangeDepositDetails() {Symbol = symbol };
                if (token["wallet"].Value<string>().Contains("::"))
                {
                    // address tags are separated with a '::'
                    var split = token["wallet"].Value<string>().Replace("::", ":").Split(':');
                    address.Address = split[0];
                    address.AddressTag = split[1];
                }
                else address.Address = token["wallet"].ToStringInvariant();
                return address;
            }
            return null;
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = false };
            if (!String.IsNullOrEmpty(withdrawalRequest.AddressTag)) withdrawalRequest.AddressTag = "::" + withdrawalRequest.AddressTag;

            // {"fault": null,"userId": 797,"userName": "poorguy","id": 11285042,"state": "APPROVED","createDate": 1432197911364,"lastModifyDate": 1432197911802,"verificationType": "NONE","verificationData": null, "comment": null, "description": "Transfer from Livecoin", "amount": 0.002, "currency": "BTC", "accountTo": "B1099909", "acceptDate": null, "valueDate": null, "docDate": 1432197911364, "docNumber": 11111111, "correspondentDetails": null, "accountFrom": "B0000001", "outcome": false, "external": null, "externalKey": "1111111", "externalSystemId": 18, "externalServiceId": null, "wallet": "1111111" }
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/out/coin?currency=" + withdrawalRequest.Symbol + "&wallet=" + withdrawalRequest.Address + withdrawalRequest.AddressTag + "&amount=" + withdrawalRequest.Amount, BaseUrl, GetNoncePayload(), "POST");
            token = CheckError(token);
            response.Success = true;
            return response;
        }


        #endregion

        #region Private Functions

        private JToken CheckError(JToken result)
        {
            if (result == null || (!(result is JArray) && result["success"] != null && result["success"].ConvertInvariant<bool>() == false))
            {
                throw new APIException((result != null && result["errorMessage"] != null) ? (result["errorMessage"] ?? result["exception"]).ToStringInvariant() : "Unknown Error");
            }
            return result;
        }

        private ExchangeTicker ParseTicker(JToken token)
        {
            // [{"symbol": "LTC/BTC","last": 0.00805061,"high": 0.00813633,"low": 0.00784855,"volume": 14729.48452951,"vwap": 0.00795126,"max_bid": 0.00813633,"min_ask": 0.00784855,"best_bid": 0.00798,"best_ask": 0.00811037}, ... ]
            var split = token["symbol"].ToStringInvariant().Split('/');
            return new ExchangeTicker()
            {
                   Ask = token["best_bid"].ConvertInvariant<decimal>(),
                   Bid = token["best_ask"].ConvertInvariant<decimal>(),
                   Last = token["last"].ConvertInvariant<decimal>(),
                   Volume = new ExchangeVolume()
                   {
                        // TODO: This is a guess. Need to verify the use of these values
                        ConvertedVolume = token["volume"].ConvertInvariant<decimal>(),
                        ConvertedSymbol = split[0],
                        BaseSymbol = split[1],
                        BaseVolume = token["volume"].ConvertInvariant<decimal>() * token["last"].ConvertInvariant<decimal>(),
                        Timestamp = DateTime.UtcNow
                   }
            };
        }

        private ExchangeTrade ParseTrade(JToken token)
        {
            // [ {"time": 1409935047,"id": 99451,"price": 350,"quantity": 2.85714285, "type": "BUY" }, ... ]
            return new ExchangeTrade()
            {
                Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(token["time"].ConvertInvariant<long>()),
                Id = token["id"].ConvertInvariant<long>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                IsBuy = token["type"].ToStringInvariant().Equals("BUY")
            };
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            if (token == null) return null;
            //{ "id": 88504958,"client_id": 1150,"status": "CANCELLED","symbol": "DASH/USD","price": 1.5,"quantity": 1.2,"remaining_quantity": 1.2,"blocked": 1.8018,"blocked_remain": 0,"commission_rate": 0.001,"trades": null}
            ExchangeOrderResult order = new ExchangeOrderResult()
            {

            };
            switch (token["status"].Value<string>())
            {
                case "CANCELLED": order.Result = ExchangeAPIOrderResult.Canceled; break;
            }
            return order;
        }

        private ExchangeOrderResult ParseClientOrder(JToken token)
        {
            //  "data": [{"id": 4910,"currencyPair": "BTC/USD","goodUntilTime": 0,"type": "MARKET_SELL","orderStatus": "EXECUTED","issueTime": 1409920636701,"price": null,"quantity": 2.85714285,"remainingQuantity": 0,"commission": null,"commissionRate": 0.005, "lastModificationTime": 1409920636701 }, .. ]
            ExchangeOrderResult order = new ExchangeOrderResult()
            {
                 OrderId = token["id"].ToStringInvariant(),
                 Symbol = token["currencyPair"].ToStringInvariant(),
                 OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["issueTime"].ConvertInvariant<long>()),
                 IsBuy = token["type"].ToStringInvariant().Contains("BUY"),
                 Price = token["price"].ConvertInvariant<decimal>(),
                 Amount = token["quantity"].ConvertInvariant<decimal>(),
                 Fees = token["commission"].ConvertInvariant<decimal>(),
            };

            order.AmountFilled = order.Amount - token["remainingQuantity"].ConvertInvariant<decimal>();
            switch (token["status"].Value<string>())
            {
                case "CANCELLED": order.Result = ExchangeAPIOrderResult.Canceled; break;
            }




            return order;
        }

        private ExchangeTransaction ParseTransaction(JToken token)
        {
            // [{"id": "OK521780496","type": "DEPOSIT","date": 1431882524782,"amount": 27190,"fee": 269.2079208,"fixedCurrency": "RUR", "taxCurrency": "RUR", "variableAmount": null, "variableCurrency": null, "external": "OkPay","login": null }, ... ]
            return new ExchangeTransaction()
            {

            };
        }


        #endregion

    }
}
