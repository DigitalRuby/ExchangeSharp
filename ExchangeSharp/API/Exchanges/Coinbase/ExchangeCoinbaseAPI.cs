/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    using ExchangeSharp.Coinbase;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    public sealed partial class ExchangeCoinbaseAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.pro.coinbase.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://ws-feed.pro.coinbase.com";

        /// <summary>
        /// The response will also contain a CB-AFTER header which will return the cursor id to use in your next request for the page after this one. The page after is an older page and not one that happened after this one in chronological time.
        /// </summary>
        private string cursorAfter;

        /// <summary>
        /// The response will contain a CB-BEFORE header which will return the cursor id to use in your next request for the page before the current one. The page before is a newer page and not one that happened before in chronological time.
        /// </summary>
        private string cursorBefore;
         
        private ExchangeOrderResult ParseFill(JToken result)
        {
            decimal amount = result["size"].ConvertInvariant<decimal>();
            decimal price = result["price"].ConvertInvariant<decimal>();
            string symbol = result["product_id"].ToStringInvariant();

            decimal fees = result["fee"].ConvertInvariant<decimal>();

            ExchangeOrderResult order = new ExchangeOrderResult
            {
                TradeId = result["trade_id"].ToStringInvariant(), 
                Amount = amount, 
                AmountFilled = amount, 
                Price = price,
                Fees = fees,
                AveragePrice = price,
                IsBuy = (result["side"].ToStringInvariant() == "buy"),
                OrderDate = result["created_at"].ToDateTimeInvariant(),
                MarketSymbol = symbol,
                OrderId = result["order_id"].ToStringInvariant(), 
            };
             
            return order;
        }

        private ExchangeOrderResult ParseOrder(JToken result)
        {
            decimal executedValue = result["executed_value"].ConvertInvariant<decimal>();
            decimal amountFilled = result["filled_size"].ConvertInvariant<decimal>();
            decimal amount = result["size"].ConvertInvariant<decimal>(amountFilled);
            decimal price = result["price"].ConvertInvariant<decimal>();
            decimal stop_price = result["stop_price"].ConvertInvariant<decimal>();
            decimal averagePrice = (amountFilled <= 0m ? 0m : executedValue / amountFilled);
            decimal fees = result["fill_fees"].ConvertInvariant<decimal>();
            string marketSymbol = result["product_id"].ToStringInvariant(result["id"].ToStringInvariant());

            ExchangeOrderResult order = new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                Price = price <= 0m ? stop_price : price,
                Fees = fees,
                FeesCurrency = marketSymbol.Substring(0, marketSymbol.IndexOf('-')),
                AveragePrice = averagePrice,
                IsBuy = (result["side"].ToStringInvariant() == "buy"),
                OrderDate = result["created_at"].ToDateTimeInvariant(),
                FillDate = result["done_at"].ToDateTimeInvariant(),
                MarketSymbol = marketSymbol,
                OrderId = result["id"].ToStringInvariant()
            };
            switch (result["status"].ToStringInvariant())
            {
                case "pending":
                    order.Result = ExchangeAPIOrderResult.Pending;
                    break;
                case "active":
                case "open":
                    if (order.Amount == order.AmountFilled)
                    {
                        order.Result = ExchangeAPIOrderResult.Filled;
                    }
                    else if (order.AmountFilled > 0.0m)
                    {
                        order.Result = ExchangeAPIOrderResult.FilledPartially;
                    }
                    else
                    {
                        order.Result = ExchangeAPIOrderResult.Pending;
                    }
                    break;
                case "done":
                case "settled":
                    switch (result["done_reason"].ToStringInvariant()) 
                    {
                        case "cancelled":
                        case "canceled":
                            order.Result = ExchangeAPIOrderResult.Canceled;
                            break;
                        case "filled":
                            order.Result = ExchangeAPIOrderResult.Filled;
                            break;
                        default:
                            order.Result = ExchangeAPIOrderResult.Unknown;
                            break;
                    }
                    break;
                case "cancelled":
                case "canceled":
                    order.Result = ExchangeAPIOrderResult.Canceled;
                    break;
                default:
                    order.Result = ExchangeAPIOrderResult.Unknown;
                    break;
            }
            return order;
        }

        protected override bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
        {
            return base.CanMakeAuthenticatedRequest(payload) && Passphrase != null;
        }

        protected override async Task OnGetNonceOffset()
        {
            try
            {
                JToken token = await MakeJsonRequestAsync<JToken>("/time");
                DateTime serverDate = token["iso"].ToDateTimeInvariant();
                NonceOffset = (CryptoUtility.UtcNow - serverDate);
            }
            catch
            {
            }
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // Coinbase is funny and wants a seconds double for the nonce, weird... we convert it to double and back to string invariantly to ensure decimal dot is used and not comma
                string timestamp = payload["nonce"].ToStringInvariant();
                payload.Remove("nonce");
                string form = CryptoUtility.GetJsonForPayload(payload);
                byte[] secret = CryptoUtility.ToBytesBase64Decode(PrivateApiKey);
                string toHash = timestamp + request.Method.ToUpperInvariant() + request.RequestUri.PathAndQuery + form;
                string signatureBase64String = CryptoUtility.SHA256SignBase64(toHash, secret);
                secret = null;
                toHash = null;
                request.AddHeader("CB-ACCESS-KEY", PublicApiKey.ToUnsecureString());
                request.AddHeader("CB-ACCESS-SIGN", signatureBase64String);
                request.AddHeader("CB-ACCESS-TIMESTAMP", timestamp);
                request.AddHeader("CB-ACCESS-PASSPHRASE", CryptoUtility.ToUnsecureString(Passphrase));
                if (request.Method == "POST")
                {
                    await CryptoUtility.WriteToRequestAsync(request, form);
                }
            }
        }

        protected override void ProcessResponse(IHttpWebResponse response)
        {
            base.ProcessResponse(response);
            cursorAfter = response.GetHeader("CB-AFTER").FirstOrDefault();
            cursorBefore = response.GetHeader("CB-BEFORE").FirstOrDefault();
        }

        public ExchangeCoinbaseAPI()
        {
            RequestContentType = "application/json";
            NonceStyle = NonceStyle.UnixSeconds;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            var markets = new List<ExchangeMarket>();
            JToken products = await MakeJsonRequestAsync<JToken>("/products");
            foreach (JToken product in products)
            {
                var market = new ExchangeMarket
                {
                    MarketSymbol = product["id"].ToStringUpperInvariant(),
                    QuoteCurrency = product["quote_currency"].ToStringUpperInvariant(),
                    BaseCurrency = product["base_currency"].ToStringUpperInvariant(),
                    IsActive = string.Equals(product["status"].ToStringInvariant(), "online", StringComparison.OrdinalIgnoreCase),
                    MinTradeSize = product["base_min_size"].ConvertInvariant<decimal>(),
                    MaxTradeSize = product["base_max_size"].ConvertInvariant<decimal>(),
                    PriceStepSize = product["quote_increment"].ConvertInvariant<decimal>()
                };
                markets.Add(market);
            }

            return markets;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await GetMarketSymbolsMetadataAsync()).Select(market => market.MarketSymbol);
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            var currencies = new Dictionary<string, ExchangeCurrency>();
            JToken products = await MakeJsonRequestAsync<JToken>("/currencies");
            foreach (JToken product in products)
            {
                var currency = new ExchangeCurrency
                {
                    Name = product["id"].ToStringUpperInvariant(),
                    FullName = product["name"].ToStringInvariant(),
                    DepositEnabled = true,
                    WithdrawalEnabled = true
                };

                currencies[currency.Name] = currency;
            }

            return currencies;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken ticker = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/ticker");
            return this.ParseTicker(ticker, marketSymbol, "ask", "bid", "price", "volume", null, "time", TimestampType.Iso8601);
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            // Hack found here: https://github.com/coinbase/gdax-node/issues/91#issuecomment-352441654 + using Fiddler 

            // Get coinbase accounts
            JArray accounts = await this.MakeJsonRequestAsync<JArray>("/coinbase-accounts", null, await GetNoncePayloadAsync(), "GET");

            foreach (JToken token in accounts)
            {
                string currency = token["currency"].ConvertInvariant<string>();
                if (currency.Equals(symbol, StringComparison.InvariantCultureIgnoreCase))
                {
                    JToken accountWalletAddress = await this.MakeJsonRequestAsync<JToken>(
                                                                                          $"/coinbase-accounts/{token["id"]}/addresses",
                                                                                          null,
                                                                                          await GetNoncePayloadAsync(),
                                                                                          "POST");

                    return new ExchangeDepositDetails { Address = accountWalletAddress["address"].ToStringInvariant(), Currency = currency };
                }

            }
            throw new APIException($"Address not found for {symbol}");
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            System.Threading.ManualResetEvent evt = new System.Threading.ManualResetEvent(false);
            List<string> symbols = (await GetMarketSymbolsAsync()).ToList();

            // stupid Coinbase does not have a one shot API call for tickers outside of web sockets
            using (var socket = GetTickersWebSocket((t) =>
                                                    {
                                                        lock (tickers)
                                                        {
                                                            if (symbols.Count != 0)
                                                            {
                                                                foreach (var kv in t)
                                                                {
                                                                    if (!tickers.Exists(m => m.Key == kv.Key))
                                                                    {
                                                                        tickers.Add(kv);
                                                                        symbols.Remove(kv.Key);
                                                                    }
                                                                }
                                                                if (symbols.Count == 0)
                                                                {
                                                                    evt.Set();
                                                                }
                                                            }
                                                        }
                                                    }
                ))
            {
                evt.WaitOne(10000);
                return tickers;
            }
        }

        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                string message = msg.ToStringFromUTF8();
                var book = new ExchangeOrderBook();

                // string comparison on the json text for faster deserialization
                // More likely to be an l2update so check for that first
                if (message.Contains(@"""l2update"""))
                {
                    // parse delta update
                    var delta = JsonConvert.DeserializeObject<Level2>(message);
                    book.MarketSymbol = delta.ProductId;
                    book.SequenceId = delta.Time.Ticks;
                    foreach (string[] change in delta.Changes)
                    {
                        decimal price = change[1].ConvertInvariant<decimal>();
                        decimal amount = change[2].ConvertInvariant<decimal>();
                        if (change[0] == "buy")
                        {
                            book.Bids[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                        }
                        else
                        {
                            book.Asks[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                        }
                    }
                }
                else if (message.Contains(@"""snapshot"""))
                {
                    // parse snapshot
                    var snapshot = JsonConvert.DeserializeObject<Snapshot>(message);
                    book.MarketSymbol = snapshot.ProductId;
                    foreach (decimal[] ask in snapshot.Asks)
                    {
                        decimal price = ask[0];
                        decimal amount = ask[1];
                        book.Asks[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                    }

                    foreach (decimal[] bid in snapshot.Bids)
                    {
                        decimal price = bid[0];
                        decimal amount = bid[1];
                        book.Bids[price] = new ExchangeOrderPrice { Amount = amount, Price = price };
                    }
                }
                else
                {
                    // no other message type handled
                    return Task.CompletedTask;
                }

                callback(book);
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                // subscribe to order book channel for each symbol
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                var chan = new Channel { Name = ChannelType.Level2, ProductIds = marketSymbols.ToList() };
                var channelAction = new ChannelAction { Type = ActionType.Subscribe, Channels = new List<Channel> { chan } };
                await _socket.SendMessageAsync(channelAction);
            });
        }

        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
        {
            return ConnectWebSocket("/", (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token["type"].ToStringInvariant() == "ticker")
                {
                    ExchangeTicker ticker = this.ParseTicker(token, token["product_id"].ToStringInvariant(), "best_ask", "best_bid", "price", "volume_24h", null, "time", TimestampType.Iso8601);
                    callback(new List<KeyValuePair<string, ExchangeTicker>>() { new KeyValuePair<string, ExchangeTicker>(token["product_id"].ToStringInvariant(), ticker) });
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? (await GetMarketSymbolsAsync()).ToArray() : marketSymbols;
                var subscribeRequest = new
                {
                    type = "subscribe",
                    product_ids = marketSymbols,
                    channels = new object[]
                    {
                        new
                        {
                            name = "ticker",
                            product_ids = marketSymbols.ToArray()
                        }
                    }
                };
                await _socket.SendMessageAsync(subscribeRequest);
            });
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
        {
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
			}
            return ConnectWebSocket("/", (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token["type"].ToStringInvariant() != "ticker") return Task.CompletedTask; //the ticker channel provides the trade information as well
                if (token["time"] == null) return Task.CompletedTask;
                ExchangeTrade trade = ParseTradeWebSocket(token);
                string marketSymbol = token["product_id"].ToStringInvariant();
                callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
                return Task.CompletedTask;
            }, async (_socket) =>
            {
				var subscribeRequest = new
                {
                    type = "subscribe",
                    product_ids = marketSymbols,
                    channels = new object[]
                    {
                        new
                        {
                            name = "ticker",
                            product_ids = marketSymbols
                        }
                    }
                };
                await _socket.SendMessageAsync(subscribeRequest);
            });
        }

        private ExchangeTrade ParseTradeWebSocket(JToken token)
        {
            return token.ParseTrade("last_size", "price", "side", "time", TimestampType.Iso8601, "sequence");
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            /*
            [{
                "time": "2014-11-07T22:19:28.578544Z",
                "trade_id": 74,
                "price": "10.00000000",
                "size": "0.01000000",
                "side": "buy"
            }, {
                "time": "2014-11-07T01:08:43.642366Z",
                "trade_id": 73,
                "price": "100.00000000",
                "size": "0.01000000",
                "side": "sell"
            }]
            */

            ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
            {
                Callback = callback,
                EndDate = endDate,
                ParseFunction = (JToken token) => token.ParseTrade("size", "price", "side", "time", TimestampType.Iso8601, "trade_id"),
                StartDate = startDate,
                MarketSymbol = marketSymbol,
                Url = "/products/[marketSymbol]/trades",
                UrlFunction = (ExchangeHistoricalTradeHelper _state) =>
                {
                    return _state.Url + (string.IsNullOrWhiteSpace(cursorBefore) ? string.Empty : "?before=" + cursorBefore.ToStringInvariant());
                }
            };
            await state.ProcessHistoricalTrades();
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            string baseUrl = "/products/" + marketSymbol.ToUpperInvariant() + "/trades";
            JToken trades = await MakeJsonRequestAsync<JToken>(baseUrl);
            List<ExchangeTrade> tradeList = new List<ExchangeTrade>();
            foreach (JToken trade in trades)
            {
                tradeList.Add(trade.ParseTrade("size", "price", "side", "time", TimestampType.Iso8601, "trade_id"));
            }
            return tradeList;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 50)
        {
            string url = "/products/" + marketSymbol.ToUpperInvariant() + "/book?level=2";
            JToken token = await MakeJsonRequestAsync<JToken>(url);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token, maxCount: maxCount);
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // /products/<product-id>/candles
            // https://api.pro.coinbase.com/products/LTC-BTC/candles?granularity=86400&start=2017-12-04T18:15:33&end=2017-12-11T18:15:33
            List<MarketCandle> candles = new List<MarketCandle>();
            string url = "/products/" + marketSymbol + "/candles?granularity=" + periodSeconds;
            if (startDate == null)
            {
                startDate = CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(1.0));
            }
            url += "&start=" + startDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            if (endDate == null)
            {
                endDate = CryptoUtility.UtcNow;
            }
            url += "&end=" + endDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

            // time, low, high, open, close, volume
            JToken token = await MakeJsonRequestAsync<JToken>(url);
            foreach (JToken candle in token)
            {
                candles.Add(this.ParseCandle(candle, marketSymbol, periodSeconds, 3, 2, 1, 4, 0, TimestampType.UnixSeconds, 5));
            }
            // re-sort in ascending order
            candles.Sort((c1, c2) => c1.Timestamp.CompareTo(c2.Timestamp));
            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, await GetNoncePayloadAsync(), "GET");
            foreach (JToken token in array)
            {
                decimal amount = token["balance"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts[token["currency"].ToStringInvariant()] = amount;
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, await GetNoncePayloadAsync(), "GET");
            foreach (JToken token in array)
            {
                decimal amount = token["available"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    amounts[token["currency"].ToStringInvariant()] = amount;
                }
            }
            return amounts;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            object nonce = await GenerateNonceAsync();
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "nonce", nonce },
                { "type", order.OrderType.ToStringLowerInvariant() },
                { "side", (order.IsBuy ? "buy" : "sell") },
                { "product_id", order.MarketSymbol },
                { "size", order.RoundAmount().ToStringInvariant() }
            };
            payload["time_in_force"] = "GTC"; // good til cancel
            payload["price"] = order.Price.ToStringInvariant();
            switch (order.OrderType)
            {
                case OrderType.Limit:
                    // set payload["post_only"] to true for default scenario when order.ExtraParameters["post_only"] is not specified
                    // to place non-post-only limit order one can set and pass order.ExtraParameters["post_only"]="false"
                    payload["post_only"] = order.ExtraParameters.TryGetValueOrDefault("post_only", "true");
                    break;
                    
                case OrderType.Stop:
                    payload["stop"] = (order.IsBuy ? "entry" : "loss");
                    payload["stop_price"] = order.StopPrice.ToStringInvariant();
                    payload["type"] = order.Price > 0m ? "limit" : "market";
                    break;
                    
                case OrderType.Market:
                default:
                    break;
            }

            order.ExtraParameters.CopyTo(payload);
            JToken result = await MakeJsonRequestAsync<JToken>("/orders", null, payload, "POST");
            return ParseOrder(result);
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/orders/" + orderId, null, await GetNoncePayloadAsync(), "GET");
            return ParseOrder(obj);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JArray array = await MakeJsonRequestAsync<JArray>("orders?status=all" + (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "&product_id=" + marketSymbol), null, await GetNoncePayloadAsync(), "GET");
            foreach (JToken token in array)
            {
                orders.Add(ParseOrder(token));
            }

            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JArray array = await MakeJsonRequestAsync<JArray>("orders?status=done" + (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "&product_id=" + marketSymbol), null, await GetNoncePayloadAsync(), "GET");
            foreach (JToken token in array)
            {
                ExchangeOrderResult result = ParseOrder(token);
                if (afterDate == null || result.OrderDate >= afterDate)
                {
                    orders.Add(result);
                }
            }

            return orders;
        }

        public async Task<IEnumerable<ExchangeOrderResult>> GetFillsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            var productId = (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "product_id=" + marketSymbol);
            do
            {
                var after = cursorAfter == null ? string.Empty : $"after={cursorAfter}&";
                await new SynchronizationContextRemover();
                await MakeFillRequest(afterDate, productId, orders, after);
            } while (cursorAfter != null);
            return orders;
        }

        private async Task MakeFillRequest(DateTime? afterDate, string productId, List<ExchangeOrderResult> orders, string after)
        {
            var interrogation = after != "" || productId != "" ? "?" : string.Empty;
            JArray array = await MakeJsonRequestAsync<JArray>($"fills{interrogation}{after}{productId}", null, await GetNoncePayloadAsync());

            foreach (JToken token in array)
            {
                ExchangeOrderResult result = ParseFill(token);
                if (afterDate == null || result.OrderDate >= afterDate)
                {
                    orders.Add(result);
                }

                if (afterDate != null && result.OrderDate < afterDate)
                {
                    cursorAfter = null;
                    break;
                }
            }
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            await MakeJsonRequestAsync<JArray>("orders/" + orderId, null, await GetNoncePayloadAsync(), "DELETE");
        }
    }

    public partial class ExchangeName { public const string Coinbase = "Coinbase"; }
}
