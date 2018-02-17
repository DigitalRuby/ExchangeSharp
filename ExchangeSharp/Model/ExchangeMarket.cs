﻿/*
MIT LICENSE

Copyright 2018 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    public class ExchangeMarket
    {
        /// <summary>Gets or sets the name of the market.</summary>
        public string MarketName { get; set; }

        /// <summary>A value indicating whether the market is active.</summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// The minimum size of the trade in the unit of "MarketCurrency".
        /// For example, in DOGE/BTC the MinTradeSize is currently 423.72881356 DOGE
        /// </summary>
        public decimal MinTradeSize { get; set; }

        /// <summary>In a pair like ZRX/BTC, BTC is the base currency.</summary>
        public string BaseCurrency { get; set; }

        /// <summary>In a pair like ZRX/BTC, ZRX is the market currency.</summary>
        public string MarketCurrency { get; set; }
    }
}