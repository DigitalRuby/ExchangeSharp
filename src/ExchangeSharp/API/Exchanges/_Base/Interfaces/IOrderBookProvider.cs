/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Provider of order books
    /// </summary>
    public interface IOrderBookProvider
    {
        /// <summary>
        /// Get pending orders. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="marketSymbol">Symbol</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Orders</returns>
        Task<ExchangeOrderBook> GetOrderBookAsync(string marketSymbol, int maxCount = 100);

        /// <summary>
        /// Get exchange order book for all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100);

        /// <summary>
        /// Get order book over web socket. This behaves differently depending on WebSocketOrderBookType.
        /// </summary>
        /// <param name="callback">Callback with the full ExchangeOrderBook</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <param name="marketSymbols">Market symbols or null/empty for all of them (if supported)</param>
        /// <returns>Web socket, call Dispose to close</returns>
        Task<IWebSocket> GetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols);

        /// <summary>
        /// What type of web socket order book is provided
        /// </summary>
        WebSocketOrderBookType WebSocketOrderBookType { get; }
    }

    /// <summary>
    /// Web socket order book type
    /// </summary>
    public enum WebSocketOrderBookType
    {
        /// <summary>
        /// Web socket order book not supported
        /// </summary>
        None,

        /// <summary>
        /// Web socket order book sends full book upon connect, and then delta books
        /// </summary>
        FullBookFirstThenDeltas,

        /// <summary>
        /// Web socket order book sends only delta books
        /// </summary>
        DeltasOnly,

        /// <summary>
        /// Web socket order book sends the full book always
        /// </summary>
        FullBookAlways
    }
}
