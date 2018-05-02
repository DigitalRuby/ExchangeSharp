/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    using System.Linq;

    public sealed class ExchangeGdaxAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.gdax.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://ws-feed.gdax.com";
        public override string Name => ExchangeName.GDAX;

        /// <summary>
        /// The response will also contain a CB-AFTER header which will return the cursor id to use in your next request for the page after this one. The page after is an older page and not one that happened after this one in chronological time.
        /// </summary>
        private string cursorAfter;

        /// <summary>
        /// The response will contain a CB-BEFORE header which will return the cursor id to use in your next request for the page before the current one. The page before is a newer page and not one that happened before in chronological time.
        /// </summary>
        private string cursorBefore;

        private ExchangeOrderResult ParseOrder(JToken result)
        {
            decimal executedValue = result["executed_value"].ConvertInvariant<decimal>();
            decimal amountFilled = result["filled_size"].ConvertInvariant<decimal>();
            decimal amount = result["size"].ConvertInvariant<decimal>(amountFilled);
            decimal price = result["price"].ConvertInvariant<decimal>();
            decimal averagePrice = (amountFilled <= 0m ? 0m : executedValue / amountFilled);
            ExchangeOrderResult order = new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                Price = price,
                AveragePrice = averagePrice,
                IsBuy = (result["side"].ToStringInvariant() == "buy"),
                OrderDate = result["created_at"].ConvertInvariant<DateTime>(),
                Symbol = result["product_id"].ToStringInvariant(),
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
                    order.Result = ExchangeAPIOrderResult.Filled;
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

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (!CanMakeAuthenticatedRequest(payload))
            {
                return;
            }

            // gdax is funny and wants a seconds double for the nonce, weird... we convert it to double and back to string invariantly to ensure decimal dot is used and not comma
            string timestamp = payload["nonce"].ToStringInvariant();
            payload.Remove("nonce");
            string form = GetJsonForPayload(payload);
            byte[] secret = CryptoUtility.SecureStringToBytesBase64Decode(PrivateApiKey);
            string toHash = timestamp + request.Method.ToUpper() + request.RequestUri.PathAndQuery + form;
            string signatureBase64String = CryptoUtility.SHA256SignBase64(toHash, secret);
            secret = null;
            toHash = null;
            request.Headers["CB-ACCESS-KEY"] = PublicApiKey.ToUnsecureString();
            request.Headers["CB-ACCESS-SIGN"] = signatureBase64String;
            request.Headers["CB-ACCESS-TIMESTAMP"] = timestamp;
            request.Headers["CB-ACCESS-PASSPHRASE"] = CryptoUtility.SecureStringToString(Passphrase);
            WriteFormToRequest(request, form);
        }

        protected override void ProcessResponse(HttpWebResponse response)
        {
            base.ProcessResponse(response);
            cursorAfter = response.Headers["cb-after"];
            cursorBefore = response.Headers["cb-before"];
        }

        public ExchangeGdaxAPI()
        {
            RequestContentType = "application/json";
            NonceStyle = NonceStyle.UnixSeconds;
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace('_', '-').ToUpperInvariant();
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            var markets = new List<ExchangeMarket>();
            JToken products = await MakeJsonRequestAsync<JToken>("/products");
            foreach (JToken product in products)
            {
                var market = new ExchangeMarket
                {
                    MarketName = product["id"].ToStringUpperInvariant(),
                    BaseCurrency = product["quote_currency"].ToStringUpperInvariant(),
                    MarketCurrency = product["base_currency"].ToStringUpperInvariant(),
                    IsActive = string.Equals(product["status"].ToStringInvariant(), "online", StringComparison.OrdinalIgnoreCase),
                    MinTradeSize = product["base_min_size"].ConvertInvariant<decimal>(),
                    PriceStepSize = product["quote_increment"].ConvertInvariant<decimal>()
                };
                markets.Add(market);
            }

            return markets;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            return (await GetSymbolsMetadataAsync()).Select(market => market.MarketName);
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
                    IsEnabled = true
                };

                currencies[currency.Name] = currency;
            }

            return currencies;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            Dictionary<string, string> ticker = await MakeJsonRequestAsync<Dictionary<string, string>>("/products/" + symbol + "/ticker");
            decimal volume = Convert.ToDecimal(ticker["volume"], System.Globalization.CultureInfo.InvariantCulture);
            DateTime timestamp = DateTime.Parse(ticker["time"]);
            decimal price = Convert.ToDecimal(ticker["price"], System.Globalization.CultureInfo.InvariantCulture);
            return new ExchangeTicker
            {
                Ask = Convert.ToDecimal(ticker["ask"], System.Globalization.CultureInfo.InvariantCulture),
                Bid = Convert.ToDecimal(ticker["bid"], System.Globalization.CultureInfo.InvariantCulture),
                Last = price,
                Volume = new ExchangeVolume { BaseVolume = volume, BaseSymbol = symbol, ConvertedVolume = volume * price, ConvertedSymbol = symbol, Timestamp = timestamp }
            };
        }

        public override IDisposable GetTickersWebSocket(System.Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            if (callback == null) return null;

            var wrapper = ConnectWebSocket("/", (msg, _socket) =>
            {
                try
                {
                    JToken token = JToken.Parse(msg);
                    if (token["type"].ToStringInvariant() != "ticker") return;
                    ExchangeTicker ticker = ParseTickerWebSocket(token);
                    callback(new List<KeyValuePair<string, ExchangeTicker>>() { new KeyValuePair<string, ExchangeTicker>(token["product_id"].ToStringInvariant(), ticker) });
                }
                catch
                {
                }
            }) as WebSocketWrapper;

            var symbols = GetSymbols();

            var subscribeRequest = new
            {
                type = "subscribe",
                product_ids = symbols,
                channels = new object[]
                {
                    new {
                        name = "ticker",
                        product_ids = symbols
                    }
                }
            };
            wrapper.SendMessage(Newtonsoft.Json.JsonConvert.SerializeObject(subscribeRequest));

            return wrapper;
        }

        private ExchangeTicker ParseTickerWebSocket(JToken token)
        {
            var price = token["price"].ConvertInvariant<decimal>();
            var lastSize = token["last_size"].ConvertInvariant<decimal>();
            var symbol = token["product_id"].ToStringInvariant();
            var time = token["time"] == null ? DateTime.Now.ToUniversalTime() : Convert.ToDateTime(token["time"].ToStringInvariant());
            return new ExchangeTicker
            {
                Ask = token["best_ask"].ConvertInvariant<decimal>(),
                Bid = token["best_bid"].ConvertInvariant<decimal>(),
                Last = price,
                Volume = new ExchangeVolume
                {
                    BaseVolume = lastSize * price,
                    BaseSymbol = symbol.Split(new char[] { '-' })[1],
                    ConvertedVolume = lastSize,
                    ConvertedSymbol = symbol.Split(new char[] { '-' })[0],
                    Timestamp = time
                }
            };
        }

        protected override async Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            string baseUrl = "/products/" + symbol.ToUpperInvariant() + "/candles?granularity=" + (sinceDateTime == null ? "3600.0" : "60.0");
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            decimal[][] tradeChunk;
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&start=" + System.Web.HttpUtility.UrlEncode(sinceDateTime.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
                    url += "&end=" + System.Web.HttpUtility.UrlEncode(sinceDateTime.Value.AddMinutes(5.0).ToString("s", System.Globalization.CultureInfo.InvariantCulture));
                }
                tradeChunk = await MakeJsonRequestAsync<decimal[][]>(url);
                if (tradeChunk == null || tradeChunk.Length == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeSeconds((double)tradeChunk[0][0]);
                }
                foreach (decimal[] tradeChunkPiece in tradeChunk)
                {
                    trades.Add(new ExchangeTrade { Amount = tradeChunkPiece[5], IsBuy = true, Price = tradeChunkPiece[3], Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds((double)tradeChunkPiece[0]), Id = 0 });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                if (!callback(trades))
                {
                    break;
                }
                trades.Clear();
                if (sinceDateTime == null)
                {
                    break;
                }
                Task.Delay(1000).Wait();
            }
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            string baseUrl = "/products/" + symbol.ToUpperInvariant() + "/trades";
            Dictionary<string, object>[] trades = await MakeJsonRequestAsync<Dictionary<string, object>[]>(baseUrl);
            List<ExchangeTrade> tradeList = new List<ExchangeTrade>();
            foreach (Dictionary<string, object> trade in trades)
            {
                tradeList.Add(new ExchangeTrade
                {
                    Amount = trade["size"].ConvertInvariant<decimal>(),
                    IsBuy = trade["side"].ToStringInvariant() == "buy",
                    Price = trade["price"].ConvertInvariant<decimal>(),
                    Timestamp = (DateTime)trade["time"],
                    Id = trade["trade_id"].ConvertInvariant<long>()
                });
            }
            return tradeList;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 50)
        {
            string url = "/products/" + symbol.ToUpperInvariant() + "/book?level=2";
            ExchangeOrderBook orders = new ExchangeOrderBook();
            Dictionary<string, object> books = await MakeJsonRequestAsync<Dictionary<string, object>>(url);
            JArray asks = books["asks"] as JArray;
            JArray bids = books["bids"] as JArray;
            foreach (JArray ask in asks)
            {
                orders.Asks.Add(new ExchangeOrderPrice { Amount = ask[1].ConvertInvariant<decimal>(), Price = ask[0].ConvertInvariant<decimal>() });
            }
            foreach (JArray bid in bids)
            {
                orders.Bids.Add(new ExchangeOrderPrice { Amount = bid[1].ConvertInvariant<decimal>(), Price = bid[0].ConvertInvariant<decimal>() });
            }
            return orders;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // /products/<product-id>/candles
            // https://api.gdax.com/products/LTC-BTC/candles?granularity=86400&start=2017-12-04T18:15:33&end=2017-12-11T18:15:33
            List<MarketCandle> candles = new List<MarketCandle>();
            symbol = NormalizeSymbol(symbol);
            string url = "/products/" + symbol + "/candles?granularity=" + periodSeconds;
            if (startDate == null)
            {
                startDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1.0));
            }
            url += "&start=" + startDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            if (endDate == null)
            {
                endDate = DateTime.UtcNow;
            }
            url += "&end=" + endDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

            // time, low, high, open, close, volume
            JToken token = await MakeJsonRequestAsync<JToken>(url);
            foreach (JArray candle in token)
            {
                candles.Add(new MarketCandle
                {
                    ClosePrice = candle[4].ConvertInvariant<decimal>(),
                    ExchangeName = Name,
                    HighPrice = candle[2].ConvertInvariant<decimal>(),
                    LowPrice = candle[1].ConvertInvariant<decimal>(),
                    Name = symbol,
                    OpenPrice = candle[3].ConvertInvariant<decimal>(),
                    PeriodSeconds = periodSeconds,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(candle[0].ConvertInvariant<long>()),
                    BaseVolume = candle[5].ConvertInvariant<double>(),
                    ConvertedVolume = candle[5].ConvertInvariant<double>() * candle[4].ConvertInvariant<double>()
                });
            }
            // re-sort in ascending order
            candles.Sort((c1, c2) => c1.Timestamp.CompareTo(c2.Timestamp));
            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, GetNoncePayload());
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
            JArray array = await MakeJsonRequestAsync<JArray>("/accounts", null, GetNoncePayload());
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
            string symbol = NormalizeSymbol(order.Symbol);
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "nonce",GenerateNonce() },
                { "type", order.OrderType.ToStringLowerInvariant() },
                { "side", (order.IsBuy ? "buy" : "sell") },
                { "product_id", symbol },
                { "size", order.RoundAmount().ToStringInvariant() }
            };

            if (order.OrderType != OrderType.Market)
            {
                payload["time_in_force"] = "GTC"; // good til cancel
                payload["price"] = order.Price.ToStringInvariant();
            }

            foreach (var kv in order.ExtraParameters)
            {
                payload[kv.Key] = kv.Value;
            }

            JObject result = await MakeJsonRequestAsync<JObject>("/orders", null, payload, "POST");
            return ParseOrder(result);
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            JObject obj = await MakeJsonRequestAsync<JObject>("/orders/" + orderId, null, GetNoncePayload(), "GET");
            return ParseOrder(obj);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            symbol = NormalizeSymbol(symbol);
            JArray array = await MakeJsonRequestAsync<JArray>("orders?status=all" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "&product_id=" + symbol), null, GetNoncePayload());
            foreach (JToken token in array)
            {
                orders.Add(ParseOrder(token));
            }

            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            symbol = NormalizeSymbol(symbol);
            JArray array = await MakeJsonRequestAsync<JArray>("orders?status=done" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "&product_id=" + symbol), null, GetNoncePayload());
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

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            await MakeJsonRequestAsync<JArray>("orders/" + orderId, null, GetNoncePayload(), "DELETE");
        }
    }
}
