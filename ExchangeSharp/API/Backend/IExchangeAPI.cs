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
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public interface IExchangeAPI
    {
        /// <summary>
        /// Get the name of the exchange this API connects to
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Optional public API key
        /// </summary>
        SecureString PublicApiKey { get; set; }

        /// <summary>
        /// Optional private API key
        /// </summary>
        SecureString PrivateApiKey { get; set; }

        /// <summary>
        /// Normalize a symbol for use on this exchange
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        string NormalizeSymbol(string symbol);

        /// <summary>
        /// Get symbols for the exchange
        /// </summary>
        /// <returns>Symbols</returns>
        IReadOnlyCollection<string> GetSymbols();

        /// <summary>
        /// Get latest ticker
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Latest ticker</returns>
        ExchangeTicker GetTicker(string symbol);

        /// <summary>
        /// Get all tickers
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> GetTickers();

        /// <summary>
        /// Get pending orders. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Orders</returns>
        ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100);

        /// <summary>
        /// Get all pending orders for all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        IReadOnlyCollection<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100);

        /// <summary>
        /// Get historical trades
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="sinceDateTime">Since date time</param>
        /// <returns>Trades</returns>
        IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null);

        /// <summary>
        /// Get the latest trades
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Trades</returns>
        IEnumerable<ExchangeTrade> GetRecentTrades(string symbol);

        /// <summary>
        /// Get amounts available to trade
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade</returns>
        Dictionary<string, decimal> GetAmountsAvailableToTrade();

        /// <summary>
        /// Place a limit order
        /// </summary>
        /// <param name="symbol">Symbol, i.e. btcusd</param>
        /// <param name="amount">Amount to buy or sell</param>
        /// <param name="price">Price to buy or sell at</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Order result and message string if any</returns>
        ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy);

        /// <summary>
        /// Get details of an order
        /// </summary>
        /// <param name="orderId">order id</param>
        /// <returns>Order details</returns>
        ExchangeOrderResult GetOrderDetails(string orderId);

        /// <summary>
        /// Cancel an order, an exception is thrown if failure
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        void CancelOrder(string orderId);
    }
}
