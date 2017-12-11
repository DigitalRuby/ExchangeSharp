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
    public class ExchangeBithumbAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.bithumb.com";
        public override string Name => ExchangeName.Bithumb;

        private static readonly char[] normalizeSeps = new char[] { '-', '_' };

        public override string NormalizeSymbol(string symbol)
        {
            if (symbol != null)
            {
                int pos = symbol.IndexOfAny(normalizeSeps);
                if (pos >= 0)
                {
                    symbol = symbol.Substring(0, pos).ToLowerInvariant();
                }
            }
            return symbol;
        }

        private string StatusToError(string status)
        {
            switch (status)
            {
                case "5100": return "Bad Request";
                case "5200": return "Not Member";
                case "5300": return "Invalid Apikey";
                case "5302": return "Method Not Allowed";
                case "5400": return "Database Fail";
                case "5500": return "Invalid Parameter";
                case "5600": return "Custom Notice";
                case "5900": return "Unknown Error";
                default: return status;
            }
        }

        private void CheckError(JToken result)
        {
            if (result != null && !(result is JArray) && result["status"] != null && result["status"].Value<string>() != "0000")
            {
                throw new APIException(result["status"].Value<string>() + ": " + result["message"].Value<string>());
            }
        }
        
        private JToken MakeRequestBithumb(ref string symbol, string subUrl)
        {
            symbol = NormalizeSymbol(symbol);
            JObject obj = MakeJsonRequest<JObject>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty));
            CheckError(obj);
            return obj["data"];
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data, DateTime? date)
        {
            return new ExchangeTicker
            {
                Ask = (decimal)data["sell_price"],
                Bid = (decimal)data["buy_price"],
                Last = (decimal)data["buy_price"], // Silly Bithumb doesn't provide the last actual trade value in the ticker,
                Volume = new ExchangeVolume
                {
                    PriceAmount = (decimal)data["average_price"],
                    PriceSymbol = "KRW",
                    QuantityAmount = (decimal)data["units_traded"],
                    QuantitySymbol = symbol,
                    Timestamp = date ?? CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)data["date"])
                }
            };
        }

        private ExchangeOrderBook ParseOrderBook(JToken data)
        {
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JToken token in data["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = (decimal)token["quantity"], Price = (decimal)token["price"] });
            }
            foreach (JToken token in data["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = (decimal)token["quantity"], Price = (decimal)token["price"] });
            }
            return book;
        }

        public override IEnumerable<string> GetSymbols()
        {
            List<string> symbols = new List<string>();
            string symbol = "all";
            JToken data = MakeRequestBithumb(ref symbol, "/public/ticker/$SYMBOL$");
            foreach (JProperty token in data)
            {
                if (token.Name != "date")
                {
                    symbols.Add(token.Name);
                }
            }
            return symbols;
        }

        public override ExchangeTicker GetTicker(string symbol)
        {
            JToken data = MakeRequestBithumb(ref symbol, "/public/ticker/$SYMBOL$");
            return ParseTicker(symbol, data, null);
        }

        public override IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            string symbol = "all";
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            JToken data = MakeRequestBithumb(ref symbol, "/public/ticker/$SYMBOL$");
            DateTime date = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((long)data["date"]);
            foreach (JProperty token in data)
            {
                if (token.Name != "date")
                {
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(token.Name, ParseTicker(token.Name, token.Value, date)));
                }
            }
            return tickers;
        }

        public override ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100)
        {
            JToken data = MakeRequestBithumb(ref symbol, "/public/orderbook/$SYMBOL$");
            return ParseOrderBook(data);
        }

        public override IEnumerable<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100)
        {
            string symbol = "all";
            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            JToken data = MakeRequestBithumb(ref symbol, "/public/orderbook/$SYMBOL$");
            foreach (JProperty book in data)
            {
                if (book.Name != "timestamp" && book.Name != "payment_currency")
                {
                    books.Add(new KeyValuePair<string, ExchangeOrderBook>(book.Name, ParseOrderBook(book.Value)));
                }
            }
            return books;
        }

        public override IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null)
        {
            JToken data = MakeRequestBithumb(ref symbol, "/public/recent_transactions/$SYMBOL$");
            foreach (JToken token in data)
            {
                yield return new ExchangeTrade
                {
                    Amount = (decimal)token["units_traded"],
                    Price = (decimal)token["price"],
                    Id = -1,
                    IsBuy = (string)token["type"] == "bid",
                    Timestamp = (DateTime)token["transaction_date"]
                };
            }
        }
    }
}
