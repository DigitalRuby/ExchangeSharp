/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    /// <summary>Result of exchange order</summary>
    public enum ExchangeAPIOrderResult
    {
        /// <summary>Order status is unknown</summary>
        Unknown,

        /// <summary>Order has been filled completely</summary>
        Filled,

        /// <summary>Order partially filled</summary>
        FilledPartially,

        /// <summary>Order is pending or open but no amount has been filled yet</summary>
        Pending,

        /// <summary>Error</summary>
        Error,

        /// <summary>Order was cancelled</summary>
        Canceled,

        /// <summary>Order cancelled after partially filled</summary>
        FilledPartiallyAndCancelled,

        /// <summary>Order is pending cancel</summary>
        PendingCancel,
    }
}