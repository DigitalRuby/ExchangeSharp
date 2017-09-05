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

using Newtonsoft.Json;

namespace ExchangeSharp
{
    public class ExchangeTicker
    {
        /// <summary>
        /// The bid is the price to sell at
        /// </summary>
        public double Bid { get; set; }

        /// <summary>
        /// The ask is the price to buy at
        /// </summary>
        public double Ask { get; set; }

        /// <summary>
        /// The last trade purchase price
        /// </summary>
        public double Last { get; set; }

        /// <summary>
        /// Volume info
        /// </summary>
        public ExchangeVolume Volume { get; set; }

        /// <summary>
        /// Get a string for this ticker
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("Bid: {0}, Ask: {1}, Last: {2}", Bid, Ask, Last);
        }
    }

    /// <summary>
    /// Info about exchange volume
    /// </summary>
    public class ExchangeVolume
    {
        /// <summary>
        /// Last volume update timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Price symbol - will equal quantity symbol if exchange doesn't break it out by price unit and quantity unit
        /// </summary>
        public string PriceSymbol { get; set; }

        /// <summary>
        /// Price
        /// </summary>
        public double PriceAmount { get; set; }

        /// <summary>
        /// Quantity symbol (converted into this unit)
        /// </summary>
        public string QuantitySymbol { get; set; }

        /// <summary>
        /// Quantity amount (this many units total)
        /// </summary>
        public double QuantityAmount { get; set; }
    }
}
