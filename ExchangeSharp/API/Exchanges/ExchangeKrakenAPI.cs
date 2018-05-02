/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed class ExchangeKrakenAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.kraken.com";
        public override string Name => ExchangeName.Kraken;

        static ExchangeKrakenAPI()
        {
            Dictionary<string, string> d = normalizedSymbolToExchangeSymbol as Dictionary<string, string>;
            foreach (KeyValuePair<string, string> kv in exchangeSymbolToNormalizedSymbol)
            {
                if (!d.ContainsKey(kv.Value))
                {
                    d.Add(kv.Value, kv.Key);
                }
            }
        }

        public ExchangeKrakenAPI()
        {
            RequestMethod = "POST";
            RequestContentType = "application/x-www-form-urlencoded";
            SymbolSeparator = string.Empty;
            SymbolIsReversed = true;
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
        }

        public override string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            if (exchangeSymbolToNormalizedSymbol.TryGetValue(symbol, out string normalizedSymbol))
            {
                return base.ExchangeSymbolToGlobalSymbolWithSeparator(normalizedSymbol.Substring(0, 3) + GlobalSymbolSeparator + normalizedSymbol.Substring(3), GlobalSymbolSeparator);
            }
            throw new ArgumentException($"Symbol {symbol} not found in Kraken lookup table");
        }

        public override string GlobalSymbolToExchangeSymbol(string symbol)
        {
            if (normalizedSymbolToExchangeSymbol.TryGetValue(symbol.Replace(GlobalSymbolSeparator.ToString(), string.Empty), out string exchangeSymbol))
            {
                return exchangeSymbol;
            }

            // not found, reverse the pair
            int idx = symbol.IndexOf(GlobalSymbolSeparator);
            symbol = symbol.Substring(idx + 1) + symbol.Substring(0, idx);
            if (normalizedSymbolToExchangeSymbol.TryGetValue(symbol.Replace(GlobalSymbolSeparator.ToString(), string.Empty), out exchangeSymbol))
            {
                return exchangeSymbol;
            }

            throw new ArgumentException($"Symbol {symbol} not found in Kraken lookup table");
        }

        /// <summary>
        /// Change Kraken symbols to more common sense symbols
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> exchangeSymbolToNormalizedSymbol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BCHEUR", "bcheur" },
            { "BCHUSD", "bchusd" },
            { "BCHXBT", "bchbtc" },
            { "DASHEUR", "dasheur" },
            { "DASHUSD", "dashusd" },
            { "DASHXBT", "dashbtc" },
            { "EOSETH", "eoseth" },
            { "EOSXBT", "eosbtc" },
            { "GNOETH", "gnoeth" },
            { "GNOXBT", "gnobtc" },
            { "USDTZUSD", "usdtusd" },
            { "XETCXETH", "etceth" },
            { "XETCXXBT", "etcbtc" },
            { "XETCZEUR", "etceur" },
            { "XETCZUSD", "etcusd" },
            { "XETHXXBT", "ethbtc" },
            { "XETHXXBT.d", "ethbtc" },
            { "XETHZCAD", "ethcad" },
            { "XETHZCAD.d", "ethcad" },
            { "XETHZEUR", "etheur" },
            { "XETHZEUR.d", "etheur" },
            { "XETHZGBP", "ethgbp" },
            { "XETHZGBP.d", "ethgbp" },
            { "XETHZJPY", "ethjpy" },
            { "XETHZJPY.d", "ethjpy" },
            { "XETHZUSD", "ethusd" },
            { "XETHZUSD.d", "ethusd" },
            { "XICNXETH", "icneth" },
            { "XICNXXBT", "icnbtc" },
            { "XLTCXXBT", "ltcbtc" },
            { "XLTCZEUR", "ltceur" },
            { "XLTCZUSD", "ltcusd" },
            { "XMLNXETH", "mlneth" },
            { "XMLNXXBT", "mlnbtc" },
            { "XREPXETH", "repeth" },
            { "XREPXXBT", "repbtc" },
            { "XREPZEUR", "repeur" },
            { "XXBTZCAD", "btccad" },
            { "XXBTZCAD.d", "btccad" },
            { "XXBTZEUR", "btceur" },
            { "XXBTZEUR.d", "btceur" },
            { "XXBTZGBP", "btcgbp" },
            { "XXBTZGBP.d", "btcgpb" },
            { "XXBTZJPY", "btcjpy" },
            { "XXBTZJPY.d", "btcjpy" },
            { "XXBTZUSD", "btcusd" },
            { "XXBTZUSD.d", "btcusd" },
            { "XXDGXXBT", "dogebtc" },
            { "XXLMXXBT", "xlmbtc" },
            { "XXMRXXBT", "xmrbtc" },
            { "XXMRZEUR", "xmreur" },
            { "XXMRZUSD", "xmrusd" },
            { "XXRPXXBT", "xrpbtc" },
            { "XXRPZEUR", "xrpeur" },
            { "XXRPZUSD", "xrpusd" },
            { "XZECXXBT", "zecbtc" },
            { "XZECZEUR", "zeceur" },
            { "XZECZUSD", "zecusd" }
        };
        private static readonly IReadOnlyDictionary<string, string> normalizedSymbolToExchangeSymbol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private JToken CheckError(JToken json)
        {
            if (!(json is JArray) && json["error"] is JArray error && error.Count != 0)
            {
                throw new APIException(error[0].ToStringInvariant());
            }
            return json["result"];
        }

        private ExchangeOrderResult ParseOrder(string orderId, JToken order)
        {
            ExchangeOrderResult orderResult = new ExchangeOrderResult { OrderId = orderId };

            switch (order["status"].ToStringInvariant())
            {
                case "pending": orderResult.Result = ExchangeAPIOrderResult.Pending; break;
                case "open": orderResult.Result = ExchangeAPIOrderResult.FilledPartially; break;
                case "closed": orderResult.Result = ExchangeAPIOrderResult.Filled; break;
                case "canceled": case "expired": orderResult.Result = ExchangeAPIOrderResult.Canceled; break;
                default: orderResult.Result = ExchangeAPIOrderResult.Error; break;
            }
            orderResult.Message = (orderResult.Message ?? order["reason"].ToStringInvariant());
            orderResult.OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(order["opentm"].ConvertInvariant<double>());
            orderResult.Symbol = order["descr"]["pair"].ToStringInvariant();
            orderResult.IsBuy = (order["descr"]["type"].ToStringInvariant() == "buy");
            orderResult.Amount = order["vol"].ConvertInvariant<decimal>();
            orderResult.AmountFilled = order["vol_exec"].ConvertInvariant<decimal>();
            orderResult.Price = order["descr"]["price"].ConvertInvariant<decimal>();
            orderResult.AveragePrice = order["price"].ConvertInvariant<decimal>();

            return orderResult;
        }

        private async Task<IEnumerable<ExchangeOrderResult>> QueryOrdersAsync(string symbol, string path)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeSharp.ExchangeOrderResult>();
            JObject json = await MakeJsonRequestAsync<JObject>(path, null, GetNoncePayload());
            JToken result = CheckError(json);
            result = result["open"];

            symbol = NormalizeSymbol(symbol);

            foreach (JProperty order in result)
            {
                if (symbol == null || order.Value["descr"]["pair"].ToStringInvariant() == symbol)
                {
                    orders.Add(ParseOrder(order.Name, order.Value));
                }
            }

            return orders;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload == null || PrivateApiKey == null || PublicApiKey == null || !payload.ContainsKey("nonce"))
            {
                WritePayloadToRequest(request, payload);
            }
            else
            {
                string nonce = payload["nonce"].ToStringInvariant();
                payload.Remove("nonce");
                string form = GetFormForPayload(payload);
                // nonce must be first on Kraken
                form = "nonce=" + nonce + (string.IsNullOrWhiteSpace(form) ? string.Empty : "&" + form);
                using (SHA256 sha256 = SHA256Managed.Create())
                {
                    string hashString = nonce + form;
                    byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashString));
                    byte[] pathBytes = Encoding.UTF8.GetBytes(request.RequestUri.AbsolutePath);
                    byte[] sigBytes = new byte[sha256Bytes.Length + pathBytes.Length];
                    pathBytes.CopyTo(sigBytes, 0);
                    sha256Bytes.CopyTo(sigBytes, pathBytes.Length);
                    byte[] privateKey = System.Convert.FromBase64String(CryptoUtility.SecureStringToString(PrivateApiKey));
                    using (System.Security.Cryptography.HMACSHA512 hmac = new System.Security.Cryptography.HMACSHA512(privateKey))
                    {
                        string sign = System.Convert.ToBase64String(hmac.ComputeHash(sigBytes));
                        request.Headers.Add("API-Sign", sign);
                    }
                }
                request.Headers.Add("API-Key", CryptoUtility.SecureStringToString(PublicApiKey));
                WriteFormToRequest(request, form);
            }
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            JObject json = await MakeJsonRequestAsync<JObject>("/0/public/AssetPairs");
            JToken result = CheckError(json);
            return (from prop in result.Children<JProperty>() where !prop.Name.Contains(".d") select prop.Name).ToArray();
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            var symbols = await GetSymbolsAsync();
            var normalizedPairsList = symbols.Select(symbol => NormalizeSymbol(symbol)).ToList();
            var csvPairsList = string.Join(",", normalizedPairsList);
            JObject json = await MakeJsonRequestAsync<JObject>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", csvPairsList } });
            JToken apiTickers = CheckError(json);
            var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (string symbol in symbols)
            {
                JToken ticker = apiTickers[symbol];
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ConvertToExchangeTicker(symbol, ticker)));
            }
            return tickers;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            JObject json = await MakeJsonRequestAsync<JObject>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", NormalizeSymbol(symbol) } });
            JToken apiTickers = CheckError(json);
            JToken ticker = apiTickers[symbol];
            return ConvertToExchangeTicker(symbol, ticker);
        }

        private static ExchangeTicker ConvertToExchangeTicker(string symbol, JToken ticker)
        {
            decimal last = ticker["c"][0].ConvertInvariant<decimal>();
            return new ExchangeTicker
            {
                Ask = ticker["a"][0].ConvertInvariant<decimal>(),
                Bid = ticker["b"][0].ConvertInvariant<decimal>(),
                Last = last,
                Volume = new ExchangeVolume
                {
                    BaseVolume = ticker["v"][1].ConvertInvariant<decimal>(),
                    BaseSymbol = symbol,
                    ConvertedVolume = ticker["v"][1].ConvertInvariant<decimal>() * last,
                    ConvertedSymbol = symbol,
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JObject json = await MakeJsonRequestAsync<JObject>("/0/public/Depth?pair=" + symbol + "&count=" + maxCount);
            JToken obj = CheckError(json);
            obj = obj[symbol];
            if (obj == null)
            {
                return null;
            }
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = obj["bids"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token[1].ConvertInvariant<decimal>(), Price = token[0].ConvertInvariant<decimal>() };
                orders.Bids.Add(order);
            }
            JToken asks = obj["asks"];
            foreach (JToken token in asks)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token[1].ConvertInvariant<decimal>(), Price = token[0].ConvertInvariant<decimal>() };
                orders.Asks.Add(order);
            }
            return orders;
        }

        protected override async Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/0/public/Trades?pair=" + symbol;
            string url;
            DateTime timestamp;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&since=" + (long)(CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value) * 1000000.0);
                }
                JObject obj = await MakeJsonRequestAsync<JObject>(url);
                if (obj == null)
                {
                    break;
                }
                JToken result = CheckError(obj);
                JArray outerArray = result[symbol] as JArray;
                if (outerArray == null || outerArray.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(result["last"].ConvertInvariant<double>() / 1000000.0d);
                }
                foreach (JArray array in outerArray.Children<JArray>())
                {
                    timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(array[2].ConvertInvariant<double>());
                    trades.Add(new ExchangeTrade
                    {
                        Amount = array[1].ConvertInvariant<decimal>(),
                        Price = array[0].ConvertInvariant<decimal>(),
                        Timestamp = timestamp,
                        Id = timestamp.Ticks,
                        IsBuy = array[3].ConvertInvariant<char>() == 'b'
                    });
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

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // https://api.kraken.com/0/public/OHLC
            // pair = asset pair to get OHLC data for, interval = time frame interval in minutes(optional):, 1(default), 5, 15, 30, 60, 240, 1440, 10080, 21600, since = return committed OHLC data since given id(optional.exclusive)
            // array of array entries(<time>, <open>, <high>, <low>, <close>, <vwap>, <volume>, <count>)
            symbol = NormalizeSymbol(symbol);
            startDate = startDate ?? DateTime.UtcNow.Subtract(TimeSpan.FromDays(1.0));
            endDate = endDate ?? DateTime.UtcNow;
            JObject json = await MakeJsonRequestAsync<JObject>("/0/public/OHLC?pair=" + symbol + "&interval=" + periodSeconds / 60 + "&since=" + startDate);
            CheckError(json);
            List<MarketCandle> candles = new List<MarketCandle>();
            if (json["result"].Children().Count() != 0)
            {
                JProperty prop = json["result"].Children().First() as JProperty;
                foreach (JArray jsonCandle in prop.Value)
                {
                    MarketCandle candle = new MarketCandle
                    {
                        ClosePrice = jsonCandle[4].ConvertInvariant<decimal>(),
                        ExchangeName = Name,
                        HighPrice = jsonCandle[2].ConvertInvariant<decimal>(),
                        LowPrice = jsonCandle[3].ConvertInvariant<decimal>(),
                        Name = symbol,
                        OpenPrice = jsonCandle[1].ConvertInvariant<decimal>(),
                        PeriodSeconds = periodSeconds,
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeSeconds(jsonCandle[0].ConvertInvariant<long>()),
                        BaseVolume = jsonCandle[6].ConvertInvariant<double>(),
                        ConvertedVolume = jsonCandle[6].ConvertInvariant<double>() * jsonCandle[4].ConvertInvariant<double>(),
                        WeightedAverage = jsonCandle[5].ConvertInvariant<decimal>()
                    };
                    if (candle.Timestamp >= startDate.Value && candle.Timestamp <= endDate.Value)
                    {
                        candles.Add(candle);
                    }
                }
            }

            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            JToken token = await MakeJsonRequestAsync<JToken>("/0/private/Balance", null, GetNoncePayload());
            JToken result = CheckError(token);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty prop in result)
            {
                decimal amount = prop.Value.ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    balances[prop.Name] = amount;
                }
            }
            return balances;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string symbol = NormalizeSymbol(order.Symbol);
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "pair", symbol },
                { "type", (order.IsBuy ? "buy" : "sell") },
                { "ordertype", order.OrderType.ToString().ToLowerInvariant() },
                { "volume", order.RoundAmount().ToStringInvariant() },
                { "nonce", GenerateNonce() }
            };
            if (order.OrderType != OrderType.Market)
            {
                payload.Add("price", order.Price.ToStringInvariant());
            }
            foreach (var kv in order.ExtraParameters)
            {
                payload[kv.Key] = kv.Value;
            }

            JObject obj = await MakeJsonRequestAsync<JObject>("/0/private/AddOrder", null, payload);
            JToken token = CheckError(obj);
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                OrderDate = DateTime.UtcNow
            };
            if (token["txid"] is JArray array)
            {
                result.OrderId = array[0].ToStringInvariant();
            }
            return result;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "txid", orderId },
                { "nonce", GenerateNonce() }
            };
            JObject obj = await MakeJsonRequestAsync<JObject>("/0/private/QueryOrders", null, payload);
            JToken result = CheckError(obj);
            ExchangeOrderResult orderResult = new ExchangeOrderResult { OrderId = orderId };
            if (result == null || result[orderId] == null)
            {
                orderResult.Message = "Unknown Error";
                return orderResult;
            }
            
            return ParseOrder(orderId, result[orderId]); ;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            return await QueryOrdersAsync(symbol, "/0/private/OpenOrders");
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            string path = "/0/private/ClosedOrders";
            if (afterDate != null)
            {
                path += "?start=" + ((long)afterDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
            }
            return await QueryOrdersAsync(symbol, path);
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "txid", orderId },
                { "nonce", GenerateNonce() }
            };
            JObject obj = await MakeJsonRequestAsync<JObject>("/0/private/CancelOrder", null, payload);
            CheckError(obj);
        }
    }
}
