using System;

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
    /// Contains information about a position on exchange
    /// </summary>
    public class ExchangePosition
	{
        /// <summary>
        /// Amount
        /// </summary>
        public decimal Amount { get; set; }

		/// <summary>Amount filled in the market currency.</summary>
		public decimal AmountFilled { get; set; }

		/// <summary>
		/// Average Price
		/// </summary>
		public decimal AveragePrice { get; set; }

		/// <summary>The fees on the order (not a percent).
		/// E.g. 0.0025 ETH</summary>
		public decimal Fees { get; set; }

		/// <summary>
		/// Liquidation Price
		/// </summary>
		public decimal LiquidationPrice { get; set; }

        /// <summary>
        /// Leverage
        /// </summary>
        public decimal Leverage { get; set; }

        /// <summary>
        /// Last Price
        /// Last Price on Exchange
        /// </summary>
        public decimal LastPrice { get; set; }

		/// <summary>
		/// Market Symbol
		/// </summary>
		public string MarketSymbol { get; set; }

		/// <summary>Message if any</summary>
		public string Message { get; set; }

		/// <summary>Order id</summary>
		public string OrderId { get; set; }

		/// <summary>
		/// The type of order
		/// </summary>
		public OrderType OrderType { get; set; }

		/// <summary>Result of the order</summary>
		public ExchangeAPIOrderResult Status { get; set; }

		/// <summary>
		/// TimeStamp
		/// </summary>
		public DateTime TimeStamp { get; set; }
    }
}
