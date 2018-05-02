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
using System.Reflection;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Base class for all exchange API
    /// </summary>
    public abstract class ExchangeAPI : BaseAPI, IExchangeAPI
    {
        /// <summary>
        /// Separator for global symbols
        /// </summary>
        public const char GlobalSymbolSeparator = '-';

        private static readonly Dictionary<string, IExchangeAPI> apis = new Dictionary<string, IExchangeAPI>(StringComparer.OrdinalIgnoreCase);

        private IEnumerable<ExchangeMarket> exchangeMarkets;

        /// <summary>
        /// Gets the exchange market from this exchange's SymbolsMetadata cache.
        /// </summary>
        /// <param name="symbol">The symbol. Ex. ADA/BTC</param>
        /// <returns>The ExchangeMarket or null if it doesn't exist</returns>
        protected ExchangeMarket GetExchangeMarket(string symbol)
        {
            PopulateExchangeMarkets();
            return exchangeMarkets.FirstOrDefault(x => x.MarketName == symbol);
        }

        /// <summary>
        /// Call GetSymbolsMetadata and store the results.
        /// </summary>
        private void PopulateExchangeMarkets()
        {
            // Get the exchange markets if we haven't gotten them yet.
            if (exchangeMarkets == null)
            {
                lock (this)
                {
                    if (exchangeMarkets == null)
                    {
                        exchangeMarkets = GetSymbolsMetadata();
                    }
                }
            }
        }

        /// <summary>
        /// Clamp price using market info
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="outputPrice">Price</param>
        /// <returns>Clamped price</returns>
        protected decimal ClampOrderPrice(string symbol, decimal outputPrice)
        {
            ExchangeMarket market = GetExchangeMarket(symbol);
            return market == null ? outputPrice : CryptoUtility.ClampDecimal(market.MinPrice, market.MaxPrice, market.PriceStepSize, outputPrice);
        }

        /// <summary>
        /// Clamp quantiy using market info
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="outputQuantity">Quantity</param>
        /// <returns>Clamped quantity</returns>
        protected decimal ClampOrderQuantity(string symbol, decimal outputQuantity)
        {
            ExchangeMarket market = GetExchangeMarket(symbol);
            return market == null ? outputQuantity : CryptoUtility.ClampDecimal(market.MinTradeSize, market.MaxTradeSize, market.QuantityStepSize, outputQuantity);
        }

        #region API Implementation

        protected virtual async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            var symbols = await GetSymbolsAsync();
            foreach (string symbol in symbols)
            {
                var ticker = await GetTickerAsync(symbol);
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ticker));
            }
            return tickers;
        }

        protected async virtual Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100)
        {
            await new SynchronizationContextRemover();

            List<KeyValuePair<string, ExchangeOrderBook>> books = new List<KeyValuePair<string, ExchangeOrderBook>>();
            var symbols = await GetSymbolsAsync();
            foreach (string symbol in symbols)
            {
                var book = await GetOrderBookAsync(symbol);
                books.Add(new KeyValuePair<string, ExchangeOrderBook>(symbol, book));
            }
            return books;
        }

        protected virtual async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            await GetHistoricalTradesAsync((e) =>
            {
                trades.AddRange(e);
                return true;
            }, symbol);
            return trades;
        }

        protected virtual Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync() => throw new NotImplementedException();
        protected virtual Task<IEnumerable<string>> OnGetSymbolsAsync() => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeTicker> OnGetTickerAsync(string symbol) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100) => throw new NotImplementedException();
        protected virtual Task OnGetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null) => throw new NotImplementedException();
        protected virtual Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null) => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAsync() => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null) => throw new NotImplementedException();
        protected virtual Task OnCancelOrderAsync(string orderId, string symbol = null) => throw new NotImplementedException();
        protected virtual Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest) => throw new NotImplementedException();

        #endregion API implementation

        /// <summary>
        /// Separator for exchange symbol, derived classes can change in constructor. This should be a single char string or empty string.
        /// </summary>
        protected string SymbolSeparator { get; set; } = "-";

        /// <summary>
        /// Whether the exchange symbol is reversed from most other exchanges, derived classes can change to true in constructor
        /// </summary>
        protected bool SymbolIsReversed { get; set; }

        /// <summary>
        /// Whether the exchange symbol is uppercase
        /// </summary>
        protected bool SymbolIsUppercase { get; set; } = true;

        /// <summary>
        /// List of exchange to global currency conversions. Exchange currency is key, global currency is value.
        /// </summary>
        protected static readonly Dictionary<Type, KeyValuePair<string, string>[]> ExchangeGlobalCurrencyReplacements = new Dictionary<Type, KeyValuePair<string, string>[]>();

        /// <summary>
        /// Convert an exchange symbol into a global symbol, which will be the same for all exchanges.
        /// Global symbols are always uppercase and separate the currency pair with a hyphen (-).
        /// Global symbols list the base currency first (i.e. BTC) and conversion currency
        /// second (i.e. USD). Example BTC-USD, read as x BTC is worth y USD.
        /// </summary>
        /// <param name="symbol">Exchange symbol</param>
        /// <param name="separator">Separator</param>
        /// <returns>Global symbol</returns>
        protected string ExchangeSymbolToGlobalSymbolWithSeparator(string symbol, char separator = GlobalSymbolSeparator)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                throw new ArgumentException("Symbol must be non null and non empty");
            }
            string[] pieces = symbol.Split(separator);
            if (SymbolIsReversed)
            {
                return ExchangeCurrencyToGlobalCurrency(pieces[1]).ToUpperInvariant() + GlobalSymbolSeparator + ExchangeCurrencyToGlobalCurrency(pieces[0]).ToUpperInvariant();
            }
            return ExchangeCurrencyToGlobalCurrency(pieces[0]).ToUpperInvariant() + GlobalSymbolSeparator + ExchangeCurrencyToGlobalCurrency(pieces[1]).ToUpperInvariant();
        }

        /// <summary>
        /// Static constructor
        /// </summary>
        static ExchangeAPI()
        {
            foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI))))
            {
                ExchangeAPI api = Activator.CreateInstance(type) as ExchangeAPI;
                apis[api.Name] = api;
                ExchangeGlobalCurrencyReplacements[type] = new KeyValuePair<string, string>[0];
            }
        }

        /// <summary>
        /// Get an exchange API given an exchange name (see ExchangeName class)
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
        /// Normalize a symbol for use on this exchange. The symbol should already be in the correct order,
        /// this method just deals with casing and putting in the right separator.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        public virtual string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty);
        }

        /// <summary>
        /// Convert an exchange symbol into a global symbol, which will be the same for all exchanges.
        /// Global symbols are always uppercase and separate the currency pair with a hyphen (-).
        /// Global symbols list the base currency first (i.e. BTC) and conversion currency
        /// second (i.e. USD). Example BTC-USD, read as x BTC is worth y USD.
        /// </summary>
        /// <param name="symbol">Exchange symbol</param>
        /// <returns>Global symbol</returns>
        public virtual string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(SymbolSeparator))
            {
                throw new ArgumentException("Exchange has set an empty SymbolSeparator, the exchange must override this method");
            }
            return ExchangeSymbolToGlobalSymbolWithSeparator(symbol, SymbolSeparator[0]);
        }

        /// <summary>
        /// Convert a global symbol into an exchange symbol, which will potentially be different from other exchanges.
        /// </summary>
        /// <param name="symbol">Global symbol</param>
        /// <returns>Exchange symbol</returns>
        public virtual string GlobalSymbolToExchangeSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Symbol must be non null and non empty");
            }
            int pos = symbol.IndexOf(GlobalSymbolSeparator);
            if (SymbolIsReversed)
            {
                symbol = GlobalCurrencyToExchangeCurrency(symbol.Substring(pos + 1)) + SymbolSeparator + GlobalCurrencyToExchangeCurrency(symbol.Substring(0, pos));
            }
            else
            {
                symbol = GlobalCurrencyToExchangeCurrency(symbol.Substring(0, pos)) + SymbolSeparator + GlobalCurrencyToExchangeCurrency(symbol.Substring(pos + 1));
            }
            return (SymbolIsUppercase ? symbol.ToUpperInvariant() : symbol.ToLowerInvariant());
        }

        /// <summary>
        /// Convert an exchange currency to a global currency. For example, on Binance,
        /// BCH (Bitcoin Cash) is BCC but in most other exchanges it is BCH, hence
        /// the global symbol is BCH.
        /// </summary>
        /// <param name="currency">Exchange currency</param>
        /// <returns>Global currency</returns>
        public string ExchangeCurrencyToGlobalCurrency(string currency)
        {
            currency = (currency ?? string.Empty);
            foreach (KeyValuePair<string, string> kv in ExchangeGlobalCurrencyReplacements[GetType()])
            {
                currency = currency.Replace(kv.Key, kv.Value);
            }
            return currency.ToUpperInvariant();
        }

        /// <summary>
        /// Convert a global currency to exchange currency. For example, on Binance,
        /// BCH (Bitcoin Cash) is BCC but in most other exchanges it is BCH, hence
        /// the global symbol BCH would convert to BCC for Binance, but stay BCH
        /// for most other exchanges.
        /// </summary>
        /// <param name="currency">Global currency</param>
        /// <returns>Exchange currency</returns>
        public string GlobalCurrencyToExchangeCurrency(string currency)
        {
            currency = (currency ?? string.Empty);
            foreach (KeyValuePair<string, string> kv in ExchangeGlobalCurrencyReplacements[GetType()])
            {
                currency = currency.Replace(kv.Value, kv.Key);
            }
            return (SymbolIsUppercase ? currency.ToUpperInvariant() : currency.ToLowerInvariant());
        }

        /// <summary>
        /// Get all tickers via web socket
        /// </summary>
        /// <param name="tickers">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IDisposable GetTickersWebSocket(System.Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers) => throw new NotImplementedException();

        /// <summary>
        /// Get the details of all completed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IDisposable GetCompletedOrderDetailsWebSocket(System.Action<ExchangeOrderResult> callback) => throw new NotImplementedException();

        /// <summary>
        /// Gets currencies and related data such as IsEnabled and TxFee (if available)
        /// </summary>
        /// <returns>Collection of Currencies</returns>
        public IReadOnlyDictionary<string, ExchangeCurrency> GetCurrencies() => GetCurrenciesAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Gets currencies and related data such as IsEnabled and TxFee (if available)
        /// </summary>
        /// <returns>Collection of Currencies</returns>
        public async Task<IReadOnlyDictionary<string, ExchangeCurrency>> GetCurrenciesAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetCurrenciesAsync();
        }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public IEnumerable<string> GetSymbols() => GetSymbolsAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public async Task<IEnumerable<string>> GetSymbolsAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetSymbolsAsync();
        }

        /// <summary>
        /// Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        public IEnumerable<ExchangeMarket> GetSymbolsMetadata() => GetSymbolsMetadataAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        public async Task<IEnumerable<ExchangeMarket>> GetSymbolsMetadataAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetSymbolsMetadataAsync();
        }

        /// <summary>
        /// Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public ExchangeTicker GetTicker(string symbol) => GetTickerAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public async Task<ExchangeTicker> GetTickerAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetTickerAsync(symbol);
        }

        /// <summary>
        /// Get all tickers in one request. If the exchange does not support this, a ticker will be requested for each symbol.
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public IEnumerable<KeyValuePair<string, ExchangeTicker>> GetTickers() => GetTickersAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get all tickers in one request. If the exchange does not support this, a ticker will be requested for each symbol.
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetTickersAsync();
        }

        /// <summary>
        /// Get exchange order book
        /// </summary>
        /// <param name="symbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100) => GetOrderBookAsync(symbol, maxCount).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get exchange order book
        /// </summary>
        /// <param name="symbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public async Task<ExchangeOrderBook> GetOrderBookAsync(string symbol, int maxCount = 100)
        {
            await new SynchronizationContextRemover();
            return await OnGetOrderBookAsync(symbol, maxCount);
        }

        /// <summary>
        /// Get all exchange order book symbols in one request. If the exchange does not support this, an order book will be requested for each symbol. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public IEnumerable<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100) => GetOrderBooksAsync(maxCount).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get all exchange order book symbols in one request. If the exchange does not support this, an order book will be requested for each symbol. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100)
        {
            await new SynchronizationContextRemover();
            return await OnGetOrderBooksAsync(maxCount);
        }

        /// <summary>
        /// Get historical trades for the exchange
        /// </summary>
        /// <param name="callback">Callback for each set of trades. Return false to stop getting trades immediately.</param>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="sinceDateTime">Optional date time to start getting the historical data at, null for the most recent data</param>
        public void GetHistoricalTrades(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null) => GetHistoricalTradesAsync(callback, symbol, sinceDateTime).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get historical trades for the exchange
        /// </summary>
        /// <param name="callback">Callback for each set of trades. Return false to stop getting trades immediately.</param>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="sinceDateTime">Optional date time to start getting the historical data at, null for the most recent data</param>
        public async Task GetHistoricalTradesAsync(System.Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            await new SynchronizationContextRemover();
            await OnGetHistoricalTradesAsync(callback, symbol, sinceDateTime);
        }

        /// <summary>
        /// Get recent trades on the exchange - the default implementation simply calls GetHistoricalTrades with a null sinceDateTime.
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all recent trades</returns>
        public IEnumerable<ExchangeTrade> GetRecentTrades(string symbol) => GetRecentTradesAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get recent trades on the exchange - the default implementation simply calls GetHistoricalTrades with a null sinceDateTime.
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all recent trades</returns>
        public async Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetRecentTradesAsync(symbol);
        }

        /// <summary>
        /// Gets the address to deposit to and applicable details.
        /// </summary>
        /// <param name="symbol">Symbol to get address for.</param>
        /// <param name="forceRegenerate">Regenerate the address</param>
        /// <returns>Deposit address details (including tag if applicable, such as XRP)</returns>
        public ExchangeDepositDetails GetDepositAddress(string symbol, bool forceRegenerate = false) => GetDepositAddressAsync(symbol, forceRegenerate).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Gets the address to deposit to and applicable details.
        /// </summary>
        /// <param name="symbol">Symbol to get address for.</param>
        /// <param name="forceRegenerate">Regenerate the address</param>
        /// <returns>Deposit address details (including tag if applicable, such as XRP)</returns>
        public async Task<ExchangeDepositDetails> GetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            await new SynchronizationContextRemover();
            return await OnGetDepositAddressAsync(symbol, forceRegenerate);
        }

        /// <summary>
        /// Gets the deposit history for a symbol
        /// </summary>
        /// <param name="symbol">The symbol to check. May be null.</param>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        public IEnumerable<ExchangeTransaction> GetDepositHistory(string symbol) => GetDepositHistoryAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Gets the deposit history for a symbol
        /// </summary>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        public async Task<IEnumerable<ExchangeTransaction>> GetDepositHistoryAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetDepositHistoryAsync(symbol);
        }

        /// <summary>
        /// Get candles (open, high, low, close)
        /// </summary>
        /// <param name="symbol">Symbol to get candles for</param>
        /// <param name="periodSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <param name="limit">Max results, can be used instead of startDate and endDate if desired</param>
        /// <returns>Candles</returns>
        public IEnumerable<MarketCandle> GetCandles(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null) => GetCandlesAsync(symbol, periodSeconds, startDate, endDate, limit).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get candles (open, high, low, close)
        /// </summary>
        /// <param name="symbol">Symbol to get candles for</param>
        /// <param name="periodSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <param name="limit">Max results, can be used instead of startDate and endDate if desired</param>
        /// <returns>Candles</returns>
        public async Task<IEnumerable<MarketCandle>> GetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetCandlesAsync(symbol, periodSeconds, startDate, endDate, limit);
        }

        /// <summary>
        /// Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        public Dictionary<string, decimal> GetAmounts() => GetAmountsAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        public async Task<Dictionary<string, decimal>> GetAmountsAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetAmountsAsync();
        }

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public Dictionary<string, decimal> GetAmountsAvailableToTrade() => GetAmountsAvailableToTradeAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public async Task<Dictionary<string, decimal>> GetAmountsAvailableToTradeAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetAmountsAvailableToTradeAsync();
        }

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="order">The order request</param>
        /// <returns>Result</returns>
        public ExchangeOrderResult PlaceOrder(ExchangeOrderRequest order) => PlaceOrderAsync(order).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Place an order
        /// </summary>
        /// <param name="order">The order request</param>
        /// <returns>Result</returns>
        public async Task<ExchangeOrderResult> PlaceOrderAsync(ExchangeOrderRequest order)
        {
            await new SynchronizationContextRemover();
            return await OnPlaceOrderAsync(order);
        }

        /// <summary>
        /// Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <param name="symbol">Symbol of order (most exchanges do not require this)</param>
        /// <returns>Order details</returns>
        public ExchangeOrderResult GetOrderDetails(string orderId, string symbol = null) => GetOrderDetailsAsync(orderId, symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <param name="symbol">Symbol of order (most exchanges do not require this)</param>
        /// <returns>Order details</returns>
        public async Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId, string symbol = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetOrderDetailsAsync(orderId, symbol);
        }

        /// <summary>
        /// Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null) => GetOpenOrderDetailsAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public async Task<IEnumerable<ExchangeOrderResult>> GetOpenOrderDetailsAsync(string symbol = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetOpenOrderDetailsAsync(symbol);
        }

        /// <summary>
        /// Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <param name="afterDate">Only returns orders on or after the specified date/time</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        public IEnumerable<ExchangeOrderResult> GetCompletedOrderDetails(string symbol = null, DateTime? afterDate = null) => GetCompletedOrderDetailsAsync(symbol, afterDate).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <param name="afterDate">Only returns orders on or after the specified date/time</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        public async Task<IEnumerable<ExchangeOrderResult>> GetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetCompletedOrderDetailsAsync(symbol, afterDate);
        }

        /// <summary>
        /// Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        /// <param name="symbol">Symbol of order (most exchanges do not require this)</param>
        public void CancelOrder(string orderId, string symbol = null) => CancelOrderAsync(orderId, symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        /// <param name="symbol">Symbol of order (most exchanges do not require this)</param>
        public async Task CancelOrderAsync(string orderId, string symbol = null)
        {
            await new SynchronizationContextRemover();
            await OnCancelOrderAsync(orderId, symbol);
        }

        /// <summary>
        /// A withdrawal request.
        /// </summary>
        /// <param name="withdrawalRequest">The withdrawal request.</param>
        public ExchangeWithdrawalResponse Withdraw(ExchangeWithdrawalRequest withdrawalRequest) => WithdrawAsync(withdrawalRequest).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronous withdraws request.
        /// </summary>
        /// <param name="withdrawalRequest">The withdrawal request.</param>
        public async Task<ExchangeWithdrawalResponse> WithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            await new SynchronizationContextRemover();
            return await OnWithdrawAsync(withdrawalRequest);
        }
    }

    /// <summary>
    /// List of exchange names
    /// </summary>
    public static class ExchangeName
    {
        private static readonly string[] exchangeNames;

        static ExchangeName()
        {
            List<string> names = new List<string>();
            foreach (FieldInfo field in typeof(ExchangeName).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                names.Add(field.GetValue(null).ToString());
            }
            names.Sort();
            exchangeNames = names.ToArray();
        }

        /// <summary>
        /// Get a list of all exchange names
        /// </summary>
        public static IReadOnlyList<string> ExchangeNames { get { return exchangeNames; } }

        /// <summary>
        /// Abucoins
        /// </summary>
        public const string Abucoins = "Abucoins";

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
        /// Bleutrade
        /// </summary>
        public const string Bleutrade = "Bleutrade";

        /// <summary>
        /// Cryptopia
        /// </summary>
        public const string Cryptopia = "Cryptopia";

        /// <summary>
        /// GDAX
        /// </summary>
        public const string GDAX = "GDAX";

        /// <summary>
        /// Gemini
        /// </summary>
        public const string Gemini = "Gemini";

        /// <summary>
        /// Hitbtc
        /// </summary>
        public const string Hitbtc = "Hitbtc";

        /// <summary>
        /// Huobi
        /// </summary>
        public const string Huobi = "Huobi";

        /// <summary>
        /// Kraken
        /// </summary>
        public const string Kraken = "Kraken";

        /// <summary>
        /// Kucoin
        /// </summary>
        public const string Kucoin = "Kucoin";

        /// <summary>
        /// Livecoin
        /// </summary>
        public const string Livecoin = "Livecoin";

        /// <summary>
        /// Okex
        /// </summary>
        public const string Okex = "Okex";

        /// <summary>
        /// Poloniex
        /// </summary>
        public const string Poloniex = "Poloniex";

        /// <summary>
        /// TuxExchange
        /// </summary>
        public const string TuxExchange = "TuxExchange";

        /// <summary>
        /// Yobit
        /// </summary>
        public const string Yobit = "Yobit";
    }
}
