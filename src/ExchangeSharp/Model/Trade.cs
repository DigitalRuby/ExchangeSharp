/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Runtime.InteropServices;

namespace ExchangeSharp
{
    /// <summary>
    /// A tight, lightweight trade object useful for iterating quickly in memory for trader testing
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct Trade
    {
        /// <summary>
        /// Unix timestamp in milliseconds, or 0 if no new trade available
        /// </summary>
        public long Ticks;

        /// <summary>
        /// Current purchase price
        /// </summary>
        public float Price;

        /// <summary>
        /// Amount purchased
        /// </summary>
        public float Amount;

        /// <summary>
        /// Get a string for this trade
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return CryptoUtility.UnixTimeStampToDateTimeMilliseconds(Ticks).ToLocalTime() + ": " + Amount + " at " + Price;
        }
    }
}
