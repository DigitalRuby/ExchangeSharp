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
		public override string BaseUrlWebSocket { get; set; } = "wss://ws.kraken.com";

        public ExchangeKrakenAPI()
        {
            RequestMethod = "POST";
            RequestContentType = "application/x-www-form-urlencoded";
            MarketSymbolSeparator = string.Empty;
            NonceStyle = NonceStyle.UnixMilliseconds;
        }

        private IReadOnlyDictionary<string, string> exchangeCurrencyToNormalizedCurrency = new Dictionary<string, string>();
        private IReadOnlyDictionary<string, string> normalizedCurrencyToExchangeCurrency = new Dictionary<string, string>();
        private IReadOnlyDictionary<string, string> exchangeSymbolToNormalizedSymbol = new Dictionary<string, string>();
        private IReadOnlyDictionary<string, string> normalizedSymbolToExchangeSymbol = new Dictionary<string, string>();
        private IReadOnlyDictionary<string, string> exchangeCurrenciesToMarketSymbol = new Dictionary<string, string>();

        static ExchangeKrakenAPI()
        {
            ExchangeGlobalCurrencyReplacements[typeof(ExchangeKrakenAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("XBT", "BTC"),
                new KeyValuePair<string, string>("XDG", "DOGE")
            };
        }

        /// <summary>
        /// Populate dictionaries to deal with Kraken weirdness in currency and market names, will use cache if it exists
        /// </summary>
        /// <returns>Task</returns>
        private async Task PopulateLookupTables()
        {
            await Cache.Get<object>(nameof(PopulateLookupTables), async () =>
            {
                IReadOnlyDictionary<string, ExchangeCurrency> currencies = await GetCurrenciesAsync();
                ExchangeMarket[] markets = (await GetMarketSymbolsMetadataAsync())?.ToArray();
                if (markets == null || markets.Length == 0)
                {
                    return new CachedItem<object>();
                }

                Dictionary<string, string> exchangeCurrencyToNormalizedCurrencyNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> normalizedCurrencyToExchangeCurrencyNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> exchangeSymbolToNormalizedSymbolNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> normalizedSymbolToExchangeSymbolNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> exchangeCurrenciesToMarketSymbolNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, ExchangeCurrency> kv in currencies)
                {
                    string altName = kv.Value.AltName;
                    switch (altName.ToLowerInvariant())
                    {
                        // wtf kraken...
                        case "xbt": altName = "BTC"; break;
                        case "xdg": altName = "DOGE"; break;
                    }
                    exchangeCurrencyToNormalizedCurrencyNew[kv.Value.Name] = altName;
                    normalizedCurrencyToExchangeCurrencyNew[altName] = kv.Value.Name;
                }

                foreach (ExchangeMarket market in markets.Where(m => !m.MarketSymbol.Contains(".d")))
                {
                    string baseSymbol = market.BaseCurrency;
                    string quoteSymbol = market.QuoteCurrency;
                    string baseNorm = exchangeCurrencyToNormalizedCurrencyNew[market.BaseCurrency];
                    string quoteNorm = exchangeCurrencyToNormalizedCurrencyNew[market.QuoteCurrency];
                    string marketSymbolNorm = baseNorm + quoteNorm;
                    string marketSymbol = market.MarketSymbol;
                    exchangeSymbolToNormalizedSymbolNew[marketSymbol] = marketSymbolNorm;
                    normalizedSymbolToExchangeSymbolNew[marketSymbolNorm] = marketSymbol;
                    exchangeCurrenciesToMarketSymbolNew[baseSymbol + quoteSymbol] = marketSymbol;
                    exchangeCurrenciesToMarketSymbolNew[quoteSymbol + baseSymbol] = marketSymbol;
                }

                exchangeCurrencyToNormalizedCurrency = exchangeCurrencyToNormalizedCurrencyNew;
                normalizedCurrencyToExchangeCurrency = normalizedCurrencyToExchangeCurrencyNew;
                exchangeSymbolToNormalizedSymbol = exchangeSymbolToNormalizedSymbolNew;
                normalizedSymbolToExchangeSymbol = normalizedSymbolToExchangeSymbolNew;
                exchangeCurrenciesToMarketSymbol = exchangeCurrenciesToMarketSymbolNew;

                return new CachedItem<object>(new object(), CryptoUtility.UtcNow.AddHours(4.0));
            });
        }

        public override async Task<(string baseCurrency, string quoteCurrency)> ExchangeMarketSymbolToCurrenciesAsync(string marketSymbol)
        {
            ExchangeMarket market = await GetExchangeMarketFromCacheAsync(marketSymbol);
            if (market == null)
            {
                throw new ArgumentException("Unable to get currencies for market symbol " + marketSymbol);
            }
            return (market.BaseCurrency, market.QuoteCurrency);
        }

        public override async Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
        {
            await PopulateLookupTables();
            var (baseCurrency, quoteCurrency) = await ExchangeMarketSymbolToCurrenciesAsync(marketSymbol);
            if (!exchangeCurrencyToNormalizedCurrency.TryGetValue(baseCurrency, out string baseCurrencyNormalized))
            {
                baseCurrencyNormalized = baseCurrency;
            }
            if (!exchangeCurrencyToNormalizedCurrency.TryGetValue(quoteCurrency, out string quoteCurrencyNormalized))
            {
                quoteCurrencyNormalized = quoteCurrency;
            }
            return baseCurrencyNormalized + GlobalMarketSymbolSeparatorString + quoteCurrencyNormalized;
        }

        public override async Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
        {
            await PopulateLookupTables();
            string[] pieces = marketSymbol.Split('-');
            if (pieces.Length < 2)
            {
                throw new ArgumentException("Market symbol must be at least two pieces");
            }
            string marketSymbol2 = pieces[0] + pieces[1];
            if (!normalizedCurrencyToExchangeCurrency.TryGetValue(pieces[0], out string baseCurrencyExchange))
            {
                baseCurrencyExchange = pieces[0];
            }
            if (!normalizedCurrencyToExchangeCurrency.TryGetValue(pieces[1], out string quoteCurrencyExchange))
            {
                quoteCurrencyExchange = pieces[1];
            }
            if (!exchangeCurrenciesToMarketSymbol.TryGetValue(baseCurrencyExchange + quoteCurrencyExchange, out string exchangeMarketSymbol))
            {
                throw new ArgumentException("Unable to find exchange market for global market symbol " + marketSymbol);
            }
            return exchangeMarketSymbol;
        }

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

        private async Task<ExchangeOrderResult> ParseHistoryOrder(string orderId, JToken order)
        {
//            //{{
//            "ordertxid": "ONKWWN-3LWZ7-4SDZVJ",
//  "postxid": "TKH2SE-M7IF5-CFI7LT",
//  "pair": "XXRPZUSD",
//  "time": 1537779676.7525,
//  "type": "buy",
//  "ordertype": "limit",
//  "price": "0.54160000",
//  "cost": "16.22210000",
//  "fee": "0.02595536",
//  "vol": "29.95217873",
//  "margin": "0.00000000",
//  "misc": ""
//}
//    }

            ExchangeOrderResult orderResult = new ExchangeOrderResult { OrderId = orderId };
            orderResult.Result = ExchangeAPIOrderResult.Filled;
            orderResult.Message = "";
            orderResult.OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(order["time"].ConvertInvariant<double>());
            orderResult.MarketSymbol = order["pair"].ToStringInvariant();
            orderResult.IsBuy = (order["type"].ToStringInvariant() == "buy");
            orderResult.Amount = order["vol"].ConvertInvariant<decimal>();
            orderResult.Fees = order["fee"].ConvertInvariant<decimal>();
            orderResult.Price = order["price"].ConvertInvariant<decimal>();
            orderResult.AveragePrice = order["price"].ConvertInvariant<decimal>();
            orderResult.TradeId = order["postxid"].ToStringInvariant(); //verify which is orderid & tradeid
            orderResult.OrderId = order["ordertxid"].ToStringInvariant();  //verify which is orderid & tradeid
            orderResult.AmountFilled = order["vol"].ConvertInvariant<decimal>();
            orderResult.FillDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(order["time"].ConvertInvariant<double>());

            string[] pairs = (await ExchangeMarketSymbolToGlobalMarketSymbolAsync(order["pair"].ToStringInvariant())).Split('-');
            orderResult.FeesCurrency = pairs[1];

            return orderResult;
        }

        private async Task<IEnumerable<ExchangeOrderResult>> QueryOrdersAsync(string symbol, string path)
        {
            await PopulateLookupTables();
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

        private async Task<IEnumerable<ExchangeOrderResult>> QueryClosedOrdersAsync(string symbol, string path)
        {
            await PopulateLookupTables();
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JToken result = await MakeJsonRequestAsync<JToken>(path, null, await GetNoncePayloadAsync());
            result = result["closed"];
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
            else
            {
                foreach (JProperty order in result)
                {
                    orders.Add(ParseOrder(order.Name, order.Value));
                }
            }

            return orders;
        }

        private async Task<IEnumerable<ExchangeOrderResult>> QueryHistoryOrdersAsync(string symbol, string path)
        {
            await PopulateLookupTables();
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            JToken result = await MakeJsonRequestAsync<JToken>(path, null, await GetNoncePayloadAsync());
            result = result["trades"];
            if (exchangeSymbolToNormalizedSymbol.TryGetValue(symbol, out string normalizedSymbol))
            {
                foreach (JProperty order in result)
                {
                    if (normalizedSymbol == null || order.Value["pair"].ToStringInvariant() == symbol.ToUpperInvariant())
                    {
                        orders.Add(await ParseHistoryOrder(order.Name, order.Value));
                    }
                }
            }
            else
            {
                foreach (JProperty order in result)
                {
                    orders.Add(await ParseHistoryOrder(order.Name, order.Value));
                }
            }

            return orders;
        }

        //private async Task<IEnumerable<ExchangeOrderResult>> QueryClosedOrdersAsync(string symbol, string path)
        //{
        //    List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
        //    JToken result = await MakeJsonRequestAsync<JToken>(path, null, await GetNoncePayloadAsync());
        //    //result = result["closed"];
        //    foreach (JProperty order in result)
        //    {
        //        orders.Add(ParseOrder(order.Name, order.Value));
        //    }


        //    //if (exchangeSymbolToNormalizedSymbol.TryGetValue(symbol, out string normalizedSymbol))
        //    //{
        //    //    foreach (JProperty order in result)
        //    //    {
        //    //        if (normalizedSymbol == null || order.Value["descr"]["pair"].ToStringInvariant() == normalizedSymbol.ToUpperInvariant())
        //    //        {
        //    //            orders.Add(ParseOrder(order.Name, order.Value));
        //    //        }
        //    //    }
        //    //}

        //    return orders;
        //}


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
                    FullName = token.Name,
                    AltName = token.Value["altname"].ToStringInvariant()
                };

                currencies[coin.Name] = coin;
            }

            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            JToken result = await MakeJsonRequestAsync<JToken>("/0/public/AssetPairs");
            return result.Children<JProperty>().Where(p => !p.Name.Contains(".d")).Select(p => p.Name).ToArray();
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {   //{"ADACAD": {
			//  "altname": "ADACAD",
			//  "wsname": "ADA/CAD",
			//  "aclass_base": "currency",
			//  "base": "ADA",
			//  "aclass_quote": "currency",
			//  "quote": "ZCAD",
			//  "lot": "unit",
			//  "pair_decimals": 6,
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
			//}}
			var markets = new List<ExchangeMarket>();
            JToken allPairs = await MakeJsonRequestAsync<JToken>("/0/public/AssetPairs");
            var res = (from prop in allPairs.Children<JProperty>() select prop).ToArray();

            foreach (JProperty prop in res.Where(p => !p.Name.EndsWith(".d")))
            {
                JToken pair = prop.Value;
                JToken child = prop.Children().FirstOrDefault();
                var quantityStepSize = Math.Pow(0.1, pair["lot_decimals"].ConvertInvariant<int>()).ConvertInvariant<decimal>();
                var market = new ExchangeMarket
                {
                    IsActive = true,
                    MarketSymbol = prop.Name,
					AltMarketSymbol = child["altname"].ToStringInvariant(),
					AltMarketSymbol2 = child["wsname"].ToStringInvariant(),
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
            foreach (string marketSymbol in normalizedPairsList)
            {
                JToken ticker;
                ticker = apiTickers[marketSymbol];

                #region Fix for pairs that are not found like USDTZUSD
                if (ticker == null)
                {
                    // Some pairs like USDTZUSD are not found, but they can be found using Metadata.
                    var symbols = (await GetMarketSymbolsMetadataAsync()).ToList();
                    var symbol = symbols.FirstOrDefault(a => a.MarketSymbol.Replace("/", "").Equals(marketSymbol));
                    ticker = apiTickers[symbol.BaseCurrency + symbol.QuoteCurrency];
                }
                #endregion

                try
                {
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, await ConvertToExchangeTickerAsync(marketSymbol, ticker)));
                }
                catch
                {
                    // if Kraken throws bogus json at us, just eat it
                }
            }
            return tickers;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken apiTickers = await MakeJsonRequestAsync<JToken>("/0/public/Ticker", null, new Dictionary<string, object> { { "pair", NormalizeMarketSymbol(marketSymbol) } });
            JToken ticker = apiTickers[marketSymbol];
            return await ConvertToExchangeTickerAsync(marketSymbol, ticker);
        }

        private async Task<ExchangeTicker> ConvertToExchangeTickerAsync(string symbol, JToken ticker)
        {
            decimal last = ticker["c"][0].ConvertInvariant<decimal>();
            var (baseCurrency, quoteCurrency) = await ExchangeMarketSymbolToCurrenciesAsync(symbol);
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

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
		{
			List<ExchangeTrade> trades = new List<ExchangeTrade>();

			//https://www.kraken.com/features/api#public-market-data note kraken does not specify but it appears the limit is around 1860 (weird)
            //https://api.kraken.com/0/public/Trades?pair=BCHUSD&count=1860
			//needs testing of different marketsymbols to establish if limit varies
			//gonna use 1500 for now

			int requestLimit = (limit == null || limit < 1 || limit > 1500) ? 1500 : (int)limit;
			string url = "/0/public/Trades?pair=" + marketSymbol +  "&count=" + requestLimit;
			//string url = "/trades/t" + marketSymbol + "/hist?sort=" + "-1"  + "&limit=" + requestLimit;

			JToken result = await MakeJsonRequestAsync<JToken>(url);

			//if (result != null && (!(result[marketSymbol] is JArray outerArray) || outerArray.Count == 0)) {
				if(result != null && result[marketSymbol] is JArray outerArray && outerArray.Count > 0)  {
					foreach(JToken trade in outerArray.Children()) {
					trades.Add(trade.ParseTrade(1, 0, 3, 2, TimestampType.UnixSecondsDouble, null, "b"));
				}
			}

			return trades.AsEnumerable().Reverse(); //Descending order (ie newest trades first)
		}

		protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
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
                { "trading_agreement", "agree" },
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

        //protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        //{
        //    string path = "/0/private/ClosedOrders";
        //    if (afterDate != null)
        //    {
        //        path += "?start=" + ((long)afterDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
        //    }
        //    return await QueryClosedOrdersAsync(marketSymbol, path);
        //}

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            string path = "/0/private/TradesHistory";
            if (afterDate != null)
            {
                path += "?start=" + ((long)afterDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
            }
            return await QueryHistoryOrdersAsync(marketSymbol, path);
        }

        //protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        //{
        //    var payload = await GetNoncePayloadAsync();
        //    if (marketSymbol == null)
        //        throw new APIException("BitBank requires marketSymbol when getting completed orders");
        //    payload.Add("pair", NormalizeMarketSymbol(marketSymbol));
        //    if (afterDate != null)
        //        payload.Add("since", afterDate.ConvertInvariant<double>());
        //    JToken token = await MakeJsonRequestAsync<JToken>($"/user/spot/trade_history", baseUrl: BaseUrlPrivate, payload: payload);
        //    return token["trades"].Select(t => TradeHistoryToExchangeOrderResult(t));
        //}

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

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectWebSocketAsync(null, messageCallback: async (_socket, msg) =>
			{
				JToken token = JToken.Parse(msg.ToStringFromUTF8());
				if (token.Type == JTokenType.Array && token[2].ToStringInvariant() == "trade")
				{   //[
					//  0,
					//  [

					//	[
					//	  "5541.20000",
					//	  "0.15850568",
					//	  "1534614057.321597",
					//	  "s",
					//	  "l",
					//	  ""
					//	],

					//	[
					//	  "6060.00000",
					//	  "0.02455000",
					//	  "1534614057.324998",
					//	  "b",
					//	  "l",
					//	  ""
					//	]
					//  ],
					//  "trade",
					//  "XBT/USD"
					//]
					string marketSymbol = token[3].ToStringInvariant();
					foreach (var tradesToken in token[1])
					{
						var trade = tradesToken.ParseTradeKraken(amountKey: 1, priceKey: 0,
								typeKey: 3, timestampKey: 2,
								TimestampType.UnixSecondsDouble, idKey: null,
								typeKeyIsBuyValue: "b");
						await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade));
					}
				}
				else if (token["event"].ToStringInvariant() == "heartbeat") { }
				else if (token["status"].ToStringInvariant() == "error")
				{   //{{
					//  "errorMessage": "Currency pair not in ISO 4217-A3 format ADACAD",
					//  "event": "subscriptionStatus",
					//  "pair": "ADACAD",
					//  "status": "error",
					//  "subscription": {
					//    "name": "trade"
					//  }
					//}}
					Logger.Info(token["errorMessage"].ToStringInvariant());
				}
				else if (token["status"].ToStringInvariant() == "online")
				{   //{{
					//  "connectionID": 9077277725533272053,
					//  "event": "systemStatus",
					//  "status": "online",
					//  "version": "0.2.0"
					//}}
				}
			}, connectCallback: async (_socket) =>
			{
				//{
				//  "event": "subscribe",
				//  "pair": [
				//    "XBT/USD","XBT/EUR"
				//  ],
				//  "subscription": {
				//    "name": "ticker"
				//  }
				//}
				await PopulateLookupTables(); // prime cache
				Task<string>[] marketSymbolsArray = marketSymbols.Select(async (m) =>
                {
                    ExchangeMarket market = await GetExchangeMarketFromCacheAsync(m);
                    if (market == null)
                    {
                        return null;
                    }
                    return market.AltMarketSymbol2;
                }).ToArray();
                List<string> marketSymbolList = new List<string>();
                foreach (Task<string> ms in marketSymbolsArray)
                {
                    string result = await ms;
                    if (result != null)
                    {
                        marketSymbolList.Add(result);
                    }
                }
                await _socket.SendMessageAsync(new
				{
					@event = "subscribe",
					pair = marketSymbolList,
                    subscription = new { name = "trade" }
				});
			});
		}
	}

    public partial class ExchangeName { public const string Kraken = "Kraken"; }
}
