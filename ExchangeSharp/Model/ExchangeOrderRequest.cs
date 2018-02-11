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
    /// Order request details
    /// </summary>
    [System.Serializable]
    public class ExchangeOrderRequest
    {
        /// <summary>
        /// Symbol or pair for the order, i.e. btcusd
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Amount to buy or sell
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// The price to buy or sell at
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// True if this is a buy, false if a sell
        /// </summary>
        public bool IsBuy { get; set; }

        /// <summary>
        /// Whether the amount should be rounded - set to false if you know the exact amount, otherwise leave
        /// as true so that the exchange does not reject the order due to too many decimal places.
        /// </summary>
        public bool ShouldRoundAmount { get; set; } = true;

        /// <summary>
        /// The type of order - only limit is supported for now
        /// </summary>
        public OrderType OrderType { get; set; } = OrderType.Limit;

        /// <summary>
        /// Return a rounded amount if needed
        /// </summary>
        /// <returns>Rounded amount or amount if no rounding is needed</returns>
        public decimal RoundAmount()
        {
            return (ShouldRoundAmount ? CryptoUtility.RoundAmount(Amount) : Amount);
        }
    }
}