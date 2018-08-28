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
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Base class for all exchange API
    /// </summary>
    public abstract partial class ExchangeAPI : BaseAPI, IExchangeAPI
    {
        /// <summary>
        /// Separator for global symbols
        /// </summary>
        public const char GlobalSymbolSeparator = '-';

        #region Private methods

        private static readonly Dictionary<string, IExchangeAPI> apis = new Dictionary<string, IExchangeAPI>(StringComparer.OrdinalIgnoreCase);
        private bool disposed;

        #endregion Private methods

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

        protected virtual async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100)
        {
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
            symbol = NormalizeSymbol(symbol);
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
        protected virtual Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null) => throw new NotImplementedException();
        protected virtual Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null) => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAsync() => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetFeesAsync() => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] order) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null) => throw new NotImplementedException();
        protected virtual Task OnCancelOrderAsync(string orderId, string symbol = null) => throw new NotImplementedException();
        protected virtual Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest) => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeMarginPositionResult> OnGetOpenPositionAsync(string symbol) => throw new NotImplementedException();
        protected virtual Task<ExchangeCloseMarginPositionResult> OnCloseMarginPositionAsync(string symbol) => throw new NotImplementedException();

        protected virtual IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers) => throw new NotImplementedException();
        protected virtual IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols) => throw new NotImplementedException();
        protected virtual IWebSocket OnGetOrderBookDeltasWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols) => throw new NotImplementedException();
        protected virtual IWebSocket OnGetOrderDetailsWebSocket(Action<ExchangeOrderResult> callback) => throw new NotImplementedException();
        protected virtual IWebSocket OnGetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback) => throw new NotImplementedException();

        #endregion API implementation

        #region Protected methods

        /// <summary>
        /// Clamp price using market info. If necessary, a network request will be made to retrieve symbol metadata.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="outputPrice">Price</param>
        /// <returns>Clamped price</returns>
        protected async Task<decimal> ClampOrderPrice(string symbol, decimal outputPrice)
        {
            ExchangeMarket market = await GetExchangeMarketFromCacheAsync(symbol);
            return market == null ? outputPrice : CryptoUtility.ClampDecimal(market.MinPrice, market.MaxPrice, market.PriceStepSize, outputPrice);
        }

        /// <summary>
        /// Clamp quantiy using market info. If necessary, a network request will be made to retrieve symbol metadata.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="outputQuantity">Quantity</param>
        /// <returns>Clamped quantity</returns>
        protected async Task<decimal> ClampOrderQuantity(string symbol, decimal outputQuantity)
        {
            ExchangeMarket market = await GetExchangeMarketFromCacheAsync(symbol);
            return market == null ? outputQuantity : CryptoUtility.ClampDecimal(market.MinTradeSize, market.MaxTradeSize, market.QuantityStepSize, outputQuantity);
        }

        /// <summary>
        /// Convert an exchange symbol into a global symbol, which will be the same for all exchanges.
        /// Global symbols are always uppercase and separate the currency pair with a hyphen (-).
        /// Global symbols list the base currency first (i.e. BTC) and conversion currency
        /// second (i.e. ETH). Example BTC-ETH, read as x BTC is worth y ETH.
        /// BTC is always first, then ETH, etc. Fiat pair is always first in global symbol too.
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
                return ExchangeCurrencyToGlobalCurrency(pieces[0]).ToUpperInvariant() + GlobalSymbolSeparator + ExchangeCurrencyToGlobalCurrency(pieces[1]).ToUpperInvariant();
            }
            return ExchangeCurrencyToGlobalCurrency(pieces[1]).ToUpperInvariant() + GlobalSymbolSeparator + ExchangeCurrencyToGlobalCurrency(pieces[0]).ToUpperInvariant();
        }

        /// <summary>
        /// Override to dispose of resources when the exchange is disposed
        /// </summary>
        protected virtual void OnDispose() { }

        #endregion Protected methods

        #region Other

        /// <summary>
        /// Static constructor
        /// </summary>
        static ExchangeAPI()
        {
            foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI)) && !type.IsAbstract))
            {
                // lazy create, we just create an instance to get the name, nothing more
                // we don't want to pro-actively create all of these becanse an API
                // may be running a timer or other house-keeping which we don't want
                // the overhead of if a user is only using one or a handful of the apis
                using (ExchangeAPI api = Activator.CreateInstance(type) as ExchangeAPI)
                {
                    apis[api.Name] = null;
                }
                ExchangeGlobalCurrencyReplacements[type] = new KeyValuePair<string, string>[0];
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ExchangeAPI()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose and cleanup all resources
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                OnDispose();
                Cache?.Dispose();

                // take out of global api dictionary if disposed
                lock (apis)
                {
                    if (apis.TryGetValue(Name, out var cachedApi) && cachedApi == this)
                    {
                        apis[Name] = null;
                    }
                }
            }
        }

        /// <summary>
        /// Get an exchange API given an exchange name (see ExchangeName class)
        /// </summary>
        /// <param name="exchangeName">Exchange name</param>
        /// <returns>Exchange API or null if not found</returns>
        public static IExchangeAPI GetExchangeAPI(string exchangeName)
        {
            // note: this method will be slightly slow (milliseconds) the first time it is called and misses the cache
            // subsequent calls with cache hits will be nanoseconds
            lock (apis)
            {
                if (!apis.TryGetValue(exchangeName, out IExchangeAPI api))
                {
                    throw new ArgumentException("No API available with name " + exchangeName);
                }
                if (api == null)
                {
                    // find an API with the right name
                    foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI)) && !type.IsAbstract))
                    {
                        api = Activator.CreateInstance(type) as ExchangeAPI;
                        if (api.Name == exchangeName)
                        {
                            // found one with right name, add it to the API dictionary
                            apis[exchangeName] = api;

                            // break out, we are done
                            break;
                        }
                        else
                        {
                            // name didn't match, dispose immediately to stop timers and other nasties we don't want running, and null out api variable
                            api.Dispose();
                            api = null;
                        }
                    }
                }
                return api;
            }
        }

        /// <summary>
        /// Get all exchange APIs
        /// </summary>
        /// <returns>All APIs</returns>
        public static IExchangeAPI[] GetExchangeAPIs()
        {
            lock (apis)
            {
                List<IExchangeAPI> apiList = new List<IExchangeAPI>();
                foreach (var kv in apis.ToArray())
                {
                    if (kv.Value == null)
                    {
                        apiList.Add(GetExchangeAPI(kv.Key));
                    }
                    else
                    {
                        apiList.Add(kv.Value);
                    }
                }
                return apiList.ToArray();
            }
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
        /// Normalize an exchange specific symbol. The symbol should already be in the correct order,
        /// this method just deals with casing and putting in the right separator.
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        public virtual string NormalizeSymbol(string symbol)
        {
            symbol = (symbol ?? string.Empty).Trim();
            symbol = symbol.Replace("-", SymbolSeparator)
                .Replace("/", SymbolSeparator)
                .Replace("_", SymbolSeparator)
                .Replace(" ", SymbolSeparator)
                .Replace(":", SymbolSeparator);
            if (SymbolIsUppercase)
            {
                return symbol.ToUpperInvariant();
            }
            return symbol.ToLowerInvariant();
        }

        /// <summary>
        /// Convert an exchange symbol into a global symbol, which will be the same for all exchanges.
        /// Global symbols are always uppercase and separate the currency pair with a hyphen (-).
        /// Global symbols list the base currency first (i.e. BTC) and conversion currency
        /// second (i.e. ETH). Example BTC-ETH, read as x BTC is worth y ETH.
        /// BTC is always first, then ETH, etc. Fiat pair is always first in global symbol too.
        /// </summary>
        /// <param name="symbol">Exchange symbol</param>
        /// <returns>Global symbol</returns>
        public virtual string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(SymbolSeparator))
            {
                if (symbol.Length != 6)
                {
                    throw new InvalidOperationException(Name + " symbol must be 6 chars: '" + symbol + "' is not. Override this method to handle symbols that are not 6 chars in length.");
                }
                return ExchangeSymbolToGlobalSymbolWithSeparator(symbol.Substring(0, symbol.Length - 3) + GlobalSymbolSeparator + (symbol.Substring(symbol.Length - 3, 3)), GlobalSymbolSeparator);
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
                symbol = GlobalCurrencyToExchangeCurrency(symbol.Substring(0, pos)) + SymbolSeparator + GlobalCurrencyToExchangeCurrency(symbol.Substring(pos + 1));
            }
            else
            {
                symbol = GlobalCurrencyToExchangeCurrency(symbol.Substring(pos + 1)) + SymbolSeparator + GlobalCurrencyToExchangeCurrency(symbol.Substring(0, pos));
            }
            return (SymbolIsUppercase ? symbol.ToUpperInvariant() : symbol.ToLowerInvariant());
        }

        /// <summary>
        /// Convert seconds to a period string, or throw exception if seconds invalid. Example: 60 seconds becomes 1m.
        /// </summary>
        /// <param name="seconds">Seconds</param>
        /// <returns>Period string</returns>
        public virtual string PeriodSecondsToString(int seconds) => CryptoUtility.SecondsToPeriodString(seconds);

        #endregion Other

        #region REST API

        /// <summary>
        /// Gets currencies and related data such as IsEnabled and TxFee (if available)
        /// </summary>
        /// <returns>Collection of Currencies</returns>
        public virtual async Task<IReadOnlyDictionary<string, ExchangeCurrency>> GetCurrenciesAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetCurrenciesAsync();
        }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public virtual async Task<IEnumerable<string>> GetSymbolsAsync()
        {
            await new SynchronizationContextRemover();
            return (await Cache.Get<string[]>(nameof(GetSymbolsAsync), async () =>
            {
                return new CachedItem<string[]>((await OnGetSymbolsAsync()).ToArray(), DateTime.UtcNow.AddHours(1.0));
            })).Value;
        }

        /// <summary>
        /// Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        public virtual async Task<IEnumerable<ExchangeMarket>> GetSymbolsMetadataAsync()
        {
            await new SynchronizationContextRemover();
            return (await Cache.Get<ExchangeMarket[]>(nameof(GetSymbolsMetadataAsync), async () =>
            {
                return new CachedItem<ExchangeMarket[]>((await OnGetSymbolsMetadataAsync()).ToArray(), DateTime.UtcNow.AddHours(1.0));
            })).Value;
        }

        /// <summary>
        /// Gets the exchange market from this exchange's SymbolsMetadata cache. This will make a network request if needed to retrieve fresh markets from the exchange using GetSymbolsMetadataAsync().
        /// Please note that sending a symbol that is not found over and over will result in many network requests. Only send symbols that you are confident exist on the exchange.
        /// </summary>
        /// <param name="symbol">The symbol. Ex. ADA/BTC. This is assumed to be normalized and already correct for the exchange.</param>
        /// <returns>The ExchangeMarket or null if it doesn't exist in the cache or there was an error</returns>
        public virtual async Task<ExchangeMarket> GetExchangeMarketFromCacheAsync(string symbol)
        {
            try
            {
                // not sure if this is needed, but adding it just in case
                await new SynchronizationContextRemover();
                ExchangeMarket[] markets = (await GetSymbolsMetadataAsync()).ToArray();
                ExchangeMarket market = markets.FirstOrDefault(m => m.MarketName == symbol);
                if (market == null)
                {
                    // try again with a fresh request, every symbol *should* be in the response from PopulateExchangeMarketsAsync
                    Cache.Remove(nameof(GetSymbolsMetadataAsync));

                    markets = (await GetSymbolsMetadataAsync()).ToArray();

                    // try and find the market again, this time if not found we give up and just return null
                    market = markets.FirstOrDefault(m => m.MarketName == symbol);
                }
                return market;
            }
            catch
            {
                // TODO: Report the error somehow, for now a failed network request will just return null symbol which will force the caller to use default handling
            }
            return null;
        }

        /// <summary>
        /// Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public virtual async Task<ExchangeTicker> GetTickerAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetTickerAsync(NormalizeSymbol(symbol));
        }

        /// <summary>
        /// Get all tickers in one request. If the exchange does not support this, a ticker will be requested for each symbol.
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public virtual async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync()
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
        public virtual async Task<ExchangeOrderBook> GetOrderBookAsync(string symbol, int maxCount = 100)
        {
            await new SynchronizationContextRemover();
            return await OnGetOrderBookAsync(NormalizeSymbol(symbol), maxCount);
        }

        /// <summary>
        /// Get all exchange order book symbols in one request. If the exchange does not support this, an order book will be requested for each symbol. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public virtual async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100)
        {
            await new SynchronizationContextRemover();
            return await OnGetOrderBooksAsync(maxCount);
        }

        /// <summary>
        /// Get historical trades for the exchange. TODO: Change to async enumerator when available.
        /// </summary>
        /// <param name="callback">Callback for each set of trades. Return false to stop getting trades immediately.</param>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="startDate">Optional UTC start date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        /// <param name="endDate">Optional UTC end date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        public virtual async Task GetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            await new SynchronizationContextRemover();
            await OnGetHistoricalTradesAsync(callback, NormalizeSymbol(symbol), startDate, endDate);
        }

        /// <summary>
        /// Get recent trades on the exchange - the default implementation simply calls GetHistoricalTrades with a null sinceDateTime.
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all recent trades</returns>
        public virtual async Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetRecentTradesAsync(NormalizeSymbol(symbol));
        }

        /// <summary>
        /// Gets the address to deposit to and applicable details.
        /// </summary>
        /// <param name="symbol">Symbol to get address for.</param>
        /// <param name="forceRegenerate">Regenerate the address</param>
        /// <returns>Deposit address details (including tag if applicable, such as XRP)</returns>
        public virtual async Task<ExchangeDepositDetails> GetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            await new SynchronizationContextRemover();
            return await OnGetDepositAddressAsync(NormalizeSymbol(symbol), forceRegenerate);
        }

        /// <summary>
        /// Gets the deposit history for a symbol
        /// </summary>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        public virtual async Task<IEnumerable<ExchangeTransaction>> GetDepositHistoryAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetDepositHistoryAsync(NormalizeSymbol(symbol));
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
        public virtual async Task<IEnumerable<MarketCandle>> GetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetCandlesAsync(NormalizeSymbol(symbol), periodSeconds, startDate, endDate, limit);
        }

        /// <summary>
        /// Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        public virtual async Task<Dictionary<string, decimal>> GetAmountsAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetAmountsAsync();
        }

        /// <summary>
        /// Get fees
        /// </summary>
        /// <returns>The customer trading fees</returns>
        public virtual async Task<Dictionary<string, decimal>> GetFeesAync()
        {
            await new SynchronizationContextRemover();
            return await OnGetFeesAsync();
        }

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual async Task<Dictionary<string, decimal>> GetAmountsAvailableToTradeAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetAmountsAvailableToTradeAsync();
        }

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="order">The order request</param>
        /// <returns>Result</returns>
        public virtual async Task<ExchangeOrderResult> PlaceOrderAsync(ExchangeOrderRequest order)
        {
            await new SynchronizationContextRemover();
            order.Symbol = NormalizeSymbol(order.Symbol);
            return await OnPlaceOrderAsync(order);
        }

        /// <summary>
        /// Place bulk orders
        /// </summary>
        /// <param name="orders">Order requests</param>
        /// <returns>Order results, each result matches up with each order in index</returns>
        public virtual async Task<ExchangeOrderResult[]> PlaceOrdersAsync(params ExchangeOrderRequest[] orders)
        {
            await new SynchronizationContextRemover();
            foreach (ExchangeOrderRequest request in orders)
            {
                request.Symbol = NormalizeSymbol(request.Symbol);
            }
            return await OnPlaceOrdersAsync(orders);
        }

        /// <summary>
        /// Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <param name="symbol">Symbol of order (most exchanges do not require this)</param>
        /// <returns>Order details</returns>
        public virtual async Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId, string symbol = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetOrderDetailsAsync(orderId, NormalizeSymbol(symbol));
        }

        /// <summary>
        /// Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public virtual async Task<IEnumerable<ExchangeOrderResult>> GetOpenOrderDetailsAsync(string symbol = null)
        {
            await new SynchronizationContextRemover();
            return await OnGetOpenOrderDetailsAsync(NormalizeSymbol(symbol));
        }

        /// <summary>
        /// Get the details of all completed orders
        /// </summary>
        /// <param name="symbol">Symbol to get completed orders for or null for all</param>
        /// <param name="afterDate">Only returns orders on or after the specified date/time</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        public virtual async Task<IEnumerable<ExchangeOrderResult>> GetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            await new SynchronizationContextRemover();
            symbol = NormalizeSymbol(symbol);
            string cacheKey = "GetCompletedOrderDetails_" + symbol + "_" + (afterDate == null ? string.Empty : afterDate.Value.Ticks.ToStringInvariant());
            return (await Cache.Get<ExchangeOrderResult[]>(cacheKey, async () =>
            {
                return new CachedItem<ExchangeOrderResult[]>((await OnGetCompletedOrderDetailsAsync(symbol, afterDate)).ToArray(), DateTime.UtcNow.AddMinutes(2.0));
            })).Value;
        }

        /// <summary>
        /// Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        /// <param name="symbol">Symbol of order (most exchanges do not require this)</param>
        public virtual async Task CancelOrderAsync(string orderId, string symbol = null)
        {
            await new SynchronizationContextRemover();
            await OnCancelOrderAsync(orderId, NormalizeSymbol(symbol));
        }

        /// <summary>
        /// Asynchronous withdraws request.
        /// </summary>
        /// <param name="withdrawalRequest">The withdrawal request.</param>
        public virtual async Task<ExchangeWithdrawalResponse> WithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            await new SynchronizationContextRemover();
            withdrawalRequest.Currency = NormalizeSymbol(withdrawalRequest.Currency);
            return await OnWithdrawAsync(withdrawalRequest);
        }

        /// <summary>
        /// Get margin amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual async Task<Dictionary<string, decimal>> GetMarginAmountsAvailableToTradeAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetMarginAmountsAvailableToTradeAsync();
        }

        /// <summary>
        /// Get open margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Open margin position result</returns>
        public virtual async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetOpenPositionAsync(NormalizeSymbol(symbol));
        }

        /// <summary>
        /// Close a margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Close margin position result</returns>
        public virtual async Task<ExchangeCloseMarginPositionResult> CloseMarginPositionAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnCloseMarginPositionAsync(NormalizeSymbol(symbol));
        }

        #endregion REST API

        #region Web Socket API

        /// <summary>
        /// Get all tickers via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IWebSocket GetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback) => OnGetTickersWebSocket(callback);

        /// <summary>
        /// Get information about trades via web socket
        /// </summary>
        /// <param name="callback">Callback (symbol and trade)</param>
        /// <param name="symbols">Symbols</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IWebSocket GetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols) => OnGetTradesWebSocket(callback, symbols);

        /// <summary>
        /// Get delta order book bids and asks via web socket. Only the deltas are returned for each callback. To manage a full order book, use ExchangeAPIExtensions.GetOrderBookWebSocket.
        /// </summary>
        /// <param name="callback">Callback of symbol, order book</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <param name="symbol">Ticker symbols or null/empty for all of them (if supported)</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IWebSocket GetOrderBookDeltasWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols) => OnGetOrderBookDeltasWebSocket(callback, maxCount, symbols);

        /// <summary>
        /// Get the details of all changed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IWebSocket GetOrderDetailsWebSocket(Action<ExchangeOrderResult> callback) => OnGetOrderDetailsWebSocket(callback);

        /// <summary>
        /// Get the details of all completed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual IWebSocket GetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback) => OnGetCompletedOrderDetailsWebSocket(callback);

        #endregion Web Socket API
    }
}
