/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ExchangeSharp;

namespace ExchangeSharpConsole
{
	public static partial class ExchangeSharpConsoleMain
    {
        public static async Task RunShowExchangeStats(int interval = 5000)
        {
            string marketSymbol = "BTC-USD";
            string marketSymbol2 = "XXBTZUSD";
            IExchangeAPI apiCoinbase = new ExchangeCoinbaseAPI();
            IExchangeAPI apiGemini = new ExchangeGeminiAPI();
            IExchangeAPI apiKraken = new ExchangeKrakenAPI();
            IExchangeAPI apiBitfinex = new ExchangeBitfinexAPI();

            while (true)
            {
                ExchangeTicker ticker = await apiCoinbase.GetTickerAsync(marketSymbol);
                ExchangeOrderBook orders = await apiCoinbase.GetOrderBookAsync(marketSymbol);
                decimal askAmountSum = orders.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum = orders.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum = orders.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum = orders.Bids.Values.Sum(o => o.Price);

                ExchangeTicker ticker2 = await apiGemini.GetTickerAsync(marketSymbol);
                ExchangeOrderBook orders2 = await apiGemini.GetOrderBookAsync(marketSymbol);
                decimal askAmountSum2 = orders2.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum2 = orders2.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum2 = orders2.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum2 = orders2.Bids.Values.Sum(o => o.Price);

                ExchangeTicker ticker3 = await apiKraken.GetTickerAsync(marketSymbol2);
                ExchangeOrderBook orders3 = await apiKraken.GetOrderBookAsync(marketSymbol2);
                decimal askAmountSum3 = orders3.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum3 = orders3.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum3 = orders3.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum3 = orders3.Bids.Values.Sum(o => o.Price);

                ExchangeTicker ticker4 = await apiBitfinex.GetTickerAsync(marketSymbol);
                ExchangeOrderBook orders4 = await apiBitfinex.GetOrderBookAsync(marketSymbol);
                decimal askAmountSum4 = orders4.Asks.Values.Sum(o => o.Amount);
                decimal askPriceSum4 = orders4.Asks.Values.Sum(o => o.Price);
                decimal bidAmountSum4 = orders4.Bids.Values.Sum(o => o.Amount);
                decimal bidPriceSum4 = orders4.Bids.Values.Sum(o => o.Price);

                Console.Clear();
                Console.WriteLine("GDAX: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker.Last, ticker.Volume.QuoteCurrencyVolume, askAmountSum, askPriceSum, bidAmountSum, bidPriceSum);
                Console.WriteLine("GEMI: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker2.Last, ticker2.Volume.QuoteCurrencyVolume, askAmountSum2, askPriceSum2, bidAmountSum2, bidPriceSum2);
                Console.WriteLine("KRAK: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker3.Last, ticker3.Volume.QuoteCurrencyVolume, askAmountSum3, askPriceSum3, bidAmountSum3, bidPriceSum3);
                Console.WriteLine("BITF: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker4.Last, ticker4.Volume.QuoteCurrencyVolume, askAmountSum4, askPriceSum4, bidAmountSum4, bidPriceSum4);
                Thread.Sleep(interval);
            }
        }
    }
}
