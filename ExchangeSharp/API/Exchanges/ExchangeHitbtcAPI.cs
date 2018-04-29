﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed class ExchangeHitbtcAPI : ExchangeAPI
    {
        public override string Name => ExchangeName.Hitbtc;
        public override string BaseUrl { get; set; } = "https://api.hitbtc.com/api/2";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.hitbtc.com/api/2/ws";

        public ExchangeHitbtcAPI()
        {
            RequestContentType = "x-www-form-urlencoded";
            NonceStyle = ExchangeSharp.NonceStyle.UnixMillisecondsString;
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace("-", string.Empty).Replace("/", string.Empty).Replace("_", string.Empty);
        }

        #region ProcessRequest 

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                using (var hmacsha512 = new HMACSHA512(Encoding.UTF8.GetBytes(PrivateApiKey.ToUnsecureString())))
                {
                    hmacsha512.ComputeHash(Encoding.UTF8.GetBytes(request.RequestUri.PathAndQuery));
                    request.Headers["X-Signature"] = string.Concat(hmacsha512.Hash.Select(b => b.ToString("x2")).ToArray()); // minimalistic hex-encoding and lower case
                }
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query - HitBTC puts all the "post" parameters in the url query instead of the request body
                var query = HttpUtility.ParseQueryString(url.Query);
                string newQuery = "nonce=" + payload["nonce"].ToString() + "&apikey=" + PublicApiKey.ToUnsecureString() + (query.Count == 0 ? string.Empty : "&" + query.ToString()) +
                    (payload.Count > 1 ? "&" + GetFormForPayload(payload, false) : string.Empty);
                url.Query = newQuery;
                return url.Uri;
            }
            return base.ProcessRequestUrl(url, payload);
        }

        #endregion

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            //[{"id": "BTC", "fullName": "Bitcoin", "crypto": true, "payinEnabled": true, "payinPaymentId": false, "payinConfirmations": 2, "payoutEnabled": true, "payoutIsPaymentId": false, "transferEnabled": true, "delisted": false, "payoutFee": "0.00958" }, ...
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/currency");
            foreach(JToken token in obj)
            {
                currencies.Add(token["id"].ToStringInvariant(), new ExchangeCurrency()
                {
                    Name = token["id"].ToStringInvariant(),
                    FullName = token["fullName"].ToStringInvariant(),
                    TxFee = token["payoutFee"].ConvertInvariant<decimal>(),
                    MinConfirmations = token["payinConfirmations"].ConvertInvariant<int>(),
                    IsEnabled = token["delisted"].ToStringInvariant().Equals("false")
                });
            }
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            // [ {"id": "ETHBTC","baseCurrency": "ETH","quoteCurrency": "BTC", "quantityIncrement": "0.001", "tickSize": "0.000001", "takeLiquidityRate": "0.001", "provideLiquidityRate": "-0.0001", "feeCurrency": "BTC"  } ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/symbol");
            foreach(JToken token in obj) symbols.Add(token["id"].ToStringInvariant());
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // [ {"id": "ETHBTC","baseCurrency": "ETH","quoteCurrency": "BTC", "quantityIncrement": "0.001", "tickSize": "0.000001", "takeLiquidityRate": "0.001", "provideLiquidityRate": "-0.0001", "feeCurrency": "BTC"  } ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/symbol");
            foreach (JToken token in obj)
            {
                markets.Add(new ExchangeMarket()
                {
                     MarketName = token["id"].ToStringInvariant(),
                     BaseCurrency = token["baseCurrency"].ToStringInvariant(),
                     MarketCurrency = token["quoteCurrency"].ToStringInvariant(),
                     QuantityStepSize = token["quantityIncrement"].ConvertInvariant<decimal>(),
                     PriceStepSize = token["tickSize"].ConvertInvariant<decimal>(),
                     IsActive = true
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/ticker/" + symbol);
            return ParseTicker(obj, symbol);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/ticker");
            foreach (JToken token in obj)
            {
                string symbol = NormalizeSymbol(token["symbol"].ToStringInvariant());
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ParseTicker(token, symbol)));
            }
            return tickers;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            List<MarketCandle> candles = new List<MarketCandle>();

            string periodString;
            if (periodSeconds <= 60) { periodString = "M1"; periodSeconds = 60; }
            else if (periodSeconds <= 180) { periodString = "M3"; periodSeconds = 180; }
            else if (periodSeconds <= 300) { periodString = "M5"; periodSeconds = 300; }
            else if (periodSeconds <= 900) { periodString = "M15"; periodSeconds = 900; }
            else if (periodSeconds <= 1800) { periodString = "M30"; periodSeconds = 1800; }
            else if (periodSeconds <= 3600) { periodString = "H1"; periodSeconds = 3600; }
            else if (periodSeconds <= 14400) { periodString = "H4"; periodSeconds = 14400; }
            else if (periodSeconds <= 86400) { periodString = "D1"; periodSeconds = 86400; }
            else if (periodSeconds <= 604800) { periodString = "D7"; periodSeconds = 604800; }
            else { periodString = "1M"; periodSeconds = 4233600; }

            limit = limit ?? 100;

            // [ {"timestamp": "2017-10-20T20:00:00.000Z","open": "0.050459","close": "0.050087","min": "0.050000","max": "0.050511","volume": "1326.628", "volumeQuote": "66.555987736"}, ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/candles/" + symbol + "?period=" + periodString + "&limit=" + limit);
            foreach(JToken token in obj)
            {
                candles.Add(new MarketCandle()
                {
                    ExchangeName = this.Name,
                    Name = symbol,
                    Timestamp = token["timestamp"].ConvertInvariant<DateTime>(),
                    OpenPrice = token["open"].ConvertInvariant<decimal>(),
                    ClosePrice = token["close"].ConvertInvariant<decimal>(),
                    LowPrice = token["min"].ConvertInvariant<decimal>(),
                    HighPrice = token["max"].ConvertInvariant<decimal>(),
                    VolumeQuantity = token["volume"].ConvertInvariant<double>(),
                    VolumePrice = token["volumeQuote"].ConvertInvariant<double>(),
                    PeriodSeconds = periodSeconds
                });
            }
            return candles;
        }


        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // Putting an arbitrary limit of 10 for 'recent'
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/trades/" + symbol + "?limit=10");
            foreach (JToken token in obj) trades.Add(ParseExchangeTrade(token));
            return trades;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/orderbook/" + symbol + "?limit=" + maxCount.ToStringInvariant());
            if (obj != null && obj.HasValues)
            {
                foreach (JToken order in obj["ask"]) if (orders.Asks.Count < maxCount) orders.Asks.Add(new ExchangeOrderPrice() { Price = order["price"].ConvertInvariant<decimal>(), Amount = order["size"].ConvertInvariant<decimal>() });
                foreach (JToken order in obj["bid"]) if (orders.Bids.Count < maxCount) orders.Bids.Add(new ExchangeOrderPrice() { Price = order["price"].ConvertInvariant<decimal>(), Amount = order["size"].ConvertInvariant<decimal>() });
            }
            return orders;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            long? lastTradeID = null;
            // TODO: Can't get Hitbtc to return other than the last 50 trades even though their API says it should (by orderid or timestamp). When passing either of these parms, it still returns the last 50
            // So until there is an update, that's what we'll go with
            JToken obj = await MakeJsonRequestAsync<JToken>("/public/trades/" + symbol);
            if (obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    ExchangeTrade trade = ParseExchangeTrade(token);
                    lastTradeID = trade.Id;
                    if (sinceDateTime == null || trade.Timestamp >= sinceDateTime)
                    {
                        trades.Add(trade);
                    }
                }
                if (trades.Count != 0)
                {
                    callback(trades.OrderBy(t => t.Timestamp));
                }
            }
        }

        #endregion

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            // [ {"currency": "BTC","available": "0.0504600","reserved": "0.0000000"}, ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/trading/balance", null, GetNoncePayload(), "GET");
            foreach (JToken token in obj["balance"])
            {
                decimal amount = token["available"].ConvertInvariant<decimal>() + token["reserved"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            // [ {"currency": "BTC","available": "0.0504600","reserved": "0.0000000"}, ... ]
            JToken obj = await MakeJsonRequestAsync<JToken>("/trading/balance", null, GetNoncePayload(), "GET");
            foreach (JToken token in obj["balance"])
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        /// <summary>
        /// HitBtc differentiates between active orders and historical trades. They also have three diffrent OrderIds (the id, the orderId, and the ClientOrderId).
        /// When placing an order, we return the ClientOrderId, which is only in force for the duration of the order. Completed orders are given a different id.
        /// Therefore, this call returns an open order only. Do not use it to return an historical trade order.
        /// To retrieve an historical trade order by id, use the GetCompletedOrderDetails with the symbol parameter empty, then filter for desired Id.
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/history/order/" + orderId + "/trades", null, GetNoncePayload(), "GET");
            if (obj != null && obj.HasValues) return ParseCompletedOrder(obj);
            return null;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = GetNoncePayload();
            if (!string.IsNullOrEmpty(symbol)) payload["symbol"] = symbol;
            if (afterDate != null) payload["from"] = afterDate;
            JToken obj = await MakeJsonRequestAsync<JToken>("/history/trades", null, payload, "GET");
            if (obj != null && obj.HasValues) foreach (JToken token in obj) orders.Add(ParseCompletedOrder(token));
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            var payload = GetNoncePayload();
            if (!string.IsNullOrEmpty(symbol)) payload["symbol"] = symbol;
            JToken obj = await MakeJsonRequestAsync<JToken>("/history/order", null, payload, "GET");
            if (obj != null && obj.HasValues) foreach (JToken token in obj) orders.Add(ParseOpenOrder(token));
            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            ExchangeOrderResult result = new ExchangeOrderResult() { Result = ExchangeAPIOrderResult.Error };
            var payload = GetNoncePayload();
            //payload["clientOrderId"] = "neuMedia" + payload["nonce"];     Currently letting hitbtc assign this, but may not be unique for more than 24 hours
            payload["amount"] = order.Amount;
            payload["symbol"] = order.Symbol;
            payload["side"] = order.IsBuy ? "buy" : "sell";
            payload["type"] = order.OrderType == OrderType.Limit ? "limit" : "market";
            if (order.OrderType == OrderType.Limit) payload["price"] = order.Price;
            payload["timeInForce"] = "GTC";
            // { "id": 0,"clientOrderId": "d8574207d9e3b16a4a5511753eeef175","symbol": "ETHBTC","side": "sell","status": "new","type": "limit","timeInForce": "GTC","quantity": "0.063","price": "0.046016","cumQuantity": "0.000","createdAt": "2017-05-15T17:01:05.092Z","updatedAt": "2017-05-15T17:01:05.092Z"  } 
            JToken token = await MakeJsonRequestAsync<JToken>("/trading/new_order", null, payload, "POST");
            if (token != null)
            {
                if (token["error"] == null)
                {
                    result.OrderId = token["ClientOrderId"].ToStringInvariant();
                    result.Symbol = token["symbol"].ToStringInvariant();
                    result.OrderDate = token["createdAt"].ConvertInvariant<DateTime>();
                    result.Amount = token["quantity"].ConvertInvariant<decimal>();
                    result.Price = token["price"].ConvertInvariant<decimal>();
                    result.AmountFilled = token["cumQuantity"].ConvertInvariant<decimal>();
                    if (result.AmountFilled >= result.Amount) result.Result = ExchangeAPIOrderResult.Filled;
                    else if (result.AmountFilled > 0m) result.Result = ExchangeAPIOrderResult.FilledPartially;
                    else result.Result = ExchangeAPIOrderResult.Pending;
                }
                else result.Message = token["error"]["message"].ToStringInvariant();
            }
            return result;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            // this call returns info about the success of the cancel. Sure would be nice have a return type on this method.
            JToken token = await MakeJsonRequestAsync<JToken>("/order/" + orderId, null, GetNoncePayload(), "DELETE");
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            ExchangeDepositDetails deposit = new ExchangeDepositDetails() { Symbol = symbol };
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/address/" + symbol, null, GetNoncePayload(), "GET");
            if (token != null)
            {
                deposit.Address =token["address"].ToStringInvariant();
                if (deposit.Address.StartsWith("bitcoincash:")) deposit.Address = deposit.Address.Replace("bitcoincash:", string.Empty);  // don't know why they do this for bitcoincash
                deposit.AddressTag = token["wallet"].ToStringInvariant();
            }
            return deposit;
        }


        /// <summary>
        /// This returns both Deposit and Withdawl history for the Bank and Trading Accounts. Currently returning everything and not filtering. 
        /// There is no support for retrieving by Symbol, so we'll filter that after reteiving all symbols
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            List<ExchangeTransaction> transactions = new List<ExchangeTransaction>();
            // [ {"id": "6a2fb54d-7466-490c-b3a6-95d8c882f7f7","index": 20400458,"currency": "ETH","amount": "38.616700000000000000000000","fee": "0.000880000000000000000000", "address": "0xfaEF4bE10dDF50B68c220c9ab19381e20B8EEB2B", "hash": "eece4c17994798939cea9f6a72ee12faa55a7ce44860cfb95c7ed71c89522fe8","status": "pending","type": "payout", "createdAt": "2017-05-18T18:05:36.957Z", "updatedAt": "2017-05-18T19:21:05.370Z" }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/account/transactions", null, GetNoncePayload(), "GET");
            if (result != null && result.HasValues)
            {
                foreach(JToken token in result)
                {
                    if (token["currency"].ToStringInvariant().Equals(symbol))
                    {
                        ExchangeTransaction transaction = new ExchangeTransaction
                        {
                            PaymentId = token["id"].ToStringInvariant(),
                            Symbol = token["currency"].ToStringInvariant(),
                            Address = token["address"].ToStringInvariant(),               // Address Tag isn't returned
                            BlockchainTxId = token["hash"].ToStringInvariant(),           // not sure about this
                            Amount = token["amount"].ConvertInvariant<decimal>(),
                            Notes = token["type"].ToStringInvariant(),                    // since no notes are returned, we'll use this to show the transaction type
                            TxFee = token["fee"].ConvertInvariant<decimal>(),
                            TimestampUTC = token["createdAt"].ConvertInvariant<DateTime>()
                        };

                        string status = token["status"].ToStringInvariant();
                        if (status.Equals("pending")) transaction.Status = TransactionStatus.Processing;
                        else if (status.Equals("success")) transaction.Status = TransactionStatus.Complete;
                        else if (status.Equals("failed")) transaction.Status = TransactionStatus.Failure;
                        else transaction.Status = TransactionStatus.Unknown;

                        transactions.Add(transaction);
                    }
                }
            }
            return transactions;
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse withdraw = new ExchangeWithdrawalResponse() { Success = false }; 
            var payload = GetNoncePayload();
            payload["amount"] = withdrawalRequest.Amount;
            payload["currency_code"] = withdrawalRequest.Symbol;
            payload["address"] = withdrawalRequest.Address;
            if (!string.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload["paymentId"] = withdrawalRequest.AddressTag;
            //{ "id": "d2ce578f-647d-4fa0-b1aa-4a27e5ee597b"}   that's all folks!
            JToken token = await MakeJsonRequestAsync<JToken>("/payment/payout", null, payload, "POST");
            if (token != null && token["id"] != null)
            {
                withdraw.Success = true;
                withdraw.Id = token["id"].ToStringInvariant();
            }
            return withdraw;
        }

        #endregion

        #region WebSocket APIs

        // working on it. Hitbtc has extensive support for sockets, including trading

        #endregion

        #region Hitbtc Public Functions outside the ExchangeAPI
        // HitBTC has two accounts per client: the main bank and trading 
        // Coins deposited from this API go into the bank, and must be withdrawn from there as well
        // Trading only takes place from the trading account.
        // You must transfer coin balances from the bank to trading in order to trade, and back again to withdaw
        // These functions aid in that process

        public Dictionary<string, decimal> GetBankAmounts()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JToken obj = MakeJsonRequest<JToken>("/account/balance", null, GetNoncePayload(), "GET");
            foreach (JToken token in obj["balance"])
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }


        public bool AccountTransfer(string Symbol, decimal Amount, bool ToBank)
        {
            var payload = GetNoncePayload();
            payload["type"] = ToBank ? "exchangeToBank" : "bankToExchange";
            payload["currency"] = Symbol;
            payload["amount"] = Amount;
            JToken obj = MakeJsonRequest<JToken>("/account/transfer", null, GetNoncePayload(), "GET");
            if (obj != null && obj.HasValues && !String.IsNullOrEmpty(obj["id"].ToStringInvariant())) return true;
            else return false;
        }

        #endregion

        #region Private Functions

        private ExchangeTicker ParseTicker(JToken token, string symbol)
        {
            // [ {"ask": "0.050043","bid": "0.050042","last": "0.050042","open": "0.047800","low": "0.047052","high": "0.051679","volume": "36456.720","volumeQuote": "1782.625000","timestamp": "2017-05-12T14:57:19.999Z","symbol": "ETHBTC"} ]
            return new ExchangeTicker()
            {
                Ask = token["ask"].ConvertInvariant<decimal>(),
                Bid = token["bid"].ConvertInvariant<decimal>(),
                Last = token["last"].ConvertInvariant<decimal>(),
                Volume = new ExchangeVolume()
                {
                    Timestamp = token["timestamp"].ConvertInvariant<DateTime>(),
                    PriceAmount = token["volumeQuote"].ConvertInvariant<decimal>(),
                    QuantityAmount = token["volume"].ConvertInvariant<decimal>()
                }
            };
        }

        private ExchangeTrade ParseExchangeTrade(JToken token)
        {
            // [ { "id": 9533117, "price": "0.046001", "quantity": "0.220", "side": "sell", "timestamp": "2017-04-14T12:18:40.426Z" }, ... ]
            return new ExchangeTrade()
            {
                Id = token["id"].ConvertInvariant<long>(),
                Timestamp = token["timestamp"].ConvertInvariant<DateTime>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy")
            };
        }

        private ExchangeOrderResult ParseCompletedOrder(JToken token)
        {
            //[ { "id": 9535486, "clientOrderId": "f8dbaab336d44d5ba3ff578098a68454", "orderId": 816088377, "symbol": "ETHBTC", "side": "sell", "quantity": "0.061", "price": "0.045487", "fee": "0.000002775", "timestamp": "2017-05-17T12:32:57.848Z" }, 
            return new ExchangeOrderResult()
            {
                OrderId = token["orderId"].ToStringInvariant(),          // here we're using OrderId. I have no idea what the id field is used for.
                Symbol = token["symbol"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                AmountFilled = token["quantity"].ConvertInvariant<decimal>(),   // these are closed, so I guess the filled quantity matches the order quantiity
                Price = token["price"].ConvertInvariant<decimal>(),
                Fees = token["fee"].ConvertInvariant<decimal>(),
                OrderDate = token["timestamp"].ConvertInvariant<DateTime>(),
                Result = ExchangeAPIOrderResult.Filled
            };
        }

        private ExchangeOrderResult ParseOpenOrder(JToken token)
        {
            // [ { "id": 840450210, "clientOrderId": "c1837634ef81472a9cd13c81e7b91401", "symbol": "ETHBTC", "side": "buy", "status": "partiallyFilled", "type": "limit", "timeInForce": "GTC", "quantity": "0.020", "price": "0.046001", "cumQuantity": "0.005", "createdAt": "2017-05-12T17:17:57.437Z",   "updatedAt": "2017-05-12T17:18:08.610Z" }]
            ExchangeOrderResult result = new ExchangeOrderResult()
            {
                OrderId = token["clientOrderId"].ToStringInvariant(),        // here we're using ClientOrderId in order to get order details by open orders
                Symbol = token["symbol"].ToStringInvariant(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                Amount = token["quantity"].ConvertInvariant<decimal>(),
                AmountFilled = token["cumQuantity"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                OrderDate = token["createdAt"].ConvertInvariant<DateTime>(),
                Message = string.Format("OrderType: {0}, TimeInForce: {1}", token["type"].ToStringInvariant(), token["timeInForce"].ToStringInvariant())   // A bit arbitrary, but this will show the ordertype and timeinforce
            };
            // new, suspended, partiallyFilled, filled, canceled, expired
            string status = token["status"].ToStringInvariant();
            if (status.Equals("filled")) result.Result = ExchangeAPIOrderResult.Filled;
            else if (status.Equals("partiallyFilled")) result.Result = ExchangeAPIOrderResult.FilledPartially;
            else if (status.Equals("canceled") || status.Equals("expired")) result.Result = ExchangeAPIOrderResult.Canceled;
            else if (status.Equals("new")) result.Result = ExchangeAPIOrderResult.Pending;
            else result.Result = ExchangeAPIOrderResult.Error;

            return result;
        }

        #endregion
    }
}
