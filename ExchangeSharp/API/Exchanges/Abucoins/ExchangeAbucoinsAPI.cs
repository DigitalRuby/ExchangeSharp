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
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeAbucoinsAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.abucoins.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://ws.abucoins.com";

        public ExchangeAbucoinsAPI()
        {
            RequestContentType = "application/json";
        }

        #region ProcessRequest 

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                payload.Remove("nonce");
                string body = CryptoUtility.GetJsonForPayload(payload);
                string timestamp = ((int)CryptoUtility.UtcNow.UnixTimestampFromDateTimeSeconds()).ToStringInvariant();
                string msg = timestamp + request.Method + request.RequestUri.PathAndQuery + (request.Method.Equals("POST") ? body : string.Empty);
                string sign = CryptoUtility.SHA256SignBase64(msg, CryptoUtility.ToBytesBase64Decode(PrivateApiKey));

                request.AddHeader("AC-ACCESS-KEY", CryptoUtility.ToUnsecureString(PublicApiKey));
                request.AddHeader("AC-ACCESS-SIGN", sign);
                request.AddHeader("AC-ACCESS-TIMESTAMP",  timestamp);
                request.AddHeader("AC-ACCESS-PASSPHRASE", CryptoUtility.ToUnsecureString(Passphrase));

                if (request.Method == "POST")
                {
                    await CryptoUtility.WriteToRequestAsync(request, body);
                }
            }
        }


        #endregion

        #region Public APIs

        protected override Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            throw new NotSupportedException("Abucoins does not provide data about its currencies via the API");
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/products");
            foreach (JToken token in obj)
            {
                symbols.Add(token["id"].ToStringInvariant());
            }
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/products");
            foreach (JToken token in obj)
            {
                markets.Add(new ExchangeMarket
                {
                    MarketSymbol = token["id"].ToStringInvariant(),
                    BaseCurrency = token["base_currency"].ToStringInvariant(),
                    QuoteCurrency = token["quote_currency"].ToStringInvariant(),
                    MinTradeSize = token["base_min_size"].ConvertInvariant<decimal>(),
                    MaxTradeSize = token["base_max_size"].ConvertInvariant<decimal>(),
                    PriceStepSize = token["quote_increment"].ConvertInvariant<decimal>(),
                    MinPrice = token["quote_increment"].ConvertInvariant<decimal>(),
                    IsActive = true
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/ticker");
            return ParseTicker(token, marketSymbol);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/ticker");
            if (obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    string marketSymbol = token["product_id"].ToStringInvariant();
                    ExchangeTicker ticker = ParseTicker(token, marketSymbol);
                    if (ticker != null)
                    {
                        tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker));
                    }
                }
            }
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/book?level=" + (maxCount > 50 ? "0" : "2"));
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, maxCount: maxCount);
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();

            // { "time": "2017-09-21T12:33:03Z", "trade_id": "553794", "price": "14167.99328000", "size": "0.00035000", "side": "buy"}
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/trades");
            if (obj.HasValues) foreach (JToken token in obj) trades.Add(ParseExchangeTrade(token));
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            long? lastTradeId = null;
            JToken obj;
            bool running = true;

            // Abucoins uses a page curser based on trade_id to iterate history. Keep paginating until startDate is reached or we run out of data
            while (running)
            {
                obj = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/trades" + (lastTradeId == null ? string.Empty : "?before=" + lastTradeId));
                if ((running = obj.HasValues))
                {
                    lastTradeId = obj.First()["trade_id"].ConvertInvariant<long>();
                    foreach (JToken token in obj)
                    {
                        ExchangeTrade trade = ParseExchangeTrade(token);
                        if (startDate == null || trade.Timestamp >= startDate)
                        {
                            trades.Add(trade);
                        }
                        else
                        {
                            // sinceDateTime has been passed, no more paging
                            running = false;
                            break;
                        }
                    }
                }
                if (trades.Count != 0 && !callback(trades.OrderBy(t => t.Timestamp)))
                {
                    return;
                }
                trades.Clear();
                await Task.Delay(1000);
            }
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            //if (limit != null) throw new APIException("Limit parameter not supported");  Really? You want to throw an exception instead of ignoring the parm?
            List<MarketCandle> candles = new List<MarketCandle>();

            endDate = endDate ?? CryptoUtility.UtcNow;
            startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));

            //[time, low, high, open, close, volume], ["1505984400","14209.92500000","14209.92500000","14209.92500000","14209.92500000","0.001"]
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/candles?granularity=" + periodSeconds.ToStringInvariant() + "&start=" + ((DateTime)startDate).ToString("o") + "&end=" + ((DateTime)endDate).ToString("o"));
            if (obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    candles.Add(this.ParseCandle(token, marketSymbol, periodSeconds, 3, 2, 1, 4, 0, TimestampType.UnixSeconds, 5));
                }
            }
            return candles;
        }

        #endregion

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, await GetNoncePayloadAsync());
            foreach (JToken token in array)
            {
                decimal amount = token["balance"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, await GetNoncePayloadAsync());
            foreach (JToken token in array)
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/orders?orderID", null, await GetNoncePayloadAsync());
            ExchangeOrderResult eor = new ExchangeOrderResult()
            {
                OrderId = token["id"].ToStringInvariant(),
                Amount = token["size"].ConvertInvariant<decimal>(),
                AmountFilled = token["filled_size"].ConvertInvariant<decimal>(),
                AveragePrice = token["price"].ConvertInvariant<decimal>(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                MarketSymbol = token["product_id"].ToStringInvariant(),
            };

            if (DateTime.TryParse(token["created_at"].ToStringInvariant(), out DateTime dt)) eor.OrderDate = dt;
            if (token["status"].ToStringInvariant().Equals("open")) eor.Result = ExchangeAPIOrderResult.Pending;
            else
            {
                if (eor.Amount == eor.AmountFilled) eor.Result = ExchangeAPIOrderResult.Filled;
                else if (eor.Amount < eor.AmountFilled) eor.Result = ExchangeAPIOrderResult.FilledPartially;
                else eor.Result = ExchangeAPIOrderResult.Unknown;
            }
            return eor;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> result = new List<ExchangeOrderResult>();
            JArray array = await MakeJsonRequestAsync<JArray>("/orders?status=done", null, await GetNoncePayloadAsync());
            foreach (var token in array)
            {
                ExchangeOrderResult eor = new ExchangeOrderResult()
                {
                    OrderId = token["id"].ToStringInvariant(),
                    Amount = token["size"].ConvertInvariant<decimal>(),
                    AmountFilled = token["filled_size"].ConvertInvariant<decimal>(),
                    AveragePrice = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["side"].ConvertInvariant<decimal>().Equals("buy"),
                    MarketSymbol = token["product_id"].ToStringInvariant(),
                };

                if (DateTime.TryParse(token["created_at"].ToStringInvariant(), out DateTime dt)) eor.OrderDate = dt;
                if (token["status"].ToStringInvariant().Equals("open")) eor.Result = ExchangeAPIOrderResult.Pending;
                else
                {
                    if (eor.Amount == eor.AmountFilled) eor.Result = ExchangeAPIOrderResult.Filled;
                    else if (eor.Amount < eor.AmountFilled) eor.Result = ExchangeAPIOrderResult.FilledPartially;
                    else eor.Result = ExchangeAPIOrderResult.Unknown;
                }
                result.Add(eor);
            }
            return result;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> result = new List<ExchangeOrderResult>();
            JArray array = await MakeJsonRequestAsync<JArray>("/orders?status=open", null, await GetNoncePayloadAsync());
            foreach (var token in array)
            {
                ExchangeOrderResult eor = new ExchangeOrderResult()
                {
                    OrderId = token["id"].ToStringInvariant(),
                    Amount = token["size"].ConvertInvariant<decimal>(),
                    AmountFilled = token["filled_size"].ConvertInvariant<decimal>(),
                    AveragePrice = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                    MarketSymbol = token["product_id"].ToStringInvariant(),
                };

                if (DateTime.TryParse(token["created_at"].ToStringInvariant(), out DateTime dt)) eor.OrderDate = dt;
                if (token["status"].ToStringInvariant().Equals("open")) eor.Result = ExchangeAPIOrderResult.Pending;
                else
                {
                    if (eor.Amount == eor.AmountFilled) eor.Result = ExchangeAPIOrderResult.Filled;
                    else if (eor.Amount < eor.AmountFilled) eor.Result = ExchangeAPIOrderResult.FilledPartially;
                    else eor.Result = ExchangeAPIOrderResult.Unknown;
                }
                result.Add(eor);
            }
            return result;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            ExchangeOrderResult result = new ExchangeOrderResult() { Result = ExchangeAPIOrderResult.Error };
            var payload = await GetNoncePayloadAsync();
            payload["priduct_id"] = order.MarketSymbol;
            payload["side"] = order.IsBuy ? "buy" : "sell";
            payload["size"] = order.Amount;
            if (order.OrderType == OrderType.Limit)
            {
                payload["price"] = order.Price;
            }
            else
            {
                payload["type"] = "market";
            }
            order.ExtraParameters.CopyTo(payload);

            // {"product_id":"ZEC-BTC","used":"17.3124", "size":"17.3124", "price":"0.035582569", "id":"4217215", "side":"buy", "type":"limit", "time_in_force":"GTT", "post_only":false, "created_at":"2017-11-15T12:41:58Z","filled_size":"17.3124", "fill_fees":"0", "executed_value":"0.61601967", "status":"done", "settled":true, "hidden":false }
            // status (pending, open, done, rejected)
            JToken token = await MakeJsonRequestAsync<JToken>("/orders", null, payload, "POST");
            if (token != null && token.HasValues)
            {
                result.OrderId = token["id"].ToStringInvariant();
                result.Amount = token["size"].ConvertInvariant<decimal>();
                result.AmountFilled = token["filled_size"].ConvertInvariant<decimal>();
                result.AveragePrice = token["price"].ConvertInvariant<decimal>();
                result.Fees = token["fill_fees"].ConvertInvariant<decimal>();
                result.IsBuy = token["buy"].ToStringInvariant().Equals("buy");
                result.OrderDate = token["created_at"].ToDateTimeInvariant();
                result.Price = token["price"].ConvertInvariant<decimal>();
                result.MarketSymbol = token["product_id"].ToStringInvariant();
                result.Message = token["reject_reason"].ToStringInvariant();
                switch (token["status"].ToStringInvariant())
                {
                    case "done": result.Result = ExchangeAPIOrderResult.Filled; break;
                    case "pending":
                    case "open": result.Result = ExchangeAPIOrderResult.Pending; break;
                    case "rejected": result.Result = ExchangeAPIOrderResult.Error; break;
                    default: result.Result = ExchangeAPIOrderResult.Unknown; break;
                }
                return result;
            }
            return result;
        }

        // This should have a return value for success
        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            await MakeJsonRequestAsync<JArray>("/orders/" + orderId, null, await GetNoncePayloadAsync(), "DELETE");
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
        {
            List<ExchangeTransaction> deposits = new List<ExchangeTransaction>();

            var payload = await GetNoncePayloadAsync();
            payload["limit"] = 1000;

            // History by symbol is not supported, so we'll get max and filter the results
            // response fields = deposit_id currency date amount fee status (awaiting-email-confirmation pending complete) url
            JArray token = await MakeJsonRequestAsync<JArray>("/deposits/history", null, payload);
            if (token != null && token.HasValues)
            {
                ExchangeTransaction deposit = new ExchangeTransaction()
                {
                    Currency = token["currency"].ToStringInvariant(),
                    Amount = token["amount"].ConvertInvariant<decimal>(),
                    Timestamp = token["date"].ToDateTimeInvariant(),
                    PaymentId = token["deposit_id"].ToStringInvariant(),
                    TxFee = token["fee"].ConvertInvariant<decimal>()
                };
                switch (token["status"].ToStringInvariant())
                {
                    case "complete": deposit.Status = TransactionStatus.Complete; break;
                    case "pending": deposit.Status = TransactionStatus.Processing; break;
                    default: deposit.Status = TransactionStatus.AwaitingApproval; break;
                }
                if (deposit.Currency == currency) deposits.Add(deposit);
            }
            return deposits;
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            var payload = await GetNoncePayloadAsync();
            JArray array = await MakeJsonRequestAsync<JArray>("/payment-methods", null, await GetNoncePayloadAsync());
            if (array != null)
            {
                var rc = array.Where(t => t["currency"].ToStringInvariant() == currency).FirstOrDefault();
                payload = await GetNoncePayloadAsync();
                payload["currency"] = currency;
                payload["method"] = rc["id"].ToStringInvariant();

                JToken token = await MakeJsonRequestAsync<JToken>("/deposits/make", null, payload, "POST");
                ExchangeDepositDetails deposit = new ExchangeDepositDetails()
                {
                    Currency = currency,
                    Address = token["address"].ToStringInvariant(),
                    AddressTag = token["tag"].ToStringInvariant()
                };
                return deposit;
            }
            return null;
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = false };
            var payload = await GetNoncePayloadAsync();
            JArray array = await MakeJsonRequestAsync<JArray>("/payment-methods", null, await GetNoncePayloadAsync());
            if (array != null)
            {
                var rc = array.Where(t => t["currency"].ToStringInvariant() == withdrawalRequest.Currency).FirstOrDefault();

                payload = await GetNoncePayloadAsync();
                payload["amount"] = withdrawalRequest.Amount;
                payload["currency"] = withdrawalRequest.Currency;
                payload["method"] = rc["id"].ToStringInvariant();
                if (!String.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload["tag"] = withdrawalRequest.AddressTag;
                // "status": 0,  "message": "Your transaction is pending. Please confirm it via email.",  "payoutId": "65",  "balance": []...
                JToken token = await MakeJsonRequestAsync<JToken>("/withdrawals/make", null, payload, "POST");
                response.Id = token["payoutId"].ToStringInvariant();
                response.Message = token["message"].ToStringInvariant();
                response.Success = token["status"].ConvertInvariant<int>().Equals(0);
            }
            return response;
        }

        #endregion

        #region WebSocket APIs

        /// <summary>
        /// Abucoins Order Subscriptions require Order IDs to subscribe to, and will stop feeds when they are completed
        /// So with each new order, a new subscription is required. 
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        protected override IWebSocket OnGetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback)
        {
            var orders = GetOpenOrderDetailsAsync().Sync().Select(o => o.OrderId).ToList();
            string ids = JsonConvert.SerializeObject(JArray.FromObject(orders));

            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                //{"type":"done","time":"2018-02-20T14:59:45Z","product_id":"BTC-PLN","sequence":648771,"price":"39034.08000000","order_id":"277370262","reason":"canceled",  "side":"sell","remaining_size":0.503343}
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if ((string)token["type"] == "done")
                {
                    callback.Invoke(new ExchangeOrderResult()
                    {
                        OrderId = token["order_id"].ToStringInvariant(),
                        MarketSymbol = token["product_id"].ToStringInvariant(),
                        OrderDate = token["time"].ToDateTimeInvariant(),
                        Message = token["reason"].ToStringInvariant(),
                        IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                        Price = token["price"].ConvertInvariant<decimal>()
                    });
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                // subscribe to done channel
                await _socket.SendMessageAsync(new { type = "subscribe", channels = new object[] { new { name = "done", products_ids = ids } } });
            });
        }

        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers, params string[] marketSymbols)
        {
            if (tickers == null) return null;
            marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? GetTickersAsync().Sync().Select(t => t.Key).ToArray() : marketSymbols;
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                //{"type": "ticker","trade_id": 20153558,"sequence": 3262786978,"time": "2017-09-02T17:05:49.250000Z","product_id": "BTC-USD","price": "4388.01000000","last_size": "0.03000000","best_bid": "4388","best_ask": "4388.01"}
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token["type"].ToStringInvariant() == "ticker")
                {
                    string marketSymbol = token["product_id"].ToStringInvariant();
                    tickers.Invoke(new List<KeyValuePair<string, ExchangeTicker>>
                    {
                        new KeyValuePair<string, ExchangeTicker>(marketSymbol, this.ParseTicker(token, marketSymbol, "best_ask", "best_bid", "price", "last_size", null, "time", TimestampType.Iso8601))
                    });
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                await _socket.SendMessageAsync(new { type = "subscribe", channels = new object[] { new { name = "ticker", product_ids = marketSymbols } } });
            });
        }

		protected override IWebSocket OnGetTradesWebSocket(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			if (callback == null) return null;
			marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? GetTickersAsync().Sync().Select(t => t.Key).ToArray() : marketSymbols;
			return ConnectWebSocket(string.Empty, async (_socket, msg) =>
			{
				// {
				//   "type":"match",
				//   "time":"2018-02-20T15:36:15Z",
				//   "product_id":"BTC-PLN",
				//   "sequence":668529,
				//   "trade_id":"1941271",
				//   "maker_order_id":"277417306",
				//   "taker_order_id":"277443754",
				//   "size":0.00093456,
				//   "price":38892.84,
				//   "side":"buy"
				// }
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token["type"].ToStringInvariant() == "match")
				{
					string marketSymbol = token["product_id"].ToStringInvariant();
					await callback.Invoke(new KeyValuePair<string, ExchangeTrade>(
						marketSymbol, token.ParseTrade(amountKey: "size", priceKey: "price", typeKey: "side",
						timestampKey: "time", timestampType: TimestampType.Iso8601, idKey: "trade_id")));
				}
			}, async (_socket) =>
			{
				await _socket.SendMessageAsync(new { type = "subscribe", channels = new object[] { new { name = "matches", product_ids = marketSymbols } } });
			});
		}
		#endregion

		#region Private Functions

		private ExchangeTrade ParseExchangeTrade(JToken token)
        {
            return token.ParseTrade("size", "price", "buy", "time", TimestampType.Iso8601, "trade_id");
        }

        private ExchangeTicker ParseTicker(JToken token, string symbol)
        {
            return this.ParseTicker(token, symbol, "ask", "bid", "price", "size");
        }

        #endregion
    }

    public partial class ExchangeName { public const string Abucoins = "Abucoins"; }
}
