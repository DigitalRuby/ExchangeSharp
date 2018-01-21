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
            if (api == null)
            {
                throw new ArgumentException("No API available with name " + exchangeName);
            }
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
        /// Normalize a symbol to a global standard symbol that is the same with all exchange symbols, i.e. btc-usd. This base method standardizes with a hyphen separator.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns>Normalized global symbol</returns>
        public virtual string NormalizeSymbolGlobal(string symbol)
        {
            return symbol?.Replace("_", "-").Replace("/", "-").ToLowerInvariant();
        }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public virtual IEnumerable<string> GetSymbols() { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public Task<IEnumerable<string>> GetSymbolsAsync() => Task.Factory.StartNew(() => GetSymbols());

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
        /// Get all tickers. If the exchange does not support this, a ticker will be requested for each symbol.
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public virtual IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers()
        {
            foreach (string symbol in GetSymbols())
            {
                yield return new KeyValuePair<string, ExchangeTicker>(symbol, GetTicker(symbol));
            }
        }

        /// <summary>
        /// ASYNC - Get all tickers. If the exchange does not support this, a ticker will be requested for each symbol.
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync() => Task.Factory.StartNew(() => GetTickers());

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
        /// Get exchange order book all symbols. If the exchange does not support this, an order book will be requested for each symbol. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public virtual IEnumerable<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100)
        {
            foreach (string symbol in GetSymbols())
            {
                yield return new KeyValuePair<string, ExchangeOrderBook>(symbol, GetOrderBook(symbol, maxCount));
            }
        }

        /// <summary>
        /// ASYNC - Get exchange order book all symbols. If the exchange does not support this, an order book will be requested for each symbol. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100) => Task.Factory.StartNew(() => GetOrderBooks(maxCount));

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
        /// Get recent trades on the exchange - this implementation simply calls GetHistoricalTrades with a null sinceDateTime.
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all trades</returns>
        public virtual IEnumerable<ExchangeTrade> GetRecentTrades(string symbol) { return GetHistoricalTrades(symbol, null); }

        /// <summary>
        /// ASYNC - Get recent trades on the exchange - this implementation simply calls GetHistoricalTradesAsync with a null sinceDateTime.
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all trades</returns>
        public Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string symbol) => Task.Factory.StartNew(() => GetRecentTrades(symbol));

        /// <summary>
        /// Get candles (open, high, low, close)
        /// </summary>
        /// <param name="symbol">Symbol to get candles for</param>
        /// <param name="periodsSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <returns>Candles</returns>
        public virtual IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null) { throw new NotSupportedException(); }

        /// <summary>
        /// ASYNC - Get candles (open, high, low, close)
        /// </summary>
        /// <param name="symbol">Symbol to get candles for</param>
        /// <param name="periodsSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <returns>Candles</returns>
        public Task<IEnumerable<MarketCandle>> GetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null) => Task.Factory.StartNew(() => GetCandles(symbol, periodSeconds, startDate, endDate));

        /// <summary>
        /// Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        public virtual Dictionary<string, decimal> GetAmounts() { throw new NotSupportedException(); }

        /// <summary>
        /// ASYNC - Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        public Task<Dictionary<string, decimal>> GetAmountsAsync() => Task.Factory.StartNew(() => GetAmounts());

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual Dictionary<string, decimal> GetAmountsAvailableToTrade() { return GetAmounts(); }

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
        /// Place a market order
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="amount">Amount</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Result</returns>
        public virtual ExchangeOrderResult PlaceMarketOrder(string symbol, decimal amount, bool buy) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Place a market order
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="amount">Amount</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Result</returns>
        public Task<ExchangeOrderResult> PlaceMarketOrderAsync(string symbol, decimal amount, bool buy) => Task.Factory.StartNew(() => PlaceMarketOrder(symbol, amount, buy));

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
        public Task<IEnumerable<ExchangeOrderResult>> GetOpenOrderDetailsAsync(string symbol = null) => Task.Factory.StartNew(() => GetOpenOrderDetails(symbol));

        /// <summary>
        /// Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <param name="afterDate">Only returns orders on or after the specified date/time</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        public virtual IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null) { throw new NotImplementedException(); }

        /// <summary>
        /// ASYNC - Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        public Task<IEnumerable<ExchangeOrderResult>> GetCompletedOrderDetailsAsync(string symbol = null) => Task.Factory.StartNew(() => GetCompletedOrderDetails(symbol));

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
        /// Bitstamp
        /// </summary>
        public const string Bitstamp = "Bitstamp";

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
