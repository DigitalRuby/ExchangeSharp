/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed class ExchangeBithumbAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.bithumb.com";
        public override string Name => ExchangeName.Bithumb;

        private static readonly char[] normalizeSeps = new char[] { '-', '_' };

        public ExchangeBithumbAPI()
        {
        }

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

        public override string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            return symbol + GlobalSymbolSeparator + "KRW";
        }

        public override string GlobalSymbolToExchangeSymbol(string symbol)
        {
            return symbol.Substring(0, symbol.IndexOf(GlobalSymbolSeparator));
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
            if (result != null && !(result is JArray) && result["status"] != null && result["status"].ToStringInvariant() != "0000")
            {
                throw new APIException(result["status"].ToStringInvariant() + ": " + result["message"].ToStringInvariant());
            }
        }
        
        private async Task<Tuple<JToken, string>> MakeRequestBithumbAsync(string symbol, string subUrl)
        {
            symbol = NormalizeSymbol(symbol);
            JObject obj = await MakeJsonRequestAsync<JObject>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty));
            CheckError(obj);
            return new Tuple<JToken, string>(obj["data"], symbol);
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data, DateTime? date)
        {
            return new ExchangeTicker
            {
                Ask = data["sell_price"].ConvertInvariant<decimal>(),
                Bid = data["buy_price"].ConvertInvariant<decimal>(),
                Last = data["buy_price"].ConvertInvariant<decimal>(), // Silly Bithumb doesn't provide the last actual trade value in the ticker,
                Volume = new ExchangeVolume
                {
                    BaseVolume = data["average_price"].ConvertInvariant<decimal>(),
                    BaseSymbol = "KRW",
                    ConvertedVolume = data["units_traded"].ConvertInvariant<decimal>(),
                    ConvertedSymbol = symbol,
                    Timestamp = date ?? CryptoUtility.UnixTimeStampToDateTimeMilliseconds(data["date"].ConvertInvariant<long>())
                }
            };
        }

        private ExchangeOrderBook ParseOrderBook(JToken data)
        {
            ExchangeOrderBook book = new ExchangeOrderBook();
            foreach (JToken token in data["bids"])
            {
                book.Bids.Add(new ExchangeOrderPrice { Amount = token["quantity"].ConvertInvariant<decimal>(), Price = token["price"].ConvertInvariant<decimal>() });
            }
            foreach (JToken token in data["asks"])
            {
                book.Asks.Add(new ExchangeOrderPrice { Amount = token["quantity"].ConvertInvariant<decimal>(), Price = token["price"].ConvertInvariant<decimal>() });
            }
            return book;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            string symbol = "all";
            var data = await MakeRequestBithumbAsync(symbol, "/public/ticker/$SYMBOL$");
            foreach (JProperty token in data.Item1)
            {
                if (token.Name != "date")
                {
                    symbols.Add(token.Name);
                }
            }
            return symbols;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var data = await MakeRequestBithumbAsync(symbol, "/public/ticker/$SYMBOL$");
            return ParseTicker(data.Item2, data.Item1, null);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            string symbol = "all";
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            var data = await MakeRequestBithumbAsync(symbol, "/public/ticker/$SYMBOL$");
            DateTime date = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(data.Item1["date"].ConvertInvariant<long>());
            foreach (JProperty token in data.Item1)
            {
                if (token.Name != "date")
                {
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(token.Name, ParseTicker(token.Name, token.Value, date)));
                }
            }
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            var data = await MakeRequestBithumbAsync(symbol, "/public/orderbook/$SYMBOL$");
            return ParseOrderBook(data.Item1);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100)
        {
            string symbol = "all";
            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            var data = await MakeRequestBithumbAsync(symbol, "/public/orderbook/$SYMBOL$");
            foreach (JProperty book in data.Item1)
            {
                if (book.Name != "timestamp" && book.Name != "payment_currency")
                {
                    books.Add(new KeyValuePair<string, ExchangeOrderBook>(book.Name, ParseOrderBook(book.Value)));
                }
            }
            return books;
        }

        protected override async Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            var data = await MakeRequestBithumbAsync(symbol, "/public/recent_transactions/$SYMBOL$");
            foreach (JToken token in data.Item1)
            {
                trades.Add(new ExchangeTrade
                {
                    Amount = token["units_traded"].ConvertInvariant<decimal>(),
                    Price = token["price"].ConvertInvariant<decimal>(),
                    Id = -1,
                    IsBuy = token["type"].ToStringInvariant() == "bid",
                    Timestamp = token["transaction_date"].ConvertInvariant<DateTime>()
                });
            }
            callback(trades);
        }
    }
}
