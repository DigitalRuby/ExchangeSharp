/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Base class for all exchange API
    /// </summary>
    public abstract class ExchangeAPI : BaseAPI, IExchangeAPI
    {
        private static readonly Dictionary<string, IExchangeAPI> apis = new Dictionary<string, IExchangeAPI>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Static constructor
        /// </summary>
        static ExchangeAPI()
        {
            foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI))))
            {
                ExchangeAPI api = Activator.CreateInstance(type) as ExchangeAPI;
                apis[api.Name] = api;
            }
        }

        /// <summary>
        /// Get an exchange API given an exchange name (see public constants at top of this file)
        /// </summary>
        /// <param name="exchangeName">Exchange name</param>
        /// <returns>Exchange API or null if not found</returns>
        public static IExchangeAPI GetExchangeAPI(string exchangeName)
        {
            GetExchangeAPIDictionary().TryGetValue(exchangeName, out IExchangeAPI api);
            return api;
        }

        /// <summary>
        /// Get a dictionary of exchange APIs for all exchanges
        /// </summary>
        /// <returns>Dictionary of string exchange name and value exchange api</returns>
        public static IReadOnlyDictionary<string, IExchangeAPI> GetExchangeAPIDictionary()
        {
            return apis;
        }

        /// <summary>
        /// Normalize a symbol for use on this exchange
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        public virtual string NormalizeSymbol(string symbol) { return symbol; }

        /// <summary>
        /// Normalize a symbol to a global standard symbol that is the same with all exchange symbols, i.e. btcusd
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns>Normalized global symbol</returns>
        public virtual string NormalizeSymbolGlobal(string symbol) { return symbol?.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant(); }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public virtual IReadOnlyCollection<string> GetSymbols() { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public Task<IReadOnlyCollection<string>> GetSymbolsAsync() => Task.Factory.StartNew(() => GetSymbols());

        /// <summary>
        /// Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public virtual ExchangeTicker GetTicker(string symbol) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public Task<ExchangeTicker> GetTickerAsync(string symbol) => Task.Factory.StartNew(() => GetTicker(symbol));

        /// <summary>
        /// Get all tickers, not all exchanges support this
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public virtual IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> GetTickers() { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get all tickers, not all exchanges support this
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public Task<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync() => Task.Factory.StartNew(() => GetTickers());

        /// <summary>
        /// Get exchange order book
        /// </summary>
        /// <param name="symbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public virtual ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get exchange order book
        /// </summary>
        /// <param name="symbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public Task<ExchangeOrderBook> GetOrderBookAsync(string symbol, int maxCount = 100) => Task.Factory.StartNew(() => GetOrderBook(symbol, maxCount));

        /// <summary>
        /// Get exchange order book all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public virtual IReadOnlyCollection<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get exchange order book all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public Task<IReadOnlyCollection<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100) => Task.Factory.StartNew(() => GetOrderBooks(maxCount));

        /// <summary>
        /// Get historical trades for the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="sinceDateTime">Optional date time to start getting the historical data at, null for the most recent data</param>
        /// <returns>An enumerator that iterates all historical data, this can take quite a while depending on how far back the sinceDateTime parameter goes</returns>
        public virtual IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get historical trades for the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="sinceDateTime">Optional date time to start getting the historical data at, null for the most recent data</param>
        /// <returns>An enumerator that iterates all historical data, this can take quite a while depending on how far back the sinceDateTime parameter goes</returns>
        public Task<IEnumerable<ExchangeTrade>> GetHistoricalTradesAsync(string symbol, DateTime? sinceDateTime = null) => Task.Factory.StartNew(() => GetHistoricalTrades(symbol, sinceDateTime));

        /// <summary>
        /// Get recent trades on the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all trades</returns>
        public virtual IEnumerable<ExchangeTrade> GetRecentTrades(string symbol) { return GetHistoricalTrades(symbol, null); }

        /// <summary>
        /// ASYNC - Get recent trades on the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all trades</returns>
        public Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string symbol) => Task.Factory.StartNew(() => GetRecentTrades(symbol));

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual Dictionary<string, decimal> GetAmountsAvailableToTrade() { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public Task<Dictionary<string, decimal>> GetAmountsAvailableToTradeAsync() => Task.Factory.StartNew<Dictionary<string, decimal>>(() => GetAmountsAvailableToTrade());

        /// <summary>
        /// Place a limit order
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="amount">Amount</param>
        /// <param name="price">Price</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Result</returns>
        public virtual ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Place a limit order
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="amount">Amount</param>
        /// <param name="price">Price</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Result</returns>
        public Task<ExchangeOrderResult> PlaceOrderAsync(string symbol, decimal amount, decimal price, bool buy) => Task.Factory.StartNew(() => PlaceOrder(symbol, amount, price, buy));

        /// <summary>
        /// Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <returns>Order details</returns>
        public virtual ExchangeOrderResult GetOrderDetails(string orderId) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <returns>Order details</returns>
        public Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId) => Task.Factory.StartNew(() => GetOrderDetails(orderId));

        /// <summary>
        /// Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public virtual IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public Task<IEnumerable<ExchangeOrderResult>> GetOpenOrderDetailsAsync(string symbol = null) => Task.Factory.StartNew(() => GetOpenOrderDetails());

        /// <summary>
        /// Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        public virtual void CancelOrder(string orderId) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        public Task CancelOrderAsync(string orderId) => Task.Factory.StartNew(() => CancelOrder(orderId));
    }

    /// <summary>
    /// List of exchange names
    /// </summary>
    public static class ExchangeName
    {
        /// <summary>
        /// Binance
        /// </summary>
        public const string Binance = "Binance";

        /// <summary>
        /// Bitfinex
        /// </summary>
        public const string Bitfinex = "Bitfinex";

        /// <summary>
        /// Bithumb
        /// </summary>
        public const string Bithumb = "Bithumb";

        /// <summary>
        /// Bittrex
        /// </summary>
        public const string Bittrex = "Bittrex";

        /// <summary>
        /// GDAX
        /// </summary>
        public const string GDAX = "GDAX";

        /// <summary>
        /// Gemini
        /// </summary>
        public const string Gemini = "Gemini";

        /// <summary>
        /// Kraken
        /// </summary>
        public const string Kraken = "Kraken";

        /// <summary>
        /// Poloniex
        /// </summary>
        public const string Poloniex = "Poloniex";
    }
}
