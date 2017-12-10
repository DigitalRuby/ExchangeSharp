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
    /// <summary>
    /// Interface for communicating with an exchange over the Internet
    /// </summary>
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
        /// Pass phrase API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// Most exchanges do not require this, but GDAX is an example of one that does
        /// </summary>
        System.Security.SecureString Passphrase { get; set; }

        /// <summary>
        /// Load API keys from an encrypted file - keys will stay encrypted in memory
        /// </summary>
        /// <param name="encryptedFile">Encrypted file to load keys from</param>
        void LoadAPIKeys(string encryptedFile);

        /// <summary>
        /// Normalize a symbol for use on this exchange
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        string NormalizeSymbol(string symbol);

        /// <summary>
        /// Normalize a symbol to a global standard symbol that is the same with all exchange symbols, i.e. btcusd
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns>Normalized global symbol</returns>
        string NormalizeSymbolGlobal(string symbol);

        /// <summary>
        /// Request timeout
        /// </summary>
        TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Make a raw request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a double value, set to unix timestamp in seconds.
        /// The encoding of payload is exchange dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        string MakeRequest(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null);

        /// <summary>
        /// ASYNC - Make a raw request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a double value, set to unix timestamp in seconds.
        /// The encoding of payload is exchange dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        Task<string> MakeRequestAsync(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null);

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a double value, set to unix timestamp in seconds.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        T MakeJsonRequest<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null);

        /// <summary>
        /// ASYNC - Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a double value, set to unix timestamp in seconds.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        Task<T> MakeJsonRequestAsync<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null);

        /// <summary>
        /// Get symbols for the exchange
        /// </summary>
        /// <returns>Symbols</returns>
        IReadOnlyCollection<string> GetSymbols();

        /// <summary>
        /// ASYNC - Get symbols for the exchange
        /// </summary>
        /// <returns>Symbols</returns>
        Task<IReadOnlyCollection<string>> GetSymbolsAsync();

        /// <summary>
        /// Get latest ticker
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Latest ticker</returns>
        ExchangeTicker GetTicker(string symbol);

        /// <summary>
        /// ASYNC - Get latest ticker
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Latest ticker</returns>
        Task<ExchangeTicker> GetTickerAsync(string symbol);

        /// <summary>
        /// Get all tickers, not all exchanges support this
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> GetTickers();

        /// <summary>
        /// ASYNC - Get all tickers, not all exchanges support this
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        Task<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync();

        /// <summary>
        /// Get pending orders. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Orders</returns>
        ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100);

        /// <summary>
        /// ASYNC - Get pending orders. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Orders</returns>
        Task<ExchangeOrderBook> GetOrderBookAsync(string symbol, int maxCount = 100);

        /// <summary>
        /// Get exchange order book for all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        IReadOnlyCollection<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100);

        /// <summary>
        /// ASYNC - Get exchange order book for all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        Task<IReadOnlyCollection<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100);

        /// <summary>
        /// Get historical trades
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="sinceDateTime">Since date time</param>
        /// <returns>Trades</returns>
        IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null);

        /// <summary>
        /// ASYNC - Get historical trades
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="sinceDateTime">Since date time</param>
        /// <returns>Trades</returns>
        Task<IEnumerable<ExchangeTrade>> GetHistoricalTradesAsync(string symbol, DateTime? sinceDateTime = null);

        /// <summary>
        /// Get the latest trades
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Trades</returns>
        IEnumerable<ExchangeTrade> GetRecentTrades(string symbol);

        /// <summary>
        /// ASYNC - Get the latest trades
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Trades</returns>
        Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string symbol);

        /// <summary>
        /// Get amounts available to trade
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade</returns>
        Dictionary<string, decimal> GetAmountsAvailableToTrade();

        /// <summary>
        /// ASYNC - Get amounts available to trade
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade</returns>
        Task<Dictionary<string, decimal>> GetAmountsAvailableToTradeAsync();

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
        /// ASYNC - Place a limit order
        /// </summary>
        /// <param name="symbol">Symbol, i.e. btcusd</param>
        /// <param name="amount">Amount to buy or sell</param>
        /// <param name="price">Price to buy or sell at</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Order result and message string if any</returns>
        Task<ExchangeOrderResult> PlaceOrderAsync(string symbol, decimal amount, decimal price, bool buy);

        /// <summary>
        /// Get details of an order
        /// </summary>
        /// <param name="orderId">order id</param>
        /// <returns>Order details</returns>
        ExchangeOrderResult GetOrderDetails(string orderId);

        /// <summary>
        /// ASYNC - Get details of an order
        /// </summary>
        /// <param name="orderId">order id</param>
        /// <returns>Order details</returns>
        Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId);

        /// <summary>
        /// Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details for the specified symbol</returns>
        IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null);

        /// <summary>
        /// ASYNC - Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details for the specified symbol</returns>
        Task<IEnumerable<ExchangeOrderResult>> GetOpenOrderDetailsAsync(string symbol = null);

        /// <summary>
        /// Cancel an order, an exception is thrown if failure
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        void CancelOrder(string orderId);

        /// <summary>
        /// ASYNC - Cancel an order, an exception is thrown if failure
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        Task CancelOrderAsync(string orderId);
    }
}
