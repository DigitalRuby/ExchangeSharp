/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public class ExchangeBinanceAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.binance.com/api/v1";
        public string BaseUrlPrivate { get; set; } = "https://www.binance.com/api/v3";
        public override string Name => ExchangeName.Binance;

        public override string NormalizeSymbol(string symbol)
        {
            if (symbol != null)
            {
                symbol = symbol.Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
            }
            return symbol;
        }

        private void CheckError(JToken result)
        {
            if (result != null && !(result is JArray) && result["status"] != null && result["code"] != null)
            {
                throw new ExchangeAPIException(result["code"].Value<string>() + ": " + (result["msg"] != null ? result["msg"].Value<string>() : "Unknown Error"));
            }
        }

        private ExchangeTicker ParseTicker(string symbol, JToken token)
        {
            // {"priceChange":"-0.00192300","priceChangePercent":"-4.735","weightedAvgPrice":"0.03980955","prevClosePrice":"0.04056700","lastPrice":"0.03869000","lastQty":"0.69300000","bidPrice":"0.03858500","bidQty":"38.35000000","askPrice":"0.03869000","askQty":"31.90700000","openPrice":"0.04061300","highPrice":"0.04081900","lowPrice":"0.03842000","volume":"128015.84300000","quoteVolume":"5096.25362239","openTime":1512403353766,"closeTime":1512489753766,"firstId":4793094,"lastId":4921546,"count":128453}
            return new ExchangeTicker
            {
                Ask = (decimal)token["askPrice"],
                Bid = (decimal)token["bidPrice"],
                Last = (decimal)token["lastPrice"],
                Volume = new ExchangeVolume
                {
                    PriceAmount = (decimal)token["volume"],
                    PriceSymbol = symbol,
                    QuantityAmount = (decimal)token["quoteVolume"],
                    QuantitySymbol = symbol,
                    Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)token["closeTime"])
                }
            };
        }

        private ExchangeOrderBook ParseOrderBook(JToken token)
        {
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JArray array in token["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Price = (decimal)array[0], Amount = (decimal)array[1] });
            }
            foreach (JArray array in token["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Price = (decimal)array[0], Amount = (decimal)array[1] });
            }
            return book;
        }

        public override IReadOnlyCollection<string> GetSymbols()
        {
            List<string> symbols = new List<string>();
            JToken obj = MakeJsonRequest<JToken>("/ticker/allPrices");
            CheckError(obj);
            foreach (JToken token in obj)
            {
                // bug I think in the API returns numbers as symbol names... WTF.
                string symbol = (string)token["symbol"];
                if (!long.TryParse(symbol, out long tmp))
                {
                    symbols.Add(symbol);
                }
            }
            return symbols;
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<JToken>("/ticker/24hr?symbol=" + symbol);
            CheckError(obj);
            return ParseTicker(symbol, obj);
        }

        public override IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            // TODO: I put in a support request to add a symbol field to https://www.binance.com/api/v1/ticker/24hr, until then multi tickers in one request is not supported
            return base.GetTickers();
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = MakeJsonRequest<JToken>("/depth?symbol=" + symbol + "&limit=" + maxCount);
            CheckError(obj);
            return ParseOrderBook(obj);
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            /* [ {
            "a": 26129,         // Aggregate tradeId
		    "p": "0.01633102",  // Price
		    "q": "4.70443515",  // Quantity
		    "f": 27781,         // First tradeId
		    "l": 27781,         // Last tradeId
		    "T": 1498793709153, // Timestamp
		    "m": true,          // Was the buyer the maker?
		    "M": true           // Was the trade the best price match?
            } ] */

            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/aggTrades?symbol=" + symbol;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            DateTime cutoff = DateTime.UtcNow;

            while (true)
            {
                url = baseUrl;
                if (sinceDateTime != null)
                {
                    url += "&startTime=" + CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value) +
                        "&endTime=" + CryptoUtility.UnixTimestampFromDateTimeMilliseconds(sinceDateTime.Value + TimeSpan.FromDays(1.0));
                }
                JArray obj = MakeJsonRequest<Newtonsoft.Json.Linq.JArray>(url);
                if (obj == null || obj.Count == 0)
                {
                    break;
                }
                if (sinceDateTime != null)
                {
                    sinceDateTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(obj.Last["T"].Value<long>());
                    if (sinceDateTime.Value > cutoff)
                    {
                        sinceDateTime = null;
                    }
                }
                foreach (JToken token in obj)
                {
                    // TODO: Binance doesn't provide a buy or sell type, I've put in a request for them to add this
                    trades.Add(new ExchangeTrade
                    {
                        Amount = token["q"].Value<decimal>(),
                        Price = token["p"].Value<decimal>(),
                        Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token["T"].Value<long>()),
                        Id = token["a"].Value<long>(),
                        IsBuy = token["m"].Value<bool>()
                    });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                foreach (ExchangeTrade t in trades)
                {
                    yield return t;
                }
                trades.Clear();
                if (sinceDateTime == null)
                {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
