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
using System.Threading;
using ExchangeSharp;

namespace ExchangeSharpConsoleApp
{
	public static partial class ExchangeSharpConsole
    {
        public static void RunShowExchangeStats(Dictionary<string, string> dict)
        {
            string symbol = "BTC-USD";
            string symbol2 = "XXBTZUSD";
            IExchangeAPI apiGDAX = new ExchangeGdaxAPI();
            IExchangeAPI apiGemini = new ExchangeGeminiAPI();
            IExchangeAPI apiKraken = new ExchangeKrakenAPI();
            IExchangeAPI apiBitfinex = new ExchangeBitfinexAPI();

            while (true)
            {
                ExchangeTicker ticker = apiGDAX.GetTicker(symbol);
                ExchangeOrderBook orders = apiGDAX.GetOrderBook(symbol);
                decimal askAmountSum = orders.Asks.Sum(o => o.Amount);
                decimal askPriceSum = orders.Asks.Sum(o => o.Price);
                decimal bidAmountSum = orders.Bids.Sum(o => o.Amount);
                decimal bidPriceSum = orders.Bids.Sum(o => o.Price);

                ExchangeTicker ticker2 = apiGemini.GetTicker(symbol);
                ExchangeOrderBook orders2 = apiGemini.GetOrderBook(symbol);
                decimal askAmountSum2 = orders2.Asks.Sum(o => o.Amount);
                decimal askPriceSum2 = orders2.Asks.Sum(o => o.Price);
                decimal bidAmountSum2 = orders2.Bids.Sum(o => o.Amount);
                decimal bidPriceSum2 = orders2.Bids.Sum(o => o.Price);

                ExchangeTicker ticker3 = apiKraken.GetTicker(symbol2);
                ExchangeOrderBook orders3 = apiKraken.GetOrderBook(symbol2);
                decimal askAmountSum3 = orders3.Asks.Sum(o => o.Amount);
                decimal askPriceSum3 = orders3.Asks.Sum(o => o.Price);
                decimal bidAmountSum3 = orders3.Bids.Sum(o => o.Amount);
                decimal bidPriceSum3 = orders3.Bids.Sum(o => o.Price);

                ExchangeTicker ticker4 = apiBitfinex.GetTicker(symbol);
                ExchangeOrderBook orders4 = apiBitfinex.GetOrderBook(symbol);
                decimal askAmountSum4 = orders4.Asks.Sum(o => o.Amount);
                decimal askPriceSum4 = orders4.Asks.Sum(o => o.Price);
                decimal bidAmountSum4 = orders4.Bids.Sum(o => o.Amount);
                decimal bidPriceSum4 = orders4.Bids.Sum(o => o.Price);

                Console.Clear();
                Console.WriteLine("GDAX: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker.Last, ticker.Volume.PriceAmount, askAmountSum, askPriceSum, bidAmountSum, bidPriceSum);
                Console.WriteLine("GEMI: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker2.Last, ticker2.Volume.PriceAmount, askAmountSum2, askPriceSum2, bidAmountSum2, bidPriceSum2);
                Console.WriteLine("KRAK: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker3.Last, ticker3.Volume.PriceAmount, askAmountSum3, askPriceSum3, bidAmountSum3, bidPriceSum3);
                Console.WriteLine("BITF: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker4.Last, ticker4.Volume.PriceAmount, askAmountSum4, askPriceSum4, bidAmountSum4, bidPriceSum4);
                Thread.Sleep(5000);
            }
        }
    }
}
