using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public sealed class ExchangeAbucoinsAPI : ExchangeAPI
    {
        public override string Name => ExchangeName.Abucoins;
        public override string BaseUrl { get; set; } = "https://api.abucoins.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://ws.abucoins.com";

        public ExchangeAbucoinsAPI()
        {
            RequestContentType = "application/json";
        }

        #region ProcessRequest 

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                payload.Remove("nonce");
                string body = GetJsonForPayload(payload);
                string timestamp = ((int)DateTime.UtcNow.UnixTimestampFromDateTimeSeconds()).ToString();
                string msg = timestamp + request.Method + request.RequestUri.PathAndQuery + (request.Method.Equals("POST") ? body : string.Empty);
                string sign = CryptoUtility.SHA256SignBase64(msg, CryptoUtility.SecureStringToBytesBase64Decode(PrivateApiKey));

                request.Headers["AC-ACCESS-KEY"] = CryptoUtility.SecureStringToString(PublicApiKey);
                request.Headers["AC-ACCESS-SIGN"] = sign;
                request.Headers["AC-ACCESS-TIMESTAMP"] = timestamp;
                request.Headers["AC-ACCESS-PASSPHRASE"] = CryptoUtility.SecureStringToString(Passphrase);

                if (request.Method == "POST") WriteFormToRequest(request, body);
            }
        }


        #endregion

        #region Public APIs

        protected override Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            throw new NotSupportedException("Abucoins does not provide data about its currencies via the API");
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/products");
            foreach (JToken token in obj) symbols.Add(token["id"].Value<string>());
            return symbols;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/products");
            const decimal StepSize = 0.00000001m;
            foreach (JToken token in obj)
            {
                markets.Add(new ExchangeMarket
                {
                    MarketName = token["id"].ToStringInvariant(),
                    BaseCurrency = token["base_currency"].ToStringInvariant(),
                    MarketCurrency = token["quote_currency"].ToStringInvariant(),
                    MinTradeSize = token["base_min_size"].ConvertInvariant<decimal>(),
                    MaxTradeSize = token["base_max_size"].ConvertInvariant<decimal>(),
                    QuantityStepSize = token["quote_increment"].ConvertInvariant<decimal>(),
                    MaxPrice = StepSize,
                    MinPrice = StepSize,
                    PriceStepSize = StepSize,
                    IsActive = true
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/products/" + symbol + "/ticker");
            if (!token.HasValues) return null;
            return new ExchangeTicker
            {
                Ask = token["ask"].ConvertInvariant<decimal>(),
                Bid = token["bid"].ConvertInvariant<decimal>(),
                Last = decimal.Parse(token["price"].ToStringLowerInvariant(), System.Globalization.NumberStyles.Float),
                Volume = new ExchangeVolume()
                {
                    PriceAmount = decimal.Parse(token["size"].ToStringLowerInvariant(), NumberStyles.Float),
                    QuantityAmount = token["volume"].ConvertInvariant<decimal>(),
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/ticker");
            if (obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    ExchangeTicker ticker = new ExchangeTicker
                    {
                        Ask = token["ask"].ConvertInvariant<decimal>(),
                        Bid = token["bid"].ConvertInvariant<decimal>(),
                        Volume = new ExchangeVolume()
                    };
                    // sometimes the size is null and sometimes it is returned using exponent notaion and sometimes not. 
                    // We therefore parse as string and convert to decimal with float option
                    if ((string)token["size"] != null) ticker.Volume.PriceAmount = decimal.Parse((string)token["size"], NumberStyles.Float);
                    ticker.Volume.QuantityAmount = token.Value<decimal>("volume");
                    if ((string)token["price"] != null) ticker.Last = decimal.Parse(token.Value<string>("price"), System.Globalization.NumberStyles.Float);

                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(token.Value<string>("product_id"), ticker));
                }
            }
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken token = await MakeJsonRequestAsync<JToken>("/products/" + symbol + "/book?level=" + (maxCount > 50 ? "0" : "2"));
            if (token.HasValues)
            {
                foreach (JArray array in token["bids"]) if (orders.Bids.Count < maxCount) orders.Bids.Add(new ExchangeOrderPrice() { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() });
                foreach (JArray array in token["asks"]) if (orders.Asks.Count < maxCount) orders.Asks.Add(new ExchangeOrderPrice() { Amount = array[1].ConvertInvariant<decimal>(), Price = array[0].ConvertInvariant<decimal>() });
            }
            return orders;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();

            // { "time": "2017-09-21T12:33:03Z", "trade_id": "553794", "price": "14167.99328000", "size": "0.00035000", "side": "buy"}
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/" + symbol + "/trades");
            if (obj.HasValues) foreach (JToken token in obj) trades.Add(parseExchangeTrade(token));
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            long? lastTradeID = null;
            // Abucoins uses a page curser based on trade_id to iterate history. Keep paginating until startDate is reached or we run out of data
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/" + symbol + "/trades");
            if (obj.HasValues)
            {
                foreach (JToken token in obj)
                {
                    ExchangeTrade trade = parseExchangeTrade(token);
                    lastTradeID = trade.Id;
                    if (sinceDateTime != null)
                    {
                        if (trade.Timestamp > sinceDateTime) trades.Add(trade);
                        else
                        {
                            if (callback != null && trades.Count > 0) callback(trades.OrderBy(t => t.Timestamp));
                            return;
                        }
                    }
                    else trades.Add(trade);
                }

                if (callback != null && trades.Count > 0) callback(trades.OrderBy(t => t.Timestamp));
                else return;

                trades.Clear();

                while (true)
                {
                    obj = await MakeJsonRequestAsync<JToken>("/products/" + symbol + "/trades?before=" + lastTradeID);
                    if (obj.HasValues)
                    {
                        foreach (JToken token in obj)
                        {
                            ExchangeTrade trade = parseExchangeTrade(token);
                            lastTradeID = trade.Id;
                            if (sinceDateTime != null)
                            {
                                if (trade.Timestamp > sinceDateTime) trades.Add(trade);
                                else
                                {
                                    if (callback != null && trades.Count > 0) callback(trades.OrderBy(t => t.Timestamp));
                                    return;
                                }
                            }
                            else trades.Add(trade);
                        }
                    }
                    else return;

                    if (callback != null && trades.Count > 0)
                    {
                        callback(trades.OrderBy(t => t.Timestamp));
                        trades.Clear();
                    }
                    else return;
                    await Task.Delay(2000);   // two seconds seems like a lot and unnecessary. The RateGate should time this
                }
            }
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            //if (limit != null) throw new APIException("Limit parameter not supported");  Really? You want to throw an exception instead of ignoring the parm?
            List<MarketCandle> candles = new List<MarketCandle>();

            endDate = endDate ?? DateTime.UtcNow;
            startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));

            //[time, low, high, open, close, volume], ["1505984400","14209.92500000","14209.92500000","14209.92500000","14209.92500000","0.001"]
            JToken obj = await MakeJsonRequestAsync<JToken>("/products/" + symbol + "/candles?granularity=" + periodSeconds + "&start=" + ((DateTime)startDate).ToString("o") + "&end=" + ((DateTime)endDate).ToString("o"));
            if (obj.HasValues)
            {
                foreach (JArray array in obj)
                {
                    candles.Add(new MarketCandle()
                    {
                        ExchangeName = this.Name,
                        Name = symbol,
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(array[0].ConvertInvariant<long>()),
                        PeriodSeconds = periodSeconds,
                        LowPrice = array[1].ConvertInvariant<decimal>(),
                        HighPrice = array[2].ConvertInvariant<decimal>(),
                        OpenPrice = array[3].ConvertInvariant<decimal>(),
                        ClosePrice = array[4].ConvertInvariant<decimal>(),
                        VolumeQuantity = array[5].ConvertInvariant<double>()
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
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, GetNoncePayload(), "GET");
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
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, GetNoncePayload(), "GET");
            foreach (JToken token in array)
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m) amounts[token["currency"].ToStringInvariant()] = amount;
            }
            return amounts;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/orders?orderID", null, GetNoncePayload(), "GET");
            ExchangeOrderResult eor = new ExchangeOrderResult()
            {
                OrderId = token["id"].ToStringInvariant(),
                Amount = token["size"].ConvertInvariant<decimal>(),
                AmountFilled = token["filled_size"].ConvertInvariant<decimal>(),
                AveragePrice = token["price"].ConvertInvariant<decimal>(),
                IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                Symbol = token["product_id"].ToStringInvariant(),
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

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> result = new List<ExchangeOrderResult>();
            JArray array = await MakeJsonRequestAsync<JArray>("/orders?status=done", null, GetNoncePayload(), "GET");
            foreach (var token in array)
            {
                ExchangeOrderResult eor = new ExchangeOrderResult()
                {
                    OrderId = token["id"].ToStringInvariant(),
                    Amount = token["size"].ConvertInvariant<decimal>(),
                    AmountFilled = token["filled_size"].ConvertInvariant<decimal>(),
                    AveragePrice = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["side"].ConvertInvariant<decimal>().Equals("buy"),
                    Symbol = token["product_id"].ToStringInvariant(),
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

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> result = new List<ExchangeOrderResult>();
            JArray array = await MakeJsonRequestAsync<JArray>("/orders?status=open", null, GetNoncePayload(), "GET");
            foreach (var token in array)
            {
                ExchangeOrderResult eor = new ExchangeOrderResult()
                {
                    OrderId = token["id"].ToStringInvariant(),
                    Amount = token["size"].ConvertInvariant<decimal>(),
                    AmountFilled = token["filled_size"].ConvertInvariant<decimal>(),
                    AveragePrice = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                    Symbol = token["product_id"].ToStringInvariant(),
                };

                DateTime dt;
                if (DateTime.TryParse(token["created_at"].ToStringInvariant(), out dt)) eor.OrderDate = dt;
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
            var payload = GetNoncePayload();
            payload["priduct_id"] = order.Symbol;
            payload["side"] = order.IsBuy ? "buy" : "sell";
            payload["size"] = order.Amount;
            if (order.OrderType == OrderType.Limit) payload["price"] = order.Price;
            else payload["type"] = "market";

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
                result.OrderDate = token["created_at"].ConvertInvariant<DateTime>();
                result.Price = token["price"].ConvertInvariant<decimal>();
                result.Symbol = token["product_id"].ToStringInvariant();
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
        protected override async Task OnCancelOrderAsync(string orderId)
        {
            await MakeJsonRequestAsync<JArray>("/orders/" + orderId, null, GetNoncePayload(), "DELETE");
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            List<ExchangeTransaction> deposits = new List<ExchangeTransaction>();

            var payload = GetNoncePayload();
            payload["limit"] = 1000;

            // History by symbol is not supported, so we'll get max and filter the results
            // response fields = deposit_id currency date amount fee status (awaiting-email-confirmation pending complete) url
            JArray token = await MakeJsonRequestAsync<JArray>("/deposits/history", null, payload, "GET");
            if (token != null && token.HasValues)
            {
                ExchangeTransaction deposit = new ExchangeTransaction()
                {
                    Symbol = token["currency"].ToStringInvariant(),
                    Amount = token["amount"].ConvertInvariant<decimal>(),
                    TimestampUTC = token["date"].ConvertInvariant<DateTime>(),
                    PaymentId = token["deposit_id"].ToStringInvariant(),
                    TxFee = token["fee"].ConvertInvariant<decimal>()
                };
                switch (token["status"].ToStringInvariant())
                {
                    case "complete": deposit.Status = TransactionStatus.Complete; break;
                    case "pending": deposit.Status = TransactionStatus.Processing; break;
                    default: deposit.Status = TransactionStatus.AwaitingApproval; break;
                }
                if (deposit.Symbol == symbol) deposits.Add(deposit);
            }
            return deposits;
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            symbol = NormalizeSymbol(symbol);
            var payload = GetNoncePayload();
            JArray array = MakeJsonRequest<JArray>("/payment-methods", null, GetNoncePayload(), "GET");
            if (array != null)
            {
                var rc = array.Where(t => t.Value<string>("currency") == symbol).FirstOrDefault();
                payload = GetNoncePayload();
                payload["currency"] = NormalizeSymbol(symbol);
                payload["method"] = rc.Value<string>("id");

                JToken token = await MakeJsonRequestAsync<JToken>("/deposits/make", null, payload, "POST");
                ExchangeDepositDetails deposit = new ExchangeDepositDetails()
                {
                    Symbol = symbol,
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
            string symbol = NormalizeSymbol(withdrawalRequest.Symbol);
            var payload = GetNoncePayload();
            JArray array = await MakeJsonRequestAsync<JArray>("/payment-methods", null, GetNoncePayload(), "GET");
            if (array != null)
            {
                var rc = array.Where(t => t.Value<string>("currency") == symbol).FirstOrDefault();

                payload = GetNoncePayload();
                payload["amount"] = withdrawalRequest.Amount;
                payload["currency"] = symbol;
                payload["method"] = rc.Value<string>("id");
                if (!String.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload["tag"] = withdrawalRequest.AddressTag;
                // "status": 0,  "message": "Your transaction is pending. Please confirm it via email.",  "payoutId": "65",  "balance": []...
                JToken token = MakeJsonRequest<JToken>("/withdrawals/make", null, payload, "POST");
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
        public override IDisposable GetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback)
        {
            if (callback == null) return null;

            var orders = GetOpenOrderDetails().Select(o => o.OrderId).ToList();
            string ids = JsonConvert.SerializeObject(JArray.FromObject(orders));

            return ConnectWebSocket(string.Empty, (msg, _socket) =>
            {
                try
                {
                    //{"type":"done","time":"2018-02-20T14:59:45Z","product_id":"BTC-PLN","sequence":648771,"price":"39034.08000000","order_id":"277370262","reason":"canceled",  "side":"sell","remaining_size":0.503343}
                    JToken token = JToken.Parse(msg);
                    if ((string)token["type"] == "done")
                    {
                        callback.Invoke(new ExchangeOrderResult()
                        {
                            OrderId = token["order_id"].ToStringInvariant(),
                            Symbol = token["product_id"].ToStringInvariant(),
                            OrderDate = token["time"].ConvertInvariant<DateTime>(),
                            Message = token["reason"].ToStringInvariant(),
                            IsBuy = token["side"].ToStringInvariant().Equals("buy"),
                            Price = token["price"].ConvertInvariant<decimal>()
                        });
                    }
                }
                catch (Exception ex) { Debug.WriteLine(ex.Message); }
            }, (_socket) =>
            {
                // subscribe to done channel
                _socket.SendMessage("{\"type\":\"subscribe\",\"channels\":[{ \"name\":\"done\",\"product_ids\":" + ids + "}]}");
            });
        }

        public override IDisposable GetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers)
        {
            if (tickers == null) return null;

            var symbols = GetTickers().Select(t => t.Key).ToList();
            string ids = JsonConvert.SerializeObject(JArray.FromObject(symbols));

            return ConnectWebSocket(string.Empty, (msg, _socket) =>
            {
                try
                {
                    //{"type": "ticker","trade_id": 20153558,"sequence": 3262786978,"time": "2017-09-02T17:05:49.250000Z","product_id": "BTC-USD","price": "4388.01000000","last_size": "0.03000000","best_bid": "4388","best_ask": "4388.01"}
                    JToken token = JToken.Parse(msg);
                    if ((string)token["type"] == "ticker")
                    {
                        tickers.Invoke(new List<KeyValuePair<string, ExchangeTicker>>
                        {
                            new KeyValuePair<string, ExchangeTicker>(token.Value<string>("product_id"), new ExchangeTicker()
                            {
                                Id = token["trade_id"].ConvertInvariant<long>().ToString(),
                                Last = token["price"].ConvertInvariant<decimal>(),
                                Ask = token["best_ask"].ConvertInvariant<decimal>(),
                                Bid = token["best_bid"].ConvertInvariant<decimal>(),
                                Volume = new ExchangeVolume()
                                {
                                    QuantitySymbol = token["product_id"].ToStringInvariant(),
                                    QuantityAmount = token["last_size"].ConvertInvariant<decimal>(),
                                    Timestamp = token["time"].ConvertInvariant<DateTime>()
                                }
                            })
                        });
                    }
                }
                catch (Exception ex) { Debug.WriteLine(ex.Message); }
            }, (_socket) =>
            {
                // subscribe to ticker channel
                _socket.SendMessage("{\"type\": \"subscribe\", \"channels\": [{ \"name\": \"ticker\", \"product_ids\": " + ids + " }] }");
            });
        }

        #endregion

        #region Private Functions

        private ExchangeTrade parseExchangeTrade(JToken token)
        {
            return new ExchangeTrade()
            {
                Id = token["trade_id"].ConvertInvariant<long>(),
                Timestamp = token["time"].ConvertInvariant<DateTime>(),
                Amount = token["size"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                IsBuy = token["buy"].ToStringLowerInvariant().Equals("buy")
            };
        }

        #endregion

    }
}
