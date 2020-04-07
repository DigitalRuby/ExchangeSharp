using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{

    public partial class ExchangeDigifinexAPI : ExchangeAPI
    {
        string[] Urls =
        {
            "openapi.digifinex.com",
            "openapi.digifinex.vip",
            "openapi.digifinex.xyz",
        };
        string fastestUrl = null;
        int failedUrlCount;
		int successUrlCount;

        public override string BaseUrl { get; set; } = "https://openapi.digifinex.vip/v3";
        public override string BaseUrlWebSocket { get; set; } = "wss://openapi.digifinex.vip/ws/v1/";
        int websocketMessageId = 0;
        string timeWindow;
        TaskCompletionSource<int> inited = new TaskCompletionSource<int>();

        public ExchangeDigifinexAPI()
        {
            MarketSymbolSeparator = "_";
            MarketSymbolIsReversed = false;
            MarketSymbolIsUppercase = true;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
            NonceStyle = NonceStyle.UnixSeconds;
            RateLimit = new RateGate(240, TimeSpan.FromMinutes(1));
            GetFastestUrl();
        }

        void GetFastestUrl()
        {
            var client = new HttpClient();
            foreach (var url in Urls)
            {
                var u = url;
                client.GetAsync($"https://{u}").ContinueWith((t) =>
                {
                    if (t.Exception != null)
                    {
                        var count = Interlocked.Increment(ref failedUrlCount);
                        if (count == Urls.Length)
                            inited.SetException(new APIException("All digifinex URLs failed."));
                        return;
                    }
					if (Interlocked.Increment(ref successUrlCount) == 1)
                    {
                        fastestUrl = u;
                        //Console.WriteLine($"Fastest url {GetHashCode()}: {u}");
                        BaseUrl = $"https://{u}/v3";
                        BaseUrlWebSocket = $"wss://{u}/ws/v1/";
                        inited.SetResult(1);
                    }
                });
            }
        }

        #region ProcessRequest

        protected override async Task OnGetNonceOffset()
        {
            try
            {
                await inited.Task;
                var start = CryptoUtility.UtcNow;
                JToken token = await MakeJsonRequestAsync<JToken>("/time");
                DateTime serverDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(token["server_time"].ConvertInvariant<long>());
                var end = CryptoUtility.UtcNow;
                var now = start + TimeSpan.FromMilliseconds((end - start).TotalMilliseconds);
                var timeFaster = now - serverDate;
                timeWindow = "30"; // max latency of 30s
                NonceOffset = now - serverDate; // how much time to substract from Nonce when making a request
                //Console.WriteLine($"NonceOffset {GetHashCode()}: {NonceOffset}");
            }
            catch
            {
                throw;
            }
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            await inited.Task;
            var query = request.RequestUri.Query.TrimStart('?');
            if (CanMakeAuthenticatedRequest(payload))
            {
                var nonce = payload["nonce"];
                payload.Remove("nonce");
                var body = string.Empty;
                if (payload.Count > 0)
                {
                    body = CryptoUtility.GetFormForPayload(payload);
                    if (query.Length > 0)
                        query += '&';
                    query += body;
                }
                string signature = CryptoUtility.SHA256Sign(query, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));
                request.AddHeader("ACCESS-KEY", PublicApiKey.ToUnsecureString());
                request.AddHeader("ACCESS-SIGN", signature);
                request.AddHeader("ACCESS-TIMESTAMP", nonce.ToStringInvariant());
                if (timeWindow != null)
                    request.AddHeader("ACCESS-RECV-WINDOW", timeWindow);

                if (request.Method == "POST")
                {
                    await CryptoUtility.WriteToRequestAsync(request, body);
                }
            }
        }

        protected override JToken CheckJsonResponse(JToken result)
        {
            if ((int)result["code"] != 0)
            {
                throw new APIException(result.ToStringInvariant());
            }
            //var resultKeys = new string[] { "result", "data", "return", "list" };
            //foreach (string key in resultKeys)
            //{
            //    JToken possibleResult = result[key];
            //    if (possibleResult != null && (possibleResult.Type == JTokenType.Object || possibleResult.Type == JTokenType.Array))
            //    {
            //        result = possibleResult;
            //        break;
            //    }
            //}
            return result;
        }

        #endregion

        #region Public APIs

        private async Task<ExchangeMarket> ParseExchangeMarketAsync(JToken x)
        {
            var symbol = x["market"].ToStringUpperInvariant();
            var (baseCurrency, quoteCurrency) = await ExchangeMarketSymbolToCurrenciesAsync(symbol);
            return new ExchangeMarket
            {
                IsActive = true,
                MarketSymbol = symbol,
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                PriceStepSize = new decimal(1, 0, 0, false, (byte)x["price_precision"]),
                QuantityStepSize = new decimal(1, 0, 0, false, (byte)x["volume_precision"]),
                MinTradeSize = x["min_volume"].ConvertInvariant<decimal>(),
                MinTradeSizeInQuoteCurrency = x["min_amount"].ConvertInvariant<decimal>(),
            };
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            await inited.Task;
            JToken obj = await MakeJsonRequestAsync<JToken>("markets");
            JToken data = obj["data"];
            List<ExchangeMarket> results = new List<ExchangeMarket>();
            foreach (JToken token in data)
            {
                results.Add(await ParseExchangeMarketAsync(token));
            }
            return results;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await GetMarketSymbolsMetadataAsync()).Select(x => x.MarketSymbol);
        }

        private async Task<ExchangeTicker> ParseTickerAsync(JToken x)
        {
            var t = x["ticker"][0];
            var symbol = t["symbol"].ToStringUpperInvariant();
            var (baseCurrency, quoteCurrency) = await ExchangeMarketSymbolToCurrenciesAsync(symbol);

            return new ExchangeTicker
            {
                Ask = t["sell"].ConvertInvariant<decimal>(),
                Bid = t["buy"].ConvertInvariant<decimal>(),
                Last = t["last"].ConvertInvariant<decimal>(),
                MarketSymbol = symbol,
                Volume = new ExchangeVolume
                {
                    BaseCurrency = baseCurrency,
                    QuoteCurrency = quoteCurrency,
                    QuoteCurrencyVolume = t["base_vol"].ConvertInvariant<decimal>(),
                    BaseCurrencyVolume = t["vol"].ConvertInvariant<decimal>(),
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["date"].ConvertInvariant<long>()),
                },
            };
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>($"/ticker?symbol={marketSymbol}");
            return await ParseTickerAsync(obj);
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>($"/order_book?symbol={marketSymbol}&limit={maxCount}");
            var result = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(obj, sequence: "date", maxCount: maxCount);
            result.LastUpdatedUtc = CryptoUtility.UnixTimeStampToDateTimeSeconds(obj["date"].ConvertInvariant<long>());
            result.MarketSymbol = marketSymbol;
            return result;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>($"/trades?symbol={marketSymbol}&limit={limit??500}"); // maximum limit = 500
            return obj["data"].Select(x => new ExchangeTrade
            {
                Id = x["id"].ToStringInvariant(),
                Amount = x["amount"].ConvertInvariant<decimal>(),
                Price = x["price"].ConvertInvariant<decimal>(),
                IsBuy = x["type"].ToStringLowerInvariant() != "sell",
                Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["date"].ConvertInvariant<long>()),
                Flags = x["type"].ToStringLowerInvariant() == "sell" ? default : ExchangeTradeFlags.IsBuy,
            });
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
            string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            string period;
            if (periodSeconds <= 60 * 720)
                period = (periodSeconds / 60).ToStringInvariant();
            else if (periodSeconds == 24 * 60 * 60)
                period = "1D";
            else if (periodSeconds == 7 * 24 * 60 * 60)
                period = "1W";
            else
                throw new ArgumentException($"Unsupported periodSeconds: {periodSeconds}", "periodSeconds");

            var url = $"/kline?symbol={marketSymbol}&period={period}";
            if (startDate != null && endDate != null && limit != null)
                throw new ArgumentException("Cannot specify `startDate`, `endDate` and `limit` all at the same time");
            if (limit != null)
            {
                if (startDate != null)
                    endDate = startDate + TimeSpan.FromSeconds(limit.Value * periodSeconds);
                else
                {
                    if (endDate == null)
                        endDate = DateTime.Now;
                    startDate = endDate - TimeSpan.FromSeconds((limit.Value-1) * periodSeconds);
                }
            }

            if (startDate != null)
                url += $"&start_time={new DateTimeOffset(startDate.Value).ToUnixTimeSeconds()}";
            if (endDate != null)
                url += $"&end_time={new DateTimeOffset(endDate.Value).ToUnixTimeSeconds()}";

            JToken obj = await MakeJsonRequestAsync<JToken>(url);
            return obj["data"].Select(x => new MarketCandle
            {
                Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(x[0].ConvertInvariant<long>()),
                BaseCurrencyVolume = x[1].ConvertInvariant<double>(),
                ClosePrice = x[2].ConvertInvariant<decimal>(),
                HighPrice = x[3].ConvertInvariant<decimal>(),
                LowPrice = x[4].ConvertInvariant<decimal>(),
                OpenPrice = x[5].ConvertInvariant<decimal>(),
            });
        }


        #endregion

        #region Private APIs

        ExchangeAPIOrderResult ParseOrderStatus(JToken token)
        {
            var x = (int)token;
            switch (x)
            {
                case 0:
                    return ExchangeAPIOrderResult.Pending;
                case 1:
                    return ExchangeAPIOrderResult.FilledPartially;
                case 2:
                    return ExchangeAPIOrderResult.Filled;
                case 3:
                    return ExchangeAPIOrderResult.Canceled;
                case 4:
                    return ExchangeAPIOrderResult.FilledPartiallyAndCancelled;
                default:
                    throw new APIException($"Unknown order result type {x}");
            }
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            var url = "/spot/order/current";

            if (marketSymbol?.Length > 0)
                url += "?symbol=" + marketSymbol;

            JToken token = await MakeJsonRequestAsync<JToken>(url, payload: payload);
            var list = token["data"];
            return list.Select(x => new ExchangeOrderResult
            {
                MarketSymbol = x["symbol"].ToStringUpperInvariant(),
                OrderId = x["order_id"].ToStringInvariant(),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["created_date"].ConvertInvariant<long>()),
                FillDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["finished_date"].ConvertInvariant<long>()),
                Price = x["price"].ConvertInvariant<decimal>(),
                AveragePrice = x["avg_price"].ConvertInvariant<decimal>(),
                Amount = x["amount"].ConvertInvariant<decimal>(),
                AmountFilled = x["executed_amount"].ConvertInvariant<decimal>(),
                IsBuy = x["type"].ToStringLowerInvariant() == "buy",
                Result = ParseOrderStatus(x["status"]),
            });
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(
            string marketSymbol = null, DateTime? afterDate = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            var url = "/spot/mytrades?limit=500";

            if (marketSymbol?.Length > 0)
                url += "&symbol=" + marketSymbol;

            if (afterDate != null)
            {
                var startTime = (long)afterDate.Value.UnixTimestampFromDateTimeSeconds();
                url += "&start_time=" + startTime.ToStringInvariant();
            }

            JToken token = await MakeJsonRequestAsync<JToken>(url, payload: payload);
            var list = token["list"];
            return list.Select(x => new ExchangeOrderResult
            {
                MarketSymbol = x["symbol"].ToStringUpperInvariant(),
                OrderId = x["order_id"].ToStringInvariant(),
                TradeId = x["id"].ToStringInvariant(),
                Price = x["price"].ConvertInvariant<decimal>(),
                AmountFilled = x["amount"].ConvertInvariant<decimal>(),
                Fees = x["fee"].ConvertInvariant<decimal>(),
                FeesCurrency = x["fee_currency"].ToStringInvariant(),
                FillDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["timestamp"].ConvertInvariant<long>()),
                IsBuy = x["side"].ToStringLowerInvariant() == "buy",
                Result = ExchangeAPIOrderResult.Unknown,
            });
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/spot/order?order_id={orderId}", payload: payload);
            var x = token["data"];
            return new ExchangeOrderResult
            {
                MarketSymbol = x["symbol"].ToStringUpperInvariant(),
                OrderId = x["order_id"].ToStringInvariant(),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["created_date"].ConvertInvariant<long>()),
                FillDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(x["finished_date"].ConvertInvariant<long>()),
                Price = x["price"].ConvertInvariant<decimal>(),
                AveragePrice = x["avg_price"].ConvertInvariant<decimal>(),
                Amount = x["amount"].ConvertInvariant<decimal>(),
                AmountFilled = x["executed_amount"].ConvertInvariant<decimal>(),
                IsBuy = x["type"].ToStringLowerInvariant() == "buy",
                Result = ParseOrderStatus(x["status"]),
            };
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>("/spot/assets", payload: payload);
            var list = token["list"];
            return list.Where(x => x["total"].ConvertInvariant<decimal>() != 0m).ToDictionary(x => x["currency"].ToStringUpperInvariant(), x => x["total"].ConvertInvariant<decimal>());
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>("/spot/assets", payload: payload);
            var list = token["list"];
            return list.Where(x => x["free"].ConvertInvariant<decimal>() != 0m).ToDictionary(x => x["currency"].ToStringUpperInvariant(), x => x["free"].ConvertInvariant<decimal>());
        }

        string GetOrderType(ExchangeOrderRequest order)
        {
            var result = order.IsBuy ? "buy" : "sell";
            switch (order.OrderType)
            {
                case OrderType.Limit:
                    break;
                case OrderType.Market:
                    result += "_market";
                    break;
                default:
                    throw new ArgumentException($"Unsupported order type `{order.OrderType}`", "OrderType");
            }
            return result;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["symbol"] = order.MarketSymbol;
            payload["type"] = GetOrderType(order);
            payload["price"] = order.Price;
            payload["amount"] = order.Amount;
            var market = order.IsMargin ? "margin" : "spot";
            JToken token = await MakeJsonRequestAsync<JToken>($"/{market}/order/new", payload: payload, requestMethod: "POST");
            return new ExchangeOrderResult { OrderId = token["order_id"].ToStringInvariant() };
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["order_id"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/spot/order/cancel", payload: payload, requestMethod: "POST");
            //{
            //  "code": 0,
            //  "success": [
            //    "198361cecdc65f9c8c9bb2fa68faec40",
            //    "3fb0d98e51c18954f10d439a9cf57de0"
            //  ],
            //  "error": [
            //    "78a7104e3c65cc0c5a212a53e76d0205"
            //  ]
            //}
        }

        #endregion

        #region WebSocket APIs

        protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
            await inited.Task;
            if (callback == null)
            {
                return null;
            }
            else if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
            }
            return await ConnectWebSocketAsync(string.Empty, async (_socket, msg) =>
            {
                // {
                //    "method": "trades.update",
                //    "params":
                //        [
                //             true,
                //             [
                //                 {
                //                 "id": 7172173,
                //                 "time": 1523339279.761838,
                //                 "price": "398.59",
                //                 "amount": "0.027",
                //                 "type": "buy"
                //                 }
                //             ],
                //			 "ETH_USDT"
                //         ],
                //     "id": null
                // }
                JToken token = JToken.Parse(CryptoUtility.DecompressDeflate((new ArraySegment<byte>(msg, 2, msg.Length - 2)).ToArray()).ToStringFromUTF8());
                if (token["method"].ToStringLowerInvariant() == "trades.update")
                {
                    var args = token["params"];
                    var clean = (bool)args[0];
                    var trades = args[1];
                    var symbol = args[2].ToStringUpperInvariant();

                    var x = trades as JArray;
                    for (int i = 0; i < x.Count; i++)
                    {
                        var trade = x[i];
                        var isBuy = trade["type"].ToStringLowerInvariant() != "sell";
                        var flags = default(ExchangeTradeFlags);
                        if (isBuy)
                        {
                            flags |= ExchangeTradeFlags.IsBuy;
                            if (clean)
                            {
                                flags |= ExchangeTradeFlags.IsFromSnapshot;
                                if (i == x.Count - 1)
                                {
                                    flags |= ExchangeTradeFlags.IsLastFromSnapshot;
                                }
                            }
                            await callback.Invoke(new KeyValuePair<string, ExchangeTrade>
                            (
                                symbol,
                                new ExchangeTrade
                                {
                                    Id = trade["id"].ToStringInvariant(),
                                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(0).AddSeconds(trade["time"].ConvertInvariant<double>()),
                                    Price = trade["price"].ConvertInvariant<decimal>(),
                                    Amount = trade["amount"].ConvertInvariant<decimal>(),
                                    IsBuy = isBuy,
                                    Flags = flags,
                                }
                            ));
                        }
                    }
                }
            },
            async (_socket2) =>
            {
                var id = Interlocked.Increment(ref websocketMessageId);
                await _socket2.SendMessageAsync(new { id, method = "trades.subscribe", @params = marketSymbols });
            });
        }

        protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            if (callback == null)
            {
                return null;
            }
            await inited.Task;

            return await ConnectWebSocketAsync(string.Empty, (_socket, msg) =>
            {
                //{
                //  "method": "depth.update",
                //  "params": [
                //    true,
                //    {
                //      "asks": [
                //        [
                //          "10249.68000000",
                //          "0.00200000"
                //        ],
                //        [
                //          "10249.67000000",
                //          "0.00110000"
                //        ]
                //      ],
                //      "bids": [
                //        [
                //          "10249.61000000",
                //          "0.86570000"
                //        ],
                //        [
                //          "10248.44000000",
                //          "1.00190000"
                //        ]
                //      ]
                //    },
                //    "BTC_USDT"
                //  ],
                //  "id": null
                //}
                JToken token = JToken.Parse(CryptoUtility.DecompressDeflate((new ArraySegment<byte>(msg, 2, msg.Length - 2)).ToArray()).ToStringFromUTF8());
                if (token["method"].ToStringLowerInvariant() == "depth.update")
                {
                    var args = token["params"];
                    var data = args[1];
                    var book = new ExchangeOrderBook { LastUpdatedUtc = CryptoUtility.UtcNow, MarketSymbol = args[2].ToStringUpperInvariant() };
                    foreach (var x in data["asks"])
                    {
                        var price = x[0].ConvertInvariant<decimal>();
                        book.Asks[price] = new ExchangeOrderPrice { Price = price, Amount = x[1].ConvertInvariant<decimal>() };
                    }
                    foreach (var x in data["bids"])
                    {
                        var price = x[0].ConvertInvariant<decimal>();
                        book.Bids[price] = new ExchangeOrderPrice { Price = price, Amount = x[1].ConvertInvariant<decimal>() };
                    }
                    callback(book);
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                var id = Interlocked.Increment(ref websocketMessageId);
                await _socket.SendMessageAsync(new { id, method = "depth.subscribe", @params = marketSymbols });
            });
        }

        #endregion
    }

    public partial class ExchangeName { public const string Digifinex = "Digifinex"; }

}
