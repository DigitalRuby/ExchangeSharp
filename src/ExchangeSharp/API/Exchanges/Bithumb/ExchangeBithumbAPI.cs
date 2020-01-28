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
            MarketSymbolIsUppercase = true;
        }

        public override string NormalizeMarketSymbol(string marketSymbol)
        {
            marketSymbol = base.NormalizeMarketSymbol(marketSymbol);
            int pos = marketSymbol.IndexOf(MarketSymbolSeparator);
            if (pos >= 0)
            {
                marketSymbol = marketSymbol.Substring(0, pos);
            }
            return marketSymbol;
        }

        public override Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
        {
            return Task.FromResult(marketSymbol + GlobalMarketSymbolSeparator + "KRW"); //e.g. 1 btc worth 9.7m KRW
        }

        public override Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
        {
	        var values = marketSymbol.Split(GlobalMarketSymbolSeparator); //for Bitthumb, e.g. "BTC-KRW", 1 btc worth about 9.7m won. Market symbol is BTC.

	        return Task.FromResult(values[0]);
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

        private async Task<Tuple<JToken, string>> MakeRequestBithumbAsync(string marketSymbol, string subUrl)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", marketSymbol ?? string.Empty));
            return new Tuple<JToken, string>(obj, marketSymbol);
        }

        private async Task<ExchangeTicker> ParseTickerAsync(string marketSymbol, JToken data)
        {
            /*
            {
                "opening_price": "12625000",
                "closing_price": "12636000",
                "min_price": "12550000",
                "max_price": "12700000",
                "units_traded": "866.21",
                "acc_trade_value": "10930847017.53",
                "prev_closing_price": "12625000",
                "units_traded_24H": "16767.54",
                "acc_trade_value_24H": "211682650507.99",
                "fluctate_24H": "3,000",
                "fluctate_rate_24H": "0.02"
            }
            */
            ExchangeTicker ticker = await this.ParseTickerAsync(data, marketSymbol, "max_price", "min_price", "min_price", "min_price", "units_traded_24H");
            ticker.Volume.Timestamp = data.Parent.Parent["date"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds();
            return ticker;
        }

        protected override (string baseCurrency, string quoteCurrency) OnSplitMarketSymbolToCurrencies(string marketSymbol)
        {
            return (marketSymbol, "KRW");
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> marketSymbols = new List<string>();
            string marketSymbol = "all";
            var data = await MakeRequestBithumbAsync(marketSymbol, "/public/ticker/$SYMBOL$");
            foreach (JProperty token in data.Item1)
            {
                if (token.Name != "date")
                {
                    marketSymbols.Add(token.Name);
                }
            }
            return marketSymbols;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            var data = await MakeRequestBithumbAsync(marketSymbol, "/public/ticker/$SYMBOL$");
            return await ParseTickerAsync(data.Item2, data.Item1);
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
                    ExchangeTicker ticker = await ParseTickerAsync(token.Name, token.Value);
                    ticker.Volume.Timestamp = date;
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(token.Name, ticker));
                }
            }
            return tickers;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            var data = await MakeRequestBithumbAsync(marketSymbol, "/public/orderbook/$SYMBOL$");
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
