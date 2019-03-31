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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeKrakenAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.kraken.com";

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
            MarketSymbolSeparator = string.Empty;
            NonceStyle = NonceStyle.UnixMilliseconds;
        }

        public override string ExchangeMarketSymbolToGlobalMarketSymbol(string marketSymbol)
        {
            if (exchangeSymbolToNormalizedSymbol.TryGetValue(marketSymbol, out string normalizedSymbol))
            {
                int pos = (normalizedSymbol.Length == 6 ? 3 : (normalizedSymbol.Length == 7 ? 4 : throw new InvalidOperationException("Cannot normalize symbol " + normalizedSymbol)));
                return base.ExchangeMarketSymbolToGlobalMarketSymbolWithSeparator(normalizedSymbol.Substring(0, pos) + GlobalMarketSymbolSeparator + normalizedSymbol.Substring(pos), GlobalMarketSymbolSeparator);
            }
            throw new ArgumentException($"Symbol {marketSymbol} not found in Kraken lookup table");
        }

        public override string GlobalMarketSymbolToExchangeMarketSymbol(string marketSymbol)
        {
            if (normalizedSymbolToExchangeSymbol.TryGetValue(marketSymbol.Replace(GlobalMarketSymbolSeparator.ToString(), string.Empty), out string exchangeSymbol))
            {
                return exchangeSymbol;
            }

            // not found, reverse the pair
            int idx = marketSymbol.IndexOf(GlobalMarketSymbolSeparator);
            marketSymbol = marketSymbol.Substring(idx + 1) + marketSymbol.Substring(0, idx);
            if (normalizedSymbolToExchangeSymbol.TryGetValue(marketSymbol.Replace(GlobalMarketSymbolSeparator.ToString(), string.Empty), out exchangeSymbol))
            {
                return exchangeSymbol;
            }

            throw new ArgumentException($"Symbol {marketSymbol} not found in Kraken lookup table");
        }

        /// <summary>
        /// Change Kraken symbols to more common sense symbols
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> exchangeSymbolToNormalizedSymbol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
		   { "ADACAD" , "adacad" },
			{ "ADAETH" , "adaeth" },
			{ "ADAEUR" , "adaeur" },
			{ "ADAUSD" , "adausd" },
			{ "ADAXBT" , "adaxbt" },
			{ "BCHEUR" , "bcheur" },
			{ "BCHUSD" , "bchusd" },
			{ "BCHXBT" , "bchxbt" },
			{ "BSVEUR" , "bsveur" },
			{ "BSVUSD" , "bsvusd" },
			{ "BSVXBT" , "bsvxbt" },
			{ "DASHEUR" , "dasheur" },
			{ "DASHUSD" , "dashusd" },
			{ "DASHXBT" , "dashxbt" },
			{ "EOSETH" , "eoseth" },
			{ "EOSEUR" , "eoseur" },
			{ "EOSUSD" , "eosusd" },
			{ "EOSXBT" , "eosxbt" },
			{ "GNOETH" , "gnoeth" },
			{ "GNOEUR" , "gnoeur" },
			{ "GNOUSD" , "gnousd" },
			{ "GNOXBT" , "gnoxbt" },
			{ "QTUMCAD" , "qtumcad" },
			{ "QTUMETH" , "qtumeth" },
			{ "QTUMEUR" , "qtumeur" },
			{ "QTUMUSD" , "qtumusd" },
			{ "QTUMXBT" , "qtumxbt" },
			{ "USDTZUSD" , "usdtusd" },
			{ "XETCXETH" , "etceth" },
			{ "XETCXXBT" , "etcxbt" },
			{ "XETCZEUR" , "etceur" },
			{ "XETCZUSD" , "etcusd" },
			{ "XETHXXBT" , "ethxbt" },
			{ "XETHXXBT.d" , "ethxbtd" },
			{ "XETHZCAD" , "ethcad" },
			{ "XETHZCAD.d" , "ethcadd" },
			{ "XETHZEUR" , "etheur" },
			{ "XETHZEUR.d" , "etheurd" },
			{ "XETHZGBP" , "ethgbp" },
			{ "XETHZGBP.d" , "ethgbpd" },
			{ "XETHZJPY" , "ethjpy" },
			{ "XETHZJPY.d" , "ethjpyd" },
			{ "XETHZUSD" , "ethusd" },
			{ "XETHZUSD.d" , "ethusdd" },
			{ "XLTCXXBT" , "ltcxbt" },
			{ "XLTCZEUR" , "ltceur" },
			{ "XLTCZUSD" , "ltcusd" },
			{ "XMLNXETH" , "mlneth" },
			{ "XMLNXXBT" , "mlnxbt" },
			{ "XREPXETH" , "repeth" },
			{ "XREPXXBT" , "repxbt" },
			{ "XREPZEUR" , "repeur" },
			{ "XREPZUSD" , "repusd" },
			{ "XTZCAD" , "xtzcad" },
			{ "XTZETH" , "xtzeth" },
			{ "XTZEUR" , "xtzeur" },
			{ "XTZUSD" , "xtzusd" },
			{ "XTZXBT" , "xtzxbt" },
			{ "XXBTZCAD" , "xbtcad" },
			{ "XXBTZCAD.d" , "xbtcadd" },
			{ "XXBTZEUR" , "xbteur" },
			{ "XXBTZEUR.d" , "xbteurd" },
			{ "XXBTZGBP" , "xbtgbp" },
			{ "XXBTZGBP.d" , "xbtgbpd" },
			{ "XXBTZJPY" , "xbtjpy" },
			{ "XXBTZJPY.d" , "xbtjpyd" },
			{ "XXBTZUSD" , "xbtusd" },
			{ "XXBTZUSD.d" , "xbtusdd" },
			{ "XXDGXXBT" , "xdgxbt" },
			{ "XXLMXXBT" , "xlmxbt" },
			{ "XXLMZEUR" , "xlmeur" },
			{ "XXLMZUSD" , "xlmusd" },
			{ "XXMRXXBT" , "xmrxbt" },
			{ "XXMRZEUR" , "xmreur" },
			{ "XXMRZUSD" , "xmrusd" },
			{ "XXRPXXBT" , "xrpxbt" },
			{ "XXRPZCAD" , "xrpcad" },
			{ "XXRPZEUR" , "xrpeur" },
			{ "XXRPZJPY" , "xrpjpy" },
			{ "XXRPZUSD" , "xrpusd" },
			{ "XZECXXBT" , "zecxbt" },
			{ "XZECZEUR" , "zeceur" },
			{ "XZECZJPY" , "zecjpy" },
			{ "XZECZUSD" , "zecusd" }
		};

        private static readonly IReadOnlyDictionary<string, string> normalizedSymbolToExchangeSymbol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        protected override JToken CheckJsonResponse(JToken json)
        {
            if (!(json is JArray) && json["error"] is JArray error && error.Count != 0)
            {
                throw new APIException(error[0].ToStringInvariant());
            }
            return json["result"] ?? json;
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
            orderResult.MarketSymbol = order["descr"]["pair"].ToStringInvariant();
            orderResult.IsBuy = (order["descr"]["type"].ToStringInvariant() == "buy");
            orderResult.Amount = order["vol"].ConvertInvariant<decimal>();
            orderResult.AmountFilled = order["vol_exec"].ConvertInvariant<decimal>();
            orderResult.Price = order["descr"]["price"].ConvertInvariant<decimal>();
            orderResult.AveragePrice = order["price"].ConvertInvariant<decimal>();

            return orderResult;
        }

        private async Task<IEnumerable<ExchangeOrderResult>> QueryOrdersAsync(string symbol, string path)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JToken result = await MakeJsonRequestAsync<JToken>(path, null, await GetNoncePayloadAsync());
            result = result["open"];
            if (exchangeSymbolToNormalizedSymbol.TryGetValue(symbol, out string normalizedSymbol))
            {
                foreach (JProperty order in result)
                {
                    if (normalizedSymbol == null || order.Value["descr"]["pair"].ToStringInvariant() == normalizedSymbol.ToUpperInvariant())
                    {
                        orders.Add(ParseOrder(order.Name, order.Value));
                    }
                }
            }

            return orders;
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload == null || PrivateApiKey == null || PublicApiKey == null || !payload.ContainsKey("nonce"))
            {
                await CryptoUtility.WritePayloadFormToRequestAsync(request, payload);
            }
            else
            {
                string nonce = payload["nonce"].ToStringInvariant();
                payload.Remove("nonce");
                string form = CryptoUtility.GetFormForPayload(payload);
                // nonce must be first on Kraken
                form = "nonce=" + nonce + (string.IsNullOrWhiteSpace(form) ? string.Empty : "&" + form);
                using (SHA256 sha256 = SHA256Managed.Create())
                {
                    string hashString = nonce + form;
                    byte[] sha256Bytes = sha256.ComputeHash(hashString.ToBytesUTF8());
                    byte[] pathBytes = request.RequestUri.AbsolutePath.ToBytesUTF8();
                    byte[] sigBytes = new byte[sha256Bytes.Length + pathBytes.Length];
                    pathBytes.CopyTo(sigBytes, 0);
                    sha256Bytes.CopyTo(sigBytes, pathBytes.Length);
                    byte[] privateKey = System.Convert.FromBase64String(CryptoUtility.ToUnsecureString(PrivateApiKey));
                    using (System.Security.Cryptography.HMACSHA512 hmac = new System.Security.Cryptography.HMACSHA512(privateKey))
                    {
                        string sign = System.Convert.ToBase64String(hmac.ComputeHash(sigBytes));
                        request.AddHeader("API-Sign", sign);
                    }
                }
                request.AddHeader("API-Key", CryptoUtility.ToUnsecureString(PublicApiKey));
                await CryptoUtility.WriteToRequestAsync(request, form);
            }
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            // https://api.kraken.com/0/public/Assets
            Dictionary<string, ExchangeCurrency> allCoins = new Dictionary<string, ExchangeCurrency>(StringComparer.OrdinalIgnoreCase);

            var currencies = new Dictionary<string, ExchangeCurrency>(StringComparer.OrdinalIgnoreCase);
            JToken array = await MakeJsonRequestAsync<JToken>("/0/public/Assets");
            foreach (JProperty token in array)
            {
                var coin = new ExchangeCurrency
                {
                    CoinType = token.Value["aclass"].ToStringInvariant(),
                    Name = token.Name,
                    FullName = token.Value["altname"].ToStringInvariant()
                };

                currencies[coin.Name] = coin;
            }

            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            JToken result = await MakeJsonRequestAsync<JToken>("/0/public/AssetPairs");
            return (from prop in result.Children<JProperty>() where !prop.Name.Contains(".d") select prop.Name).ToArray();
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            //  {
            //  "BCHEUR": {
            //  "altname": "BCHEUR",
            //  "aclass_base": "currency",
            //  "base": "BCH",
            //  "aclass_quote": "currency",
            //  "quote": "ZEUR",
            //  "lot": "unit",
            //  "pair_decimals": 1,
            //  "lot_decimals": 8,
            //  "lot_multiplier": 1,
            //  "leverage_buy": [],
            //  "leverage_sell": [],
            //  "fees": [
            //    [
            //      0,
            //      0.26
            //    ],
            //    [
            //      50000,
            //      0.24
            //    ],
            //    [
            //      100000,
            //      0.22
            //    ],
            //    [
            //      250000,
            //      0.2
            //    ],
            //    [
            //      500000,
            //      0.18
            //    ],
            //    [
            //      1000000,
            //      0.16
            //    ],
            //    [
            //      2500000,
            //      0.14
            //    ],
            //    [
            //      5000000,
            //      0.12
            //    ],
            //    [
            //      10000000,
            //      0.1
            //    ]
            //  ],
            //  "fees_maker": [
            //    [
            //      0,
            //      0.16
            //    ],
            //    [
            //      50000,
            //      0.14
            //    ],
            //    [
            //      100000,
            //      0.12
            //    ],
            //    [
            //      250000,
            //      0.1
            //    ],
            //    [
            //      500000,
            //      0.08
            //    ],
            //    [
            //      1000000,
            //      0.06
            //    ],
            //    [
            //      2500000,
            //      0.04
            //    ],
            //    [
            //      5000000,
            //      0.02
            //    ],
            //    [
            //      10000000,
            //      0
            //    ]
            //  ],
            //  "fee_volume_currency": "ZUSD",
            //  "margin_call": 80,
            //  "margin_stop": 40
            //}
            //}
            var markets = new List<ExchangeMarket>();
            JToken allPairs = await MakeJsonRequestAsync<JToken>("/0/public/AssetPairs");
            var res = (from prop in allPairs.Children<JProperty>() select prop).ToArray();

            foreach (JProperty prop in res)
            {
                JToken pair = prop.Value;
                var quantityStepSize = Math.Pow(0.1, pair["lot_decimals"].ConvertInvariant<int>()).ConvertInvariant<decimal>();
                var market = new ExchangeMarket
                {
                    IsActive = !prop.Name.Contains(".d"),
                    MarketSymbol = prop.Name,
                    MinTradeSize = quantityStepSize,
                    MarginEnabled = pair["leverage_buy"].Children().Any() || pair["leverage_sell"].Children().Any(),
                    BaseCurrency = pair["base"].ToStringInvariant(),
                    QuoteCurrency = pair["quote"].ToStringInvariant(),
                    QuantityStepSize = quantityStepSize,
                    PriceStepSize = Math.Pow(0.1, pair["pair_decimals"].ConvertInvariant<int>()).ConvertInvariant<decimal>()
                };
                markets.Add(market);
            }

            return markets;
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            var marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
            var normalizedPairsList = marketSymbols.Select(symbol => NormalizeMarketSymbol(symbol)).ToList();
            var csvPairsList = string.Join(",", normalizedPairsList);
            JToken apiTickers = await MakeJsonRequestAsync<JToken>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", csvPairsList } });
            var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (string marketSymbol in marketSymbols)
            {
                JToken ticker = apiTickers[marketSymbol];
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, ConvertToExchangeTicker(marketSymbol, ticker)));
            }
            return tickers;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken apiTickers = await MakeJsonRequestAsync<JToken>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", NormalizeMarketSymbol(marketSymbol) } });
            JToken ticker = apiTickers[marketSymbol];
            return ConvertToExchangeTicker(marketSymbol, ticker);
        }

        private ExchangeTicker ConvertToExchangeTicker(string symbol, JToken ticker)
        {
            decimal last = ticker["c"][0].ConvertInvariant<decimal>();
            var (baseCurrency, quoteCurrency) = ExchangeMarketSymbolToCurrencies(symbol);
            return new ExchangeTicker
            {
                MarketSymbol = symbol,
                Ask = ticker["a"][0].ConvertInvariant<decimal>(),
                Bid = ticker["b"][0].ConvertInvariant<decimal>(),
                Last = last,
                Volume = new ExchangeVolume
                {
                    QuoteCurrencyVolume = ticker["v"][1].ConvertInvariant<decimal>(),
                    QuoteCurrency = quoteCurrency,
                    BaseCurrencyVolume = ticker["v"][1].ConvertInvariant<decimal>() * ticker["p"][1].ConvertInvariant<decimal>(),
                    BaseCurrency = baseCurrency,
                    Timestamp = CryptoUtility.UtcNow
                }
            };
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/0/public/Depth?pair=" + marketSymbol + "&count=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(obj[marketSymbol], maxCount: maxCount);
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            string baseUrl = "/0/public/Trades?pair=" + marketSymbol;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (startDate != null)
                {
                    url += "&since=" + (long)(CryptoUtility.UnixTimestampFromDateTimeMilliseconds(startDate.Value) * 1000000.0);
                }
                JToken result = await MakeJsonRequestAsync<JToken>(url);
                if (result == null)
                {
                    break;
                }
                if (!(result[marketSymbol] is JArray outerArray) || outerArray.Count == 0)
                {
                    break;
                }
                if (startDate != null)
                {
                    startDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(result["last"].ConvertInvariant<double>() / 1000000.0d);
                }
                foreach (JToken trade in outerArray.Children())
                {
                    trades.Add(trade.ParseTrade(1, 0, 3, 2, TimestampType.UnixSecondsDouble, null, "b"));
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                if (!callback(trades))
                {
                    break;
                }
                trades.Clear();
                if (startDate == null)
                {
                    break;
                }
                Task.Delay(1000).Wait();
            }
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // https://api.kraken.com/0/public/OHLC
            // pair = asset pair to get OHLC data for, interval = time frame interval in minutes(optional):, 1(default), 5, 15, 30, 60, 240, 1440, 10080, 21600, since = return committed OHLC data since given id(optional.exclusive)
            // array of array entries(<time>, <open>, <high>, <low>, <close>, <vwap>, <volume>, <count>)
            startDate = startDate ?? CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(1.0));
            endDate = endDate ?? CryptoUtility.UtcNow;
            JToken json = await MakeJsonRequestAsync<JToken>("/0/public/OHLC?pair=" + marketSymbol + "&interval=" + (periodSeconds / 60).ToStringInvariant() + "&since=" + startDate);
            List<MarketCandle> candles = new List<MarketCandle>();
            if (json.Children().Count() != 0)
            {
                JProperty prop = json.Children().First() as JProperty;
                foreach (JToken jsonCandle in prop.Value)
                {
                    MarketCandle candle = this.ParseCandle(jsonCandle, marketSymbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixSeconds, 6, null, 5);
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
            JToken result = await MakeJsonRequestAsync<JToken>("/0/private/Balance", null, await GetNoncePayloadAsync());
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

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            JToken result = await MakeJsonRequestAsync<JToken>("/0/private/TradeBalance", null, await GetNoncePayloadAsync());
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
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
            object nonce = await GenerateNonceAsync();
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "pair", order.MarketSymbol },
                { "type", (order.IsBuy ? "buy" : "sell") },
                { "ordertype", order.OrderType.ToString().ToLowerInvariant() },
                { "volume", order.RoundAmount().ToStringInvariant() },
                { "nonce", nonce }
            };
            if (order.OrderType != OrderType.Market)
            {
                payload.Add("price", order.Price.ToStringInvariant());
            }
            order.ExtraParameters.CopyTo(payload);

            JToken token = await MakeJsonRequestAsync<JToken>("/0/private/AddOrder", null, payload);
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                OrderDate = CryptoUtility.UtcNow
            };
            if (token["txid"] is JArray array)
            {
                result.OrderId = array[0].ToStringInvariant();
            }
            return result;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            object nonce = await GenerateNonceAsync();
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "txid", orderId },
                { "nonce", nonce }
            };
            JToken result = await MakeJsonRequestAsync<JToken>("/0/private/QueryOrders", null, payload);
            ExchangeOrderResult orderResult = new ExchangeOrderResult { OrderId = orderId };
            if (result == null || result[orderId] == null)
            {
                orderResult.Message = "Unknown Error";
                return orderResult;
            }

            return ParseOrder(orderId, result[orderId]); ;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            return await QueryOrdersAsync(marketSymbol, "/0/private/OpenOrders");
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            string path = "/0/private/ClosedOrders";
            if (afterDate != null)
            {
                path += "?start=" + ((long)afterDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
            }
            return await QueryOrdersAsync(marketSymbol, path);
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            object nonce = await GenerateNonceAsync();
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "txid", orderId },
                { "nonce", nonce }
            };
            await MakeJsonRequestAsync<JToken>("/0/private/CancelOrder", null, payload);
        }
    }

    public partial class ExchangeName { public const string Kraken = "Kraken"; }
}
