/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;

namespace ExchangeSharp
{
    /// <summary>
    /// Order request details
    /// </summary>
    [System.Serializable]
    public class ExchangeOrderRequest
    {
        /// <summary>
        /// Market symbol or pair for the order, i.e. btcusd
        /// </summary>
        public string MarketSymbol { get; set; }

        /// <summary>
        /// Amount to buy or sell
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// The price to buy or sell at
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// The price to trigger a stop
        /// </summary>
        public decimal StopPrice { get; set; }
    
        /// <summary>
        /// True if this is a buy, false if a sell
        /// </summary>
        public bool IsBuy { get; set; }

        /// <summary>
        /// Whether the order is a margin order. Not all exchanges support margin orders, so this parameter may be ignored.
        /// You should verify that your exchange supports margin orders before passing this field as true and expecting
        /// it to be a margin order. The best way to determine this in code is to call one of the margin account balance
        /// methods and see if it fails.
        /// </summary>
        public bool IsMargin { get; set; }

        /// <summary>Order id</summary>
        public string OrderId { get; set; }

        /// <summary>
        /// Client Order id
        /// Order IDs put here will be returned in the Order Result returned by the exchange
        /// Not all exchanges support this
        /// </summary>
        public string ClientOrderId { get; set; }

        /// <summary>
        /// Whether the amount should be rounded - set to false if you know the exact amount, otherwise leave
        /// as true so that the exchange does not reject the order due to too many decimal places.
        /// </summary>
        public bool ShouldRoundAmount { get; set; } = true;

        /// <summary>
        /// The type of order
        /// </summary>
        public OrderType OrderType { get; set; } = OrderType.Limit;

        /// <summary>
        /// Additional order parameters specific to the exchange that don't fit in common order properties. These will be forwarded on to the exchange as key=value pairs.
        /// Not all exchanges will use this dictionary.
        /// These are added after all other parameters and will replace existing properties, such as order type.
        /// </summary>
        public Dictionary<string, object> ExtraParameters { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Return a rounded amount if needed
        /// </summary>
        /// <returns>Rounded amount or amount if no rounding is needed</returns>
        public decimal RoundAmount()
        {
            return ShouldRoundAmount ? CryptoUtility.RoundAmount(Amount) : Amount;
        }
    }

    /// <summary>
    /// The type of order - default is limit. Please use market orders with caution. Not all exchanges support market orders.
    /// Types of orders
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// A limit order, the order will not buy or sell beyond the price you specify
        /// </summary>
        Limit,

        /// <summary>
        /// A market order, you will buy or sell the full amount - use with caution as this will give you a terrible deal if the order book is thin
        /// </summary>
        Market,

        /// <summary>
        /// A stop order, you will sell if price reaches a low enough level down to a limit
        /// </summary>
        Stop
  }
}