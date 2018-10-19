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
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Candlestick data
    /// </summary>
    public class MarketCandle
    {
        /// <summary>
        /// The name of the exchange for this candle
        /// </summary>
        public string ExchangeName { get; set; }

        /// <summary>
        /// The name of the market
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Timestamp, the open time of the candle
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The period in seconds
        /// </summary>
        public int PeriodSeconds { get; set; }

        /// <summary>
        /// Opening price
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// Close price
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// Base currency volume (i.e. in BTC-USD, this would be BTC volume)
        /// </summary>
        public double BaseCurrencyVolume { get; set; }

        /// <summary>
        /// Quote currency volume (i.e. in BTC-USD, this would be USD volume)
        /// </summary>
        public double QuoteCurrencyVolume { get; set; }

        /// <summary>
        /// The weighted average price if provided
        /// </summary>
        public decimal WeightedAverage { get; set; }

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("{0}/{1}: {2}, {3}, {4}, {5}, {6}, {7}, {8}", Timestamp, PeriodSeconds, OpenPrice, HighPrice, LowPrice, ClosePrice, BaseCurrencyVolume, QuoteCurrencyVolume, WeightedAverage);
        }
    }
}
