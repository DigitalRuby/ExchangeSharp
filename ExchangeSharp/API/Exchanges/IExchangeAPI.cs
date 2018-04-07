﻿/*
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
        ///  Load API keys from unsecure strings
        /// <param name="publicApiKey">Public Api Key</param>
        /// <param name="privateApiKey">Private Api Key</param>
        /// <param name="passPhrase">Pass phrase, null for none</param>
        /// </summary>
        void LoadAPIKeysUnsecure(string publicApiKey, string privateApiKey, string passPhrase = null);

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
        /// Request window - most services do not use this, but Binance API is an example of one that does
        /// </summary>
        TimeSpan RequestWindow { get; set; }

        /// <summary>
        /// Nonce style
        /// </summary>
        NonceStyle NonceStyle { get; }

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        System.Net.Cache.RequestCachePolicy CachePolicy { get; set; }

        /// <summary>
        /// Generate a nonce
        /// </summary>
        /// <returns>Nonce</returns>
        object GenerateNonce();

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        T MakeJsonRequest<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null);

        /// <summary>
        /// ASYNC - Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        Task<T> MakeJsonRequestAsync<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null);

        /// <summary>
        /// Gets currencies and related data such as IsEnabled and TxFee (if available)
        /// </summary>
        /// <returns>Collection of Currencies</returns>
        IReadOnlyDictionary<string, ExchangeCurrency> GetCurrencies();

        /// <summary>
        /// ASYNC - Gets currencies and related data such as IsEnabled and TxFee (if available)
        /// </summary>
        /// <returns>Collection of Currencies</returns>
        Task<IReadOnlyDictionary<string, ExchangeCurrency>> GetCurrenciesAsync();

        /// <summary>
        /// Gets the address to deposit to and applicable details.
        /// </summary>
        /// <param name="symbol">Symbol to get address for.</param>
        /// <param name="forceRegenerate">True to regenerate the address</param>
        /// <returns>Deposit address details (including tag if applicable, such as XRP)</returns>
        ExchangeDepositDetails GetDepositAddress(string symbol, bool forceRegenerate = false);

        /// <summary>Gets the deposit history for a symbol</summary>
        /// <param name="symbol">The symbol to check. May be null.</param>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        IEnumerable<ExchangeTransaction> GetDepositHistory(string symbol);

        /// <summary>
        /// Get symbols for the exchange
        /// </summary>
        /// <returns>Symbols</returns>
        IEnumerable<string> GetSymbols();

        /// <summary>
        /// ASYNC - Get symbols for the exchange
        /// </summary>
        /// <returns>Symbols</returns>
        Task<IEnumerable<string>> GetSymbolsAsync();

        /// <summary>
        /// Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        IEnumerable<ExchangeMarket> GetSymbolsMetadata();

        /// <summary>
        /// ASYNC - Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        Task<IEnumerable<ExchangeMarket>> GetSymbolsMetadataAsync();

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
        IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers();

        /// <summary>
        /// ASYNC - Get all tickers, not all exchanges support this
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync();

        /// <summary>
        /// Get all tickers via web socket
        /// </summary>
        /// <param name="tickers">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        IDisposable GetTickersWebSocket(System.Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers);

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
        IEnumerable<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100);

        /// <summary>
        /// ASYNC - Get exchange order book for all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100);

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
        /// Get candles (open, high, low, close)
        /// </summary>
        /// <param name="symbol">Symbol to get candles for</param>
        /// <param name="periodSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <param name="limit">Max results, can be used instead of startDate and endDate if desired</param>
        /// <returns>Candles</returns>
        IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null);

        /// <summary>
        /// ASYNC - Get candles (open, high, low, close)
        /// </summary>
        /// <param name="symbol">Symbol to get candles for</param>
        /// <param name="periodSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <param name="limit">Max results, can be used instead of startDate and endDate if desired</param>
        /// <returns>Candles</returns>
        Task<IEnumerable<MarketCandle>> GetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null);

        /// <summary>
        /// Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        Dictionary<string, decimal> GetAmounts();

        /// <summary>
        /// ASYNC - Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        Task<Dictionary<string, decimal>> GetAmountsAsync();

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade</returns>
        Dictionary<string, decimal> GetAmountsAvailableToTrade();

        /// <summary>
        /// ASYNC - Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade</returns>
        Task<Dictionary<string, decimal>> GetAmountsAvailableToTradeAsync();

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="order">Order request</param>
        /// <returns>Order result and message string if any</returns>
        ExchangeOrderResult PlaceOrder(ExchangeOrderRequest order);

        /// <summary>
        /// ASYNC - Place an order
        /// </summary>
        /// <param name="order">Order request</param>
        /// <returns>Order result and message string if any</returns>
        Task<ExchangeOrderResult> PlaceOrderAsync(ExchangeOrderRequest order);

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
        /// Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <param name="afterDate">Only returns orders on or after the specified date/time</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null);

        /// <summary>
        /// Get the details of all completed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        IDisposable GetCompletedOrderDetailsWebSocket(System.Action<ExchangeOrderResult> callback);

        /// <summary>
        /// ASYNC - Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        Task<IEnumerable<ExchangeOrderResult>> GetCompletedOrderDetailsAsync(string symbol = null);

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
        Market
    }
}
