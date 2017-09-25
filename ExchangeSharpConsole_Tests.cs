/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using ExchangeSharp;

namespace ExchangeSharpConsole
{
    public partial class ExchangeSharpConsoleApp
    {
        private static void Assert(bool expression)
        {
            if (!expression)
            {
                throw new ApplicationException("Test failure, unexpected result");
            }
        }

        private static void PerformTests(Dictionary<string, string> dict)
        {
            // test all public API for each exchange
            IExchangeAPI[] apis = ExchangeAPI.GetExchangeAPIDictionary().Values.ToArray();
            foreach (IExchangeAPI api in apis)
            {
                string symbol = (api is ExchangeKrakenAPI ? "XXBTZUSD" : (api is ExchangeBittrexAPI ? "BTC-LTC" : "BTC-USD"));

                string[] symbols = api.GetSymbols();
                Assert(symbols != null && symbols.Length != 0);

                ExchangeTrade[] trades = api.GetHistoricalTrades(symbol).ToArray();
                Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);

                var book = api.GetOrderBook(symbol);
                Assert(book.Asks.Count != 0 && book.Bids.Count != 0 && book.Asks[0].Amount > 0m &&
                    book.Asks[0].Price > 0m && book.Bids[0].Amount > 0m && book.Bids[0].Price > 0m);

                trades = api.GetRecentTrades(symbol).ToArray();
                Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);

                var ticker = api.GetTicker(symbol);
                Assert(ticker != null && ticker.Ask > 0m && ticker.Bid > 0m && ticker.Last > 0m &&
                    ticker.Volume != null && ticker.Volume.PriceAmount > 0m && ticker.Volume.QuantityAmount > 0m);
            }           
        }
    }
}
