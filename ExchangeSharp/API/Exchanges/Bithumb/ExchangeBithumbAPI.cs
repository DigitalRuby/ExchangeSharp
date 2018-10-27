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
    public sealed partial class ExchangeBithumbAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.bithumb.com";

        public ExchangeBithumbAPI()
        {
            SymbolIsUppercase = true;
        }

        public override string NormalizeSymbol(string symbol)
        {
            symbol = base.NormalizeSymbol(symbol);
            int pos = symbol.IndexOf(SymbolSeparator);
            if (pos >= 0)
            {
                symbol = symbol.Substring(0, pos);
            }
            return symbol;
        }

        public override string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            return "KRW" + GlobalSymbolSeparator + symbol;
        }

        public override string GlobalSymbolToExchangeSymbol(string symbol)
        {
            return symbol.Substring(symbol.IndexOf(GlobalSymbolSeparator) + 1);
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

        protected override JToken CheckJsonResponse(JToken result)
        {
            if (result != null && !(result is JArray) && result["status"] != null && result["status"].ToStringInvariant() != "0000")
            {
                throw new APIException(result["status"].ToStringInvariant() + ": " + result["message"].ToStringInvariant());
            }
            return result["data"];
        }

        private async Task<Tuple<JToken, string>> MakeRequestBithumbAsync(string symbol, string subUrl)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty));
            return new Tuple<JToken, string>(obj, symbol);
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data)
        {
            return this.ParseTicker(data, symbol, "sell_price", "buy_price", "buy_price", "average_price", "units_traded", "date", TimestampType.UnixMilliseconds);
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
            return ParseTicker(data.Item2, data.Item1);
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
                    ExchangeTicker ticker = ParseTicker(token.Name, token.Value);
                    ticker.Volume.Timestamp = date;
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(token.Name, ticker));
                }
            }
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            var data = await MakeRequestBithumbAsync(symbol, "/public/orderbook/$SYMBOL$");
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(data.Item1, amount: "quantity", sequence: "timestamp", maxCount: maxCount);
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
                    ExchangeOrderBook orderBook = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(book.Value);
                    books.Add(new KeyValuePair<string, ExchangeOrderBook>(book.Name, orderBook));
                }
            }
            return books;
        }
    }

    public partial class ExchangeName { public const string Bithumb = "Bithumb"; }
}
