using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{

    public partial class ExchangeDigifinexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://openapi.digifinex.vip/v3";
        public override string BaseUrlWebSocket { get; set; } = "wss://openapi.digifinex.com/ws/v1/";
        int websocketMessageId = 0;
        string timeWindow;

        public ExchangeDigifinexAPI()
        {
            MarketSymbolSeparator = "_";
            MarketSymbolIsReversed = false;
            MarketSymbolIsUppercase = false;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
            NonceStyle = NonceStyle.UnixSeconds;
        }

        #region ProcessRequest 

        protected override async Task OnGetNonceOffset()
        {
            try
            {
                var start = CryptoUtility.UtcNow;
                JToken token = await MakeJsonRequestAsync<JToken>("/time");
                DateTime serverDate = DateTimeOffset.FromUnixTimeSeconds((long)token["server_time"]).UtcDateTime;
                var end = CryptoUtility.UtcNow;
                var now = start + TimeSpan.FromMilliseconds((end - start).TotalMilliseconds / 2);
                var timeFaster = now - serverDate;
                if (timeFaster <= TimeSpan.Zero)
                {
                    timeWindow = (timeFaster.Negate().TotalSeconds*10).ToString();
                    NonceOffset = TimeSpan.FromSeconds(2.5);
                }
                else
                    NonceOffset = now - serverDate; // how much time to substract from Nonce when making a request
            }
            catch (Exception)
            { }
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
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
                request.AddHeader("ACCESS-TIMESTAMP", nonce.ToString());
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

        ExchangeMarket ParseSymbol(JToken x)
        {
            var symbol = (string)x["market"];
            var (baseCurrency, quoteCurrency) = ExchangeMarketSymbolToCurrencies(symbol);
            return new ExchangeMarket
            {
                IsActive = true,
                MarketSymbol = symbol,
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                PriceStepSize = new decimal(1, 0, 0, false, (byte)x["price_precision"]),
                QuantityStepSize = new decimal(1, 0, 0, false, (byte)x["volume_precision"]),
                MinTradeSize = (decimal)x["min_volume"],
                MinTradeSizeInQuoteCurrency = (decimal)x["min_amount"],
            };
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("markets");
            return obj["data"].Select(x => ParseSymbol(x));
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await GetMarketSymbolsMetadataAsync()).Select(x => x.MarketSymbol);
        }


        ExchangeTicker ParseTicker(JToken x)
        {
            var t = x["ticker"][0];
            var symbol = (string)t["symbol"];
            var (baseCurrency, quoteCurrency) = ExchangeMarketSymbolToCurrencies(symbol);

            return new ExchangeTicker
            {
                Ask = (decimal)t["sell"],
                Bid = (decimal)t["buy"],
                Last = (decimal)t["last"],
                MarketSymbol = (string)t["symbol"],
                Volume = new ExchangeVolume
                {
                    BaseCurrency = baseCurrency,
                    QuoteCurrency = quoteCurrency,
                    QuoteCurrencyVolume = (decimal)t["base_vol"],
                    BaseCurrencyVolume = (decimal)t["vol"],
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)x["date"]).LocalDateTime,
                },
            };
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>($"/ticker?symbol={marketSymbol}");
            return ParseTicker(obj);
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>($"/order_book?symbol={marketSymbol}&limit={maxCount}");
            var result = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(obj, sequence: "date", maxCount: maxCount);
            result.LastUpdatedUtc = DateTimeOffset.FromUnixTimeSeconds((long)obj["date"]).UtcDateTime;
            result.MarketSymbol = marketSymbol;
            return result;
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>($"/trades?symbol={marketSymbol}&limit=500"); // maximum limit = 500
            return obj["data"].Select(x => new ExchangeTrade
            {
                Id = (string)x["id"],
                Amount = (decimal)x["amount"],
                Price = (decimal)x["price"],
                IsBuy = (string)x["type"] != "sell",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)x["date"]).LocalDateTime,
                Flags = (string)x["type"] == "sell" ? default : ExchangeTradeFlags.IsBuy,
            });
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
            string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
                throw new ArgumentException("Non-null limit is not supported", "limit");

            string period;
            if (periodSeconds <= 60 * 720)
                period = (periodSeconds / 60).ToString();
            else if (periodSeconds == 24 * 60 * 60)
                period = "1D";
            else if (periodSeconds == 7 * 24 * 60 * 60)
                period = "1W";
            else
                throw new ArgumentException($"Unsupported periodSeconds: {periodSeconds}", "periodSeconds");

            var url = $"/kline?symbol={marketSymbol}&period={period}";
            if (startDate != null)
                url += $"&start_time={new DateTimeOffset(startDate.Value).ToUnixTimeSeconds()}";
            if (endDate != null)
                url += $"&end_time={new DateTimeOffset(endDate.Value).ToUnixTimeSeconds()}";

            JToken obj = await MakeJsonRequestAsync<JToken>(url);
            return obj["data"].Select(x => new MarketCandle
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)x[0]).LocalDateTime,
                BaseCurrencyVolume = (double)x[1],
                ClosePrice = (decimal)x[2],
                HighPrice = (decimal)x[3],
                LowPrice = (decimal)x[4],
                OpenPrice = (decimal)x[5],
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
                MarketSymbol = (string)x["symbol"],
                OrderId = (string)x["order_id"],
                OrderDate = DateTimeOffset.FromUnixTimeSeconds((long)x["created_date"]).LocalDateTime,
                FillDate = DateTimeOffset.FromUnixTimeSeconds((long)x["finished_date"]).LocalDateTime,
                Price = (decimal)x["price"],
                AveragePrice = (decimal)x["avg_price"],
                Amount = (decimal)x["amount"],
                AmountFilled = (decimal)x["executed_amount"],
                IsBuy = (string)x["type"] == "buy",
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
                url += "&start_time=" + startTime.ToString();
            }

            JToken token = await MakeJsonRequestAsync<JToken>(url, payload: payload);
            var list = token["list"];
            return list.Select(x => new ExchangeOrderResult
            {
                MarketSymbol = (string)x["symbol"],
                OrderId = (string)x["order_id"],
                TradeId = (string)x["id"],
                Price = (decimal)x["price"],
                AmountFilled = (decimal)x["amount"],
                Fees = (decimal)x["fee"],
                FeesCurrency = (string)x["fee_currency"],
                FillDate = DateTimeOffset.FromUnixTimeSeconds((long)x["timestamp"]).LocalDateTime,
                IsBuy = (string)x["side"] == "buy",
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
                MarketSymbol = (string)x["symbol"],
                OrderId = (string)x["order_id"],
                OrderDate = DateTimeOffset.FromUnixTimeSeconds((long)x["created_date"]).LocalDateTime,
                FillDate = DateTimeOffset.FromUnixTimeSeconds((long)x["finished_date"]).LocalDateTime,
                Price = (decimal)x["price"],
                AveragePrice = (decimal)x["avg_price"],
                Amount = (decimal)x["amount"],
                AmountFilled = (decimal)x["executed_amount"],
                IsBuy = (string)x["type"] == "buy",
                Result = ParseOrderStatus(x["status"]),
            };
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>("/spot/assets", payload: payload);
            var list = token["list"];
            return list.Where(x => (decimal)x["total"] != 0).ToDictionary(x => (string)x["currency"], x => (decimal)x["total"]);
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>("/spot/assets", payload: payload);
            var list = token["list"];
            return list.Where(x => (decimal)x["free"] != 0).ToDictionary(x => (string)x["currency"], x => (decimal)x["free"]);
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
            return new ExchangeOrderResult { OrderId = (string)token["order_id"] };
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

        protected override IWebSocket OnGetTradesWebSocket(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
            if (callback == null)
                return null;
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
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
                JToken token = JToken.Parse(CryptoUtility.DecompressDeflate((new ArraySegment<byte>(msg, 2, msg.Length-2)).ToArray()).ToStringFromUTF8());
                if (token["method"].ToStringInvariant() == "trades.update")
                {
                    var args = token["params"];
                    var clean = (bool)args[0];
                    var trades = args[1];
                    var symbol = (string)args[2];

                    var x = trades as JArray;
                    for (int i=0; i<x.Count; i++)
                    {
                        var trade = x[i];
                        var isbuy = (string)trade["type"] != "sell";
                        var flags = default(ExchangeTradeFlags);
                        if (isbuy)
                            flags |= ExchangeTradeFlags.IsBuy;
                        if (clean)
                            flags |= ExchangeTradeFlags.IsFromSnapshot;
                        if (i == x.Count - 1)
                            flags |= ExchangeTradeFlags.IsLastFromSnapshot;

                        await callback.Invoke(new KeyValuePair<string, ExchangeTrade>(
                            symbol, new ExchangeTrade
                            {
                                Id = (string)trade["id"],
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(0).AddSeconds((double)trade["time"]).LocalDateTime,
                                Price = (decimal)trade["price"],
                                Amount = (decimal)trade["amount"],
                                IsBuy = isbuy,
                                Flags = flags,
                            }));
                    }
                }
            }, async (_socket) =>
            {
                var id = Interlocked.Increment(ref websocketMessageId);
                await _socket.SendMessageAsync(new { id, method = "trades.subscribe", @params = marketSymbols } );
            });
        }

        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            if (callback == null)
                return null;
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
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
                JToken token = JToken.Parse(CryptoUtility.DecompressDeflate((new ArraySegment<byte>(msg, 2, msg.Length-2)).ToArray()).ToStringFromUTF8());
                if (token["method"].ToStringInvariant() == "depth.update")
                {
                    var args = token["params"];
                    var data = args[1];
                    var book = new ExchangeOrderBook { LastUpdatedUtc = DateTime.UtcNow, MarketSymbol = (string)args[2] };
                    foreach (var x in data["asks"])
                    {
                        var price = (decimal)x[0];
                        book.Asks[price] = new ExchangeOrderPrice { Price = price, Amount = (decimal)x[1] };
                    }
                    foreach (var x in data["bids"])
                    {
                        var price = (decimal)x[0];
                        book.Bids[price] = new ExchangeOrderPrice { Price = price, Amount = (decimal)x[1] };
                    }
                    callback(book);
                }
            }, async (_socket) =>
            {
                var id = Interlocked.Increment(ref websocketMessageId);
                await _socket.SendMessageAsync(new { id, method = "depth.subscribe", @params = marketSymbols });
            });
        }

        #endregion
    }
}
