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

namespace ExchangeSharpConsole
{
	public static partial class ExchangeSharpConsoleMain
    {
        public static void RunShowExchangeStats(Dictionary<string, string> dict)
        {
            string symbol = "BTC-USD";
            string symbol2 = "XXBTZUSD";
            IExchangeAPI apiCoinbase = new ExchangeCoinbaseAPI();
            IExchangeAPI apiGemini = new ExchangeGeminiAPI();
            IExchangeAPI apiKraken = new ExchangeKrakenAPI();
            IExchangeAPI apiBitfinex = new ExchangeBitfinexAPI();

            while (true)
            {
                ExchangeTicker ticker = apiCoinbase.GetTickerAsync(symbol).Sync();
                ExchangeOrderBook orders = apiCoinbase.GetOrderBookAsync(symbol).Sync();
                decimal askAmountSum = orders.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum = orders.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum = orders.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum = orders.Bids.Values.Sum(o => o.Price);

                ExchangeTicker ticker2 = apiGemini.GetTickerAsync(symbol).Sync();
                ExchangeOrderBook orders2 = apiGemini.GetOrderBookAsync(symbol).Sync();
                decimal askAmountSum2 = orders2.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum2 = orders2.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum2 = orders2.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum2 = orders2.Bids.Values.Sum(o => o.Price);

                ExchangeTicker ticker3 = apiKraken.GetTickerAsync(symbol2).Sync();
                ExchangeOrderBook orders3 = apiKraken.GetOrderBookAsync(symbol2).Sync();
                decimal askAmountSum3 = orders3.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum3 = orders3.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum3 = orders3.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum3 = orders3.Bids.Values.Sum(o => o.Price);

                ExchangeTicker ticker4 = apiBitfinex.GetTickerAsync(symbol).Sync();
                ExchangeOrderBook orders4 = apiBitfinex.GetOrderBookAsync(symbol).Sync();
                decimal askAmountSum4 = orders4.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum4 = orders4.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum4 = orders4.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum4 = orders4.Bids.Values.Sum(o => o.Price);

                Console.Clear();
                Console.WriteLine("GDAX: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker.Last, ticker.Volume.BaseVolume, askAmountSum, askPriceSum, bidAmountSum, bidPriceSum);
                Console.WriteLine("GEMI: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker2.Last, ticker2.Volume.BaseVolume, askAmountSum2, askPriceSum2, bidAmountSum2, bidPriceSum2);
                Console.WriteLine("KRAK: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker3.Last, ticker3.Volume.BaseVolume, askAmountSum3, askPriceSum3, bidAmountSum3, bidPriceSum3);
                Console.WriteLine("BITF: {0:0.00}, {1:0.00}, {2:0.00}, {3:0.00}, {4:0.00}, {5:0.00}", ticker4.Last, ticker4.Volume.BaseVolume, askAmountSum4, askPriceSum4, bidAmountSum4, bidPriceSum4);
                Thread.Sleep(5000);
            }
        }
    }
}
