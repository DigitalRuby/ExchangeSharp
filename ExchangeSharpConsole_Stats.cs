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
        private static void RunShowExchangeStats(Dictionary<string, string> dict)
        {
            string symbol = "BTC-USD";
            string symbol2 = "XXBTZUSD";
            IExchangeAPI apiGDAX = new ExchangeGdaxAPI();
            IExchangeAPI apiGemini = new ExchangeGeminiAPI();
            IExchangeAPI apiKraken = new ExchangeKrakenAPI();
            while (true)
            {
                ExchangeTicker ticker = apiGDAX.GetTicker(symbol);
                ExchangeOrderBook orders = apiGDAX.GetOrderBook(symbol);
                double askAmountSum = orders.Asks.Sum(o => o.Amount);
                double askPriceSum = orders.Asks.Sum(o => o.Price);
                double bidAmountSum = orders.Bids.Sum(o => o.Amount);
                double bidPriceSum = orders.Bids.Sum(o => o.Price);

                ExchangeTicker ticker2 = apiGemini.GetTicker(symbol);
                ExchangeOrderBook orders2 = apiGemini.GetOrderBook(symbol);
                double askAmountSum2 = orders2.Asks.Sum(o => o.Amount);
                double askPriceSum2 = orders2.Asks.Sum(o => o.Price);
                double bidAmountSum2 = orders2.Bids.Sum(o => o.Amount);
                double bidPriceSum2 = orders2.Bids.Sum(o => o.Price);

                ExchangeTicker ticker3 = apiKraken.GetTicker(symbol2);
                ExchangeOrderBook orders3 = apiKraken.GetOrderBook(symbol2);
                double askAmountSum3 = orders3.Asks.Sum(o => o.Amount);
                double askPriceSum3 = orders3.Asks.Sum(o => o.Price);
                double bidAmountSum3 = orders3.Bids.Sum(o => o.Amount);
                double bidPriceSum3 = orders3.Bids.Sum(o => o.Price);

                Console.Clear();
                Console.WriteLine("GDAX: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker.Last, ticker.Volume.PriceAmount, askAmountSum, askPriceSum, bidAmountSum, bidPriceSum);
                Console.WriteLine("GEMI: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker2.Last, ticker2.Volume.PriceAmount, askAmountSum2, askPriceSum2, bidAmountSum2, bidPriceSum2);
                Console.WriteLine("KRAK: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker3.Last, ticker3.Volume.PriceAmount, askAmountSum3, askPriceSum3, bidAmountSum3, bidPriceSum3);
                Thread.Sleep(5000);
            }
        }
    }
}
