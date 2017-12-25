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
    /// Result of exchange order
    /// </summary>
    public enum ExchangeAPIOrderResult
    {
        /// <summary>
        /// Order status is unknown
        /// </summary>
        Unknown,

        /// <summary>
        /// Order has been filled completely
        /// </summary>
        Filled,

        /// <summary>
        /// Order partially filled
        /// </summary>
        FilledPartially,

        /// <summary>
        /// Order is pending or open but no amount has been filled yet
        /// </summary>
        Pending,

        /// <summary>
        /// Error
        /// </summary>
        Error,

        /// <summary>
        /// Order was cancelled
        /// </summary>
        Canceled
    }

    /// <summary>
    /// Result of an exchange order
    /// </summary>
    public class ExchangeOrderResult
    {
        /// <summary>
        /// Order id
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// Result of the order
        /// </summary>
        public ExchangeAPIOrderResult Result { get; set; }

        /// <summary>
        /// Message if any
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Original order amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Amount filled
        /// </summary>
        public decimal AmountFilled { get; set; }

        /// <summary>
        /// Average price
        /// </summary>
        public decimal AveragePrice { get; set; }

        /// <summary>
        /// Order date
        /// </summary>
        public DateTime OrderDate { get; set; }

        /// <summary>
        /// Symbol
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Whether the order is a buy or sell
        /// </summary>
        public bool IsBuy { get; set; }

        /// <summary>
        /// Append another order to this order - order id and type must match
        /// </summary>
        /// <param name="other">Order to append</param>
        public void AppendOrderWithOrder(ExchangeOrderResult other)
        {
            if (OrderId != null && Symbol != null && (OrderId != other.OrderId || IsBuy != other.IsBuy || Symbol != other.Symbol))
            {
                throw new InvalidOperationException("Appending orders requires order id, symbol and is buy to match");
            }

            decimal tradeSum = Amount + other.Amount;
            decimal baseAmount = Amount;
            Amount += other.Amount;
            AmountFilled += other.AmountFilled;
            AveragePrice = (AveragePrice * (baseAmount / tradeSum)) + (other.AveragePrice * (other.Amount / tradeSum));
            OrderId = other.OrderId;
            OrderDate = (OrderDate == default(DateTime)) ? other.OrderDate : OrderDate;
            Symbol = other.Symbol;
            IsBuy = other.IsBuy;
        }

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("[{0}], {1} {2} of {3} {4} filled at {5}", OrderDate, (IsBuy ? "Buy" : "Sell"), AmountFilled, Amount, Symbol, AveragePrice);
        }
    }
}
