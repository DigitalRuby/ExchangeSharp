/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    /// <summary>
    /// A summary of a specific market/asset
    /// </summary>
    public class MarketSummary
    {
        /// <summary>
        /// The name of the exchange for the market
        /// </summary>
        public string ExchangeName { get; set; }

        /// <summary>
        /// The name of the market
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The last price paid for the asset of the market
        /// </summary>
        public decimal LastPrice { get; set; }
        
        /// <summary>
        /// The highest price paid for the asset of the market, usually in the last 24hr
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// The lowest price paid for the asset of the market, usually in the last 24hr
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// The percent change in price, usually in the last 24hr
        /// </summary>
        public double PriceChangePercent { get; set; }

        /// <summary>
        /// The absolute change in price, usually in the last 24hr
        /// </summary>
        public decimal PriceChangeAmount { get; set; }

        /// <summary>
        /// The volume, usually in the last 24hr
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// The percent change in volume, usually in the last 24hr
        /// </summary>
        public double VolumeChangePercent { get; set; }

        /// <summary>
        /// The absolute change in volume, usually in the last 24hr
        /// </summary>
        public double VolumeChangeAmount { get; set; }
    }
}
