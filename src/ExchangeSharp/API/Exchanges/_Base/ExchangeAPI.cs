/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        /// Separator for global symbols (char)
        /// </summary>
        public const char GlobalMarketSymbolSeparator = '-';

        /// <summary>
        /// Separator for global symbols (string)
        /// </summary>
        public const string GlobalMarketSymbolSeparatorString = "-";

        /// <summary>
        /// Whether to use the default method cache policy, default is true.
        /// The default cache policy caches things like get symbols, tickers, order book, order details, etc. See ExchangeAPI constructor for full list.
        /// </summary>
        public static bool UseDefaultMethodCachePolicy { get; set; } = true;

        #region Private methods

        private static readonly Dictionary<string, IExchangeAPI?> apis = new Dictionary<string, IExchangeAPI?>(StringComparer.OrdinalIgnoreCase);
        private bool disposed;

		private static readonly Dictionary<string, string> classNamesToApiName = new Dictionary<string, string>();
		
        #endregion Private methods

        #region API Implementation

        protected virtual async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            var marketSymbols = await GetMarketSymbolsAsync();
            foreach (string marketSymbol in marketSymbols)
            {
                var ticker = await GetTickerAsync(marketSymbol);
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker));
            }
            return tickers;
        }

        protected virtual async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(
	        int maxCount = 100
        )
        {
	        var marketSymbols = await GetMarketSymbolsAsync();
	        var orderBooks = await Task.WhenAll(
		        marketSymbols.Select(async ms =>
		        {
			        var orderBook = await GetOrderBookAsync(ms, maxCount);
			        orderBook.MarketSymbol ??= ms;
			        return orderBook;
		        })
	        );
	        return orderBooks.ToDictionary(k => k.MarketSymbol, v => v);
        }

		/// <summary>
		/// When possible, the sort order will be Descending (ie newest trades first)
		/// </summary>
		/// <param name="marketSymbol">name of symbol</param>
		/// <param name="limit">max number of results returned, if limiting is supported by the exchange</param>
		/// <returns></returns>
		protected virtual async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            await GetHistoricalTradesAsync((e) =>
            {
                trades.AddRange(e);
                return true;
            }, marketSymbol, limit:limit); //KK2020
            return trades;
        }

        protected virtual Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync() => throw new NotImplementedException();
        protected virtual Task<IEnumerable<string>> OnGetMarketSymbolsAsync() => throw new NotImplementedException();
        protected internal virtual Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100) => throw new NotImplementedException();
        protected virtual Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null) => throw new NotImplementedException();
		//protected virtual Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null) => throw new NotImplementedException();
        protected virtual Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null) => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAsync() => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetFeesAsync() => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] order) => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string? marketSymbol = null) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string? marketSymbol = null) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string? marketSymbol = null, DateTime? afterDate = null) => throw new NotImplementedException();
        protected virtual Task OnCancelOrderAsync(string orderId, string? marketSymbol = null) => throw new NotImplementedException();
        protected virtual Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest) => throw new NotImplementedException();
        protected virtual Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawHistoryAsync(string currency) => throw new NotImplementedException();
        protected virtual Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances) => throw new NotImplementedException();
        protected virtual Task<ExchangeMarginPositionResult> OnGetOpenPositionAsync(string marketSymbol) => throw new NotImplementedException();
        protected virtual Task<ExchangeCloseMarginPositionResult> OnCloseMarginPositionAsync(string marketSymbol) => throw new NotImplementedException();
        protected virtual Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers, params string[] marketSymbols) => throw new NotImplementedException();
        protected virtual Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols) => throw new NotImplementedException();
        protected virtual Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols) => throw new NotImplementedException();
        protected virtual Task<IWebSocket> OnGetOrderDetailsWebSocketAsync(Action<ExchangeOrderResult> callback) => throw new NotImplementedException();
        protected virtual Task<IWebSocket> OnGetCompletedOrderDetailsWebSocketAsync(Action<ExchangeOrderResult> callback) => throw new NotImplementedException();
		protected virtual Task<IWebSocket> OnUserDataWebSocketAsync(Action<object> callback, string listenKey) => throw new NotImplementedException();

		#endregion API implementation

		#region Protected methods

		/// <summary>
		/// Clamp price using market info. If necessary, a network request will be made to retrieve symbol metadata.
		/// </summary>
		/// <param name="marketSymbol">Market Symbol</param>
		/// <param name="outputPrice">Price</param>
		/// <returns>Clamped price</returns>
		protected async Task<decimal> ClampOrderPrice(string marketSymbol, decimal outputPrice)
        {
            ExchangeMarket? market = await GetExchangeMarketFromCacheAsync(marketSymbol);
            return market == null ? outputPrice : CryptoUtility.ClampDecimal(market.MinPrice, market.MaxPrice, market.PriceStepSize, outputPrice);
        }

        /// <summary>
        /// Clamp quantiy using market info. If necessary, a network request will be made to retrieve symbol metadata.
        /// </summary>
        /// <param name="marketSymbol">Market Symbol</param>
        /// <param name="outputQuantity">Quantity</param>
        /// <returns>Clamped quantity</returns>
        protected async Task<decimal> ClampOrderQuantity(string marketSymbol, decimal outputQuantity)
        {
            ExchangeMarket? market = await GetExchangeMarketFromCacheAsync(marketSymbol);
            return market == null ? outputQuantity : CryptoUtility.ClampDecimal(market.MinTradeSize, market.MaxTradeSize, market.QuantityStepSize, outputQuantity);
        }

		/// <summary>
		/// Convert an exchange symbol into a global symbol, which will be the same for all exchanges.
		/// Global symbols are always uppercase and separate the currency pair with a hyphen (-).
		/// Global symbols list the base currency first (i.e. BTC) and quote/conversion currency
		/// second (i.e. USD). Global symbols are of the form BASE-QUOTE. BASE-QUOTE is read as
		/// 1 BASE is worth y QUOTE. 
		///
		/// Examples:
		///		On 1/25/2020,
		///			- BTC-USD: $8,371; 1 BTC (base) is worth $8,371 USD (quote)
		///			- ETH-BTC: 0.01934; 1 ETH is worth 0.01934 BTC
		///			- EUR-USD: 1.2; 1 EUR worth 1.2 USD
		/// 
		/// A value greater than 1 means one unit of base currency is more valuable than one unit of
		/// quote currency.
		/// 
		/// </summary>
		/// <param name="marketSymbol">Exchange market symbol</param>
		/// <param name="separator">Separator</param>
		/// <returns>Global symbol</returns>
		protected async Task<string> ExchangeMarketSymbolToGlobalMarketSymbolWithSeparatorAsync(string marketSymbol, char separator = GlobalMarketSymbolSeparator)
		{
			if (string.IsNullOrEmpty(marketSymbol))
			{
				throw new ArgumentException("Symbol must be non null and non empty");
			}
			string[] pieces = marketSymbol.Split(separator);
			if (MarketSymbolIsReversed == false) //if reversed then put quote currency first
			{
				return (await ExchangeCurrencyToGlobalCurrencyAsync(pieces[0])).ToUpperInvariant() + GlobalMarketSymbolSeparator + (await ExchangeCurrencyToGlobalCurrencyAsync(pieces[1])).ToUpperInvariant();
			}
			return (await ExchangeCurrencyToGlobalCurrencyAsync(pieces[1])).ToUpperInvariant() + GlobalMarketSymbolSeparator + (await ExchangeCurrencyToGlobalCurrencyAsync(pieces[0])).ToUpperInvariant();
		}

		/// <summary>
		/// Split a market symbol into currencies. For weird exchanges like Bitthumb, they can override and hard-code the other pair
		/// </summary>
		/// <param name="marketSymbol">Market symbol</param>
		/// <returns>Base and quote currency</returns>
		protected virtual (string baseCurrency, string quoteCurrency) OnSplitMarketSymbolToCurrencies(string marketSymbol)
        {
            var pieces = marketSymbol.Split(MarketSymbolSeparator[0]);
            if (pieces.Length < 2)
            {
                throw new InvalidOperationException($"Splitting {Name} symbol '{marketSymbol}' with symbol separator '{MarketSymbolSeparator}' must result in at least 2 pieces.");
            }
            string baseCurrency = MarketSymbolIsReversed ? pieces[1] : pieces[0];
            string quoteCurrency = MarketSymbolIsReversed ? pieces[0] : pieces[1];
            return (baseCurrency, quoteCurrency);
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
                if ((Activator.CreateInstance(type) is ExchangeAPI api))
                {
                    api.Dispose();
                    apis[api.Name] = null;
                    classNamesToApiName[type.Name] = api.Name;
				}

                // in case derived class is accessed first, check for existance of key
                if (!ExchangeGlobalCurrencyReplacements.ContainsKey(type))
                {
                    ExchangeGlobalCurrencyReplacements[type] = new KeyValuePair<string, string>[0];
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ExchangeAPI()
        {
            if (UseDefaultMethodCachePolicy)
            {
                MethodCachePolicy.Add(nameof(GetCurrenciesAsync), TimeSpan.FromHours(1.0));
                MethodCachePolicy.Add(nameof(GetMarketSymbolsAsync), TimeSpan.FromHours(1.0));
                MethodCachePolicy.Add(nameof(GetMarketSymbolsMetadataAsync), TimeSpan.FromHours(4.0));
                MethodCachePolicy.Add(nameof(GetTickerAsync), TimeSpan.FromSeconds(10.0));
                MethodCachePolicy.Add(nameof(GetTickersAsync), TimeSpan.FromSeconds(10.0));
                MethodCachePolicy.Add(nameof(GetOrderBookAsync), TimeSpan.FromSeconds(10.0));
                MethodCachePolicy.Add(nameof(GetOrderBooksAsync), TimeSpan.FromSeconds(10.0));
                MethodCachePolicy.Add(nameof(GetCandlesAsync), TimeSpan.FromSeconds(10.0));
                MethodCachePolicy.Add(nameof(GetAmountsAsync), TimeSpan.FromMinutes(1.0));
                MethodCachePolicy.Add(nameof(GetAmountsAvailableToTradeAsync), TimeSpan.FromMinutes(1.0));
                MethodCachePolicy.Add(nameof(GetCompletedOrderDetailsAsync), TimeSpan.FromMinutes(2.0));
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

		public static IExchangeAPI? GetExchangeAPIFromClassName(string className)
        {
	        if (!classNamesToApiName.TryGetValue(className, out string exchangeName))
		        throw new ArgumentException("No API available with class name " + className);

	        return GetExchangeAPI(exchangeName);
        }

		/// <summary>
		/// Get an exchange API given an exchange name (see ExchangeName class)
		/// </summary>
		/// <param name="exchangeName">Exchange name</param>
		/// <returns>Exchange API or null if not found</returns>
		public static IExchangeAPI? GetExchangeAPI(string exchangeName)
        {
            // note: this method will be slightly slow (milliseconds) the first time it is called and misses the cache
            // subsequent calls with cache hits will be nanoseconds
            lock (apis)
            {
                if (!apis.TryGetValue(exchangeName, out IExchangeAPI? api))
                {
                    throw new ArgumentException("No API available with name " + exchangeName);
                }
                if (api == null)
                {
                    // find an API with the right name
                    foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI)) && !type.IsAbstract))
                    {
                        if (!((api = Activator.CreateInstance(type) as IExchangeAPI) is null))
                        {
                            if (api.Name.Equals(exchangeName, StringComparison.OrdinalIgnoreCase))
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
		public Task<string> ExchangeCurrencyToGlobalCurrencyAsync(string currency)
		{
			currency = (currency ?? string.Empty);
			foreach (KeyValuePair<string, string> kv in ExchangeGlobalCurrencyReplacements[GetType()])
			{
				currency = currency.Replace(kv.Key, kv.Value);
			}
			return Task.FromResult(currency.ToUpperInvariant());
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
            return (MarketSymbolIsUppercase ? currency.ToUpperInvariant() : currency.ToLowerInvariant());
        }

        /// <summary>
        /// Normalize an exchange specific symbol. The symbol should already be in the correct order,
        /// this method just deals with casing and putting in the right separator.
        /// </summary>
        /// <param name="marketSymbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        public virtual string NormalizeMarketSymbol(string? marketSymbol)
        {
            marketSymbol = (marketSymbol ?? string.Empty).Trim();
            marketSymbol = marketSymbol.Replace("-", MarketSymbolSeparator)
                .Replace("/", MarketSymbolSeparator)
                .Replace("_", MarketSymbolSeparator)
                .Replace(" ", MarketSymbolSeparator)
                .Replace(":", MarketSymbolSeparator);
            if (MarketSymbolIsUppercase)
            {
                return marketSymbol.ToUpperInvariant();
            }
            return marketSymbol.ToLowerInvariant();
        }

		/// <summary>
		/// Convert an exchange symbol into a global symbol, which will be the same for all exchanges.
		/// Global symbols are always uppercase and separate the currency pair with a hyphen (-).
		/// Global symbols list the base currency first (i.e. BTC) and quote/conversion currency
		/// second (i.e. USD). Global symbols are of the form BASE-QUOTE. BASE-QUOTE is read as
		/// 1 BASE is worth y QUOTE. 
		///
		/// Examples:
		///		On 1/25/2020,
		///			- BTC-USD: $8,371; 1 BTC (base) is worth $8,371 USD (quote)
		///			- ETH-BTC: 0.01934; 1 ETH is worth 0.01934 BTC
		///			- EUR-USD: 1.2; 1 EUR worth 1.2 USD
		/// 
		/// A value greater than 1 means one unit of base currency is more valuable than one unit of
		/// quote currency.
		/// </summary>
		/// <param name="marketSymbol">Exchange symbol</param>
		/// <returns>Global symbol</returns>
		public virtual async Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
        {
            string modifiedMarketSymbol = marketSymbol;
            char separator;

            // if no separator, we must query metadata and build the pair
            if (string.IsNullOrWhiteSpace(MarketSymbolSeparator))
            {
                // we must look it up via metadata, most often this call will be cached and fast
                ExchangeMarket marketSymbolMetadata = await GetExchangeMarketFromCacheAsync(marketSymbol);
                if (marketSymbolMetadata == null)
                {
                    throw new InvalidDataException($"No market symbol metadata returned or unable to find symbol metadata for {marketSymbol}");
                }
                modifiedMarketSymbol = marketSymbolMetadata.BaseCurrency + GlobalMarketSymbolSeparatorString + marketSymbolMetadata.QuoteCurrency;
                separator = GlobalMarketSymbolSeparator;
            }
            else
            {
                separator = MarketSymbolSeparator[0];
            }
            return await ExchangeMarketSymbolToGlobalMarketSymbolWithSeparatorAsync(modifiedMarketSymbol, separator);
        }

        /// <summary>
        /// Convert currencies to exchange market symbol
        /// </summary>
        /// <param name="baseCurrency">Base currency</param>
        /// <param name="quoteCurrency">Quote currency</param>
        /// <returns>Exchange market symbol</returns>
        public virtual Task<string> CurrenciesToExchangeMarketSymbol(string baseCurrency, string quoteCurrency)
        {
            string symbol = (MarketSymbolIsReversed ? $"{quoteCurrency}{MarketSymbolSeparator}{baseCurrency}" : $"{baseCurrency}{MarketSymbolSeparator}{quoteCurrency}");
            return Task.FromResult(MarketSymbolIsUppercase ? symbol.ToUpperInvariant() : symbol);
        }

        /// <summary>
        /// Get currencies from exchange market symbol. This method will call GetMarketSymbolsMetadataAsync if no MarketSymbolSeparator is defined for the exchange.
        /// </summary>
        /// <param name="marketSymbol">Market symbol</param>
        /// <returns>Base and quote currency</returns>
        public virtual async Task<(string baseCurrency, string quoteCurrency)> ExchangeMarketSymbolToCurrenciesAsync(string marketSymbol)
        {
            marketSymbol.ThrowIfNullOrWhitespace(nameof(marketSymbol));

            // no separator logic...
            if (string.IsNullOrWhiteSpace(MarketSymbolSeparator))
            {
                // we must look it up via metadata, most often this call will be cached and fast
                ExchangeMarket marketSymbolMetadata = await GetExchangeMarketFromCacheAsync(marketSymbol);
                if (marketSymbolMetadata == null)
                {
                    throw new InvalidDataException($"No market symbol metadata returned or unable to find symbol metadata for {marketSymbol}");
                }
                return (marketSymbolMetadata.BaseCurrency, marketSymbolMetadata.QuoteCurrency);
            }

            // default behavior with separator
            return OnSplitMarketSymbolToCurrencies(marketSymbol);
        }

        /// <summary>
        /// Convert a global symbol into an exchange symbol, which will potentially be different from other exchanges.
        /// </summary>
        /// <param name="marketSymbol">Global market symbol</param>
        /// <returns>Exchange market symbol</returns>
        public virtual Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
        {
            if (string.IsNullOrWhiteSpace(marketSymbol))
            {
                throw new ArgumentException("Market symbol must be non null and non empty");
            }
            int pos = marketSymbol.IndexOf(GlobalMarketSymbolSeparator);
            if (pos < 0)
            {
                throw new ArgumentException($"Market symbol {marketSymbol} is missing the global symbol separator '{GlobalMarketSymbolSeparator}'");
            }
            if (MarketSymbolIsReversed == false)
            {
                marketSymbol = GlobalCurrencyToExchangeCurrency(marketSymbol.Substring(0, pos)) + MarketSymbolSeparator + GlobalCurrencyToExchangeCurrency(marketSymbol.Substring(pos + 1));
            }
            else
            {
                marketSymbol = GlobalCurrencyToExchangeCurrency(marketSymbol.Substring(pos + 1)) + MarketSymbolSeparator + GlobalCurrencyToExchangeCurrency(marketSymbol.Substring(0, pos));
            }
            return Task.FromResult(MarketSymbolIsUppercase ? marketSymbol.ToUpperInvariant() : marketSymbol.ToLowerInvariant());
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
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetCurrenciesAsync(), nameof(GetCurrenciesAsync));
        }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public virtual async Task<IEnumerable<string>> GetMarketSymbolsAsync()
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => (await OnGetMarketSymbolsAsync()).ToArray(), nameof(GetMarketSymbolsAsync));
        }

        /// <summary>
        /// Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        public virtual async Task<IEnumerable<ExchangeMarket>> GetMarketSymbolsMetadataAsync()
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => (await OnGetMarketSymbolsMetadataAsync()).ToArray(), nameof(GetMarketSymbolsMetadataAsync));
        }

        /// <summary>
        /// Gets the exchange market from this exchange's SymbolsMetadata cache. This will make a network request if needed to retrieve fresh markets from the exchange using GetSymbolsMetadataAsync().
        /// Please note that sending a symbol that is not found over and over will result in many network requests. Only send symbols that you are confident exist on the exchange.
        /// </summary>
        /// <param name="marketSymbol">The market symbol. Ex. ADA/BTC. This is assumed to be normalized and already correct for the exchange.</param>
        /// <returns>The ExchangeMarket or null if it doesn't exist in the cache or there was an error</returns>
        public virtual async Task<ExchangeMarket?> GetExchangeMarketFromCacheAsync(string marketSymbol)
        {
            try
            {
                // *NOTE*: custom caching, do not wrap in CacheMethodCall...
                // *NOTE*: vulnerability exists where if spammed with not found symbols, lots of network calls will happen, stalling the application
                // TODO: Add not found dictionary, or some mechanism to mitigate this risk
                // not sure if this is needed, but adding it just in case
                await new SynchronizationContextRemover();
                Dictionary<string, ExchangeMarket> lookup = await this.GetExchangeMarketDictionaryFromCacheAsync();
                if (lookup != null && lookup.TryGetValue(marketSymbol, out ExchangeMarket market))
                {
                    return market;
                }

                // try again with a fresh request
                Cache.Remove(nameof(GetMarketSymbolsMetadataAsync));
                Cache.Remove(nameof(ExchangeAPIExtensions.GetExchangeMarketDictionaryFromCacheAsync));
                lookup = await this.GetExchangeMarketDictionaryFromCacheAsync();
                if (lookup != null && lookup.TryGetValue(marketSymbol, out market))
                {
                    return market;
                }
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
        /// <param name="marketSymbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public virtual async Task<ExchangeTicker> GetTickerAsync(string marketSymbol)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetTickerAsync(marketSymbol), nameof(GetTickerAsync), nameof(marketSymbol), marketSymbol);
        }

        /// <summary>
        /// Get all tickers in one request. If the exchange does not support this, a ticker will be requested for each symbol.
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public virtual async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync()
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetTickersAsync(), nameof(GetTickersAsync));
        }

        /// <summary>
        /// Get exchange order book
        /// </summary>
        /// <param name="marketSymbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public virtual async Task<ExchangeOrderBook> GetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetOrderBookAsync(marketSymbol, maxCount), nameof(GetOrderBookAsync), nameof(marketSymbol), marketSymbol, nameof(maxCount), maxCount);
        }

        /// <summary>
        /// Get all exchange order book symbols in one request. If the exchange does not support this, an order book will be requested for each symbol. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public virtual async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooksAsync(int maxCount = 100)
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetOrderBooksAsync(maxCount), nameof(GetOrderBooksAsync), nameof(maxCount), maxCount);
        }

        /// <summary>
        /// Get historical trades for the exchange. TODO: Change to async enumerator when available.
        /// </summary>
        /// <param name="callback">Callback for each set of trades. Return false to stop getting trades immediately.</param>
        /// <param name="marketSymbol">Symbol to get historical data for</param>
        /// <param name="startDate">Optional UTC start date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        /// <param name="endDate">Optional UTC end date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        public virtual async Task GetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            // *NOTE*: Do not wrap in CacheMethodCall, uses a callback with custom queries, not easy to cache
            await new SynchronizationContextRemover();
            await OnGetHistoricalTradesAsync(callback, NormalizeMarketSymbol(marketSymbol), startDate, endDate, limit);
        }

        /// <summary>
        /// Get recent trades on the exchange - the default implementation simply calls GetHistoricalTrades with a null sinceDateTime.
        /// </summary>
        /// <param name="marketSymbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all recent trades</returns>
        public virtual async Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string marketSymbol, int? limit = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetRecentTradesAsync(marketSymbol, limit), nameof(GetRecentTradesAsync), nameof(marketSymbol), marketSymbol, nameof(limit), limit);
			//return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetRecentTradesAsync(marketSymbol), nameof(GetRecentTradesAsync), nameof(marketSymbol), marketSymbol);
        }

        /// <summary>
        /// Gets the address to deposit to and applicable details.
        /// </summary>
        /// <param name="currency">Currency to get address for.</param>
        /// <param name="forceRegenerate">Regenerate the address</param>
        /// <returns>Deposit address details (including tag if applicable, such as XRP)</returns>
        public virtual async Task<ExchangeDepositDetails> GetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            if (forceRegenerate)
            {
                // force regenetate, do not cache
                return await OnGetDepositAddressAsync(currency, forceRegenerate);
            }
            else
            {
                return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetDepositAddressAsync(currency, forceRegenerate), nameof(GetDepositAddressAsync), nameof(currency), currency);
            }
        }

        /// <summary>
        /// Gets the deposit history for a symbol
        /// </summary>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        public virtual async Task<IEnumerable<ExchangeTransaction>> GetDepositHistoryAsync(string currency)
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetDepositHistoryAsync(currency), nameof(GetDepositHistoryAsync), nameof(currency), currency);
        }

        /// <summary>
        /// Get candles (open, high, low, close)
        /// </summary>
        /// <param name="marketSymbol">Symbol to get candles for</param>
        /// <param name="periodSeconds">Period in seconds to get candles for. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <param name="startDate">Optional start date to get candles for</param>
        /// <param name="endDate">Optional end date to get candles for</param>
        /// <param name="limit">Max results, can be used instead of startDate and endDate if desired</param>
        /// <returns>Candles</returns>
        public virtual async Task<IEnumerable<MarketCandle>> GetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetCandlesAsync(marketSymbol, periodSeconds, startDate, endDate, limit), nameof(GetCandlesAsync),
                nameof(marketSymbol), marketSymbol, nameof(periodSeconds), periodSeconds, nameof(startDate), startDate, nameof(endDate), endDate, nameof(limit), limit);
        }

        /// <summary>
        /// Get total amounts, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts</returns>
        public virtual async Task<Dictionary<string, decimal>> GetAmountsAsync()
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => (await OnGetAmountsAsync()), nameof(GetAmountsAsync));
        }

        /// <summary>
        /// Get fees
        /// </summary>
        /// <returns>The customer trading fees</returns>
        public virtual async Task<Dictionary<string, decimal>> GetFeesAync()
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetFeesAsync(), nameof(GetFeesAync));
        }

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual async Task<Dictionary<string, decimal>> GetAmountsAvailableToTradeAsync()
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetAmountsAvailableToTradeAsync(), nameof(GetAmountsAvailableToTradeAsync));
        }

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="order">The order request</param>
        /// <returns>Result</returns>
        public virtual async Task<ExchangeOrderResult> PlaceOrderAsync(ExchangeOrderRequest order)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            order.MarketSymbol = NormalizeMarketSymbol(order.MarketSymbol);
            return await OnPlaceOrderAsync(order);
        }

        /// <summary>
        /// Place bulk orders
        /// </summary>
        /// <param name="orders">Order requests</param>f
        /// <returns>Order results, each result matches up with each order in index</returns>
        public virtual async Task<ExchangeOrderResult[]> PlaceOrdersAsync(params ExchangeOrderRequest[] orders)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            foreach (ExchangeOrderRequest request in orders)
            {
                request.MarketSymbol = NormalizeMarketSymbol(request.MarketSymbol);
            }
            return await OnPlaceOrdersAsync(orders);
        }

        /// <summary>
        /// Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <param name="marketSymbol">Symbol of order (most exchanges do not require this)</param>
        /// <returns>Order details</returns>
        public virtual async Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId, string? marketSymbol = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetOrderDetailsAsync(orderId, marketSymbol), nameof(GetOrderDetailsAsync), nameof(orderId), orderId, nameof(marketSymbol), marketSymbol);
        }

        /// <summary>
        /// Get the details of all open orders
        /// </summary>
        /// <param name="marketSymbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public virtual async Task<IEnumerable<ExchangeOrderResult>> GetOpenOrderDetailsAsync(string? marketSymbol = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetOpenOrderDetailsAsync(marketSymbol), nameof(GetOpenOrderDetailsAsync), nameof(marketSymbol), marketSymbol);
        }

        /// <summary>
        /// Get the details of all completed orders
        /// </summary>
        /// <param name="marketSymbol">Symbol to get completed orders for or null for all</param>
        /// <param name="afterDate">Only returns orders on or after the specified date/time</param>
        /// <returns>All completed order details for the specified symbol, or all if null symbol</returns>
        public virtual async Task<IEnumerable<ExchangeOrderResult>> GetCompletedOrderDetailsAsync(string? marketSymbol = null, DateTime? afterDate = null)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => (await OnGetCompletedOrderDetailsAsync(marketSymbol, afterDate)).ToArray(), nameof(GetCompletedOrderDetailsAsync),
                nameof(marketSymbol), marketSymbol, nameof(afterDate), afterDate);
        }

        /// <summary>
        /// Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        /// <param name="marketSymbol">Symbol of order (most exchanges do not require this)</param>
        public virtual async Task CancelOrderAsync(string orderId, string? marketSymbol = null)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            await OnCancelOrderAsync(orderId, NormalizeMarketSymbol(marketSymbol));
        }

        /// <summary>
        /// Asynchronous withdraws request.
        /// </summary>
        /// <param name="withdrawalRequest">The withdrawal request.</param>
        public virtual async Task<ExchangeWithdrawalResponse> WithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            withdrawalRequest.Currency = NormalizeMarketSymbol(withdrawalRequest.Currency);
            return await OnWithdrawAsync(withdrawalRequest);
        }

        /// <summary>
        /// Gets the withdraw history for a symbol
        /// </summary>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        public virtual async Task<IEnumerable<ExchangeTransaction>> GetWithdrawHistoryAsync(string currency)
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetWithdrawHistoryAsync(currency), nameof(GetWithdrawHistoryAsync), nameof(currency), currency);
        }

        /// <summary>
        /// Get margin amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <param name="includeZeroBalances">Include currencies with zero balance in return value</param>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual async Task<Dictionary<string, decimal>> GetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances = false)
        {
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetMarginAmountsAvailableToTradeAsync(includeZeroBalances),
                nameof(GetMarginAmountsAvailableToTradeAsync), nameof(includeZeroBalances), includeZeroBalances);
        }

        /// <summary>
        /// Get open margin position
        /// </summary>
        /// <param name="marketSymbol">Symbol</param>
        /// <returns>Open margin position result</returns>
        public virtual async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string marketSymbol)
        {
            marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetOpenPositionAsync(marketSymbol), nameof(GetOpenPositionAsync), nameof(marketSymbol), marketSymbol);
        }

        /// <summary>
        /// Close a margin position
        /// </summary>
        /// <param name="marketSymbol">Symbol</param>
        /// <returns>Close margin position result</returns>
        public virtual async Task<ExchangeCloseMarginPositionResult> CloseMarginPositionAsync(string marketSymbol)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            return await OnCloseMarginPositionAsync(NormalizeMarketSymbol(marketSymbol));
        }



		#endregion REST API

		#region Web Socket API

		/// <summary>
		/// Get all tickers via web socket
		/// </summary>
		/// <param name="callback">Callback</param>
		/// <param name="symbols"></param>
		/// <returns>Web socket, call Dispose to close</returns>
		public virtual Task<IWebSocket> GetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] symbols)
        {
            callback.ThrowIfNull(nameof(callback), "Callback must not be null");
            return OnGetTickersWebSocketAsync(callback, symbols);
        }

        /// <summary>
        /// Get information about trades via web socket
        /// </summary>
        /// <param name="callback">Callback (symbol and trade)</param>
        /// <param name="marketSymbols">Market Symbols</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual Task<IWebSocket> GetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
            callback.ThrowIfNull(nameof(callback), "Callback must not be null");
            return OnGetTradesWebSocketAsync(callback, marketSymbols);
        }

        /// <summary>
        /// Get delta order book bids and asks via web socket. Only the deltas are returned for each callback. To manage a full order book, use ExchangeAPIExtensions.GetOrderBookWebSocket.
        /// </summary>
        /// <param name="callback">Callback of symbol, order book</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <param name="marketSymbols">Market symbols or null/empty for all of them (if supported)</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual Task<IWebSocket> GetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            callback.ThrowIfNull(nameof(callback), "Callback must not be null");
            return OnGetDeltaOrderBookWebSocketAsync(callback, maxCount, marketSymbols);
        }

        /// <summary>
        /// Get the details of all changed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual Task<IWebSocket> GetOrderDetailsWebSocketAsync(Action<ExchangeOrderResult> callback)
        {
            callback.ThrowIfNull(nameof(callback), "Callback must not be null");
            return OnGetOrderDetailsWebSocketAsync(callback);
        }

        /// <summary>
        /// Get the details of all completed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual Task<IWebSocket> GetCompletedOrderDetailsWebSocketAsync(Action<ExchangeOrderResult> callback)
        {
            callback.ThrowIfNull(nameof(callback), "Callback must not be null");
            return OnGetCompletedOrderDetailsWebSocketAsync(callback);
        }

        /// <summary>
        /// Get user detail over web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <param name="listenKey">Listen key</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public virtual Task<IWebSocket> GetUserDataWebSocketAsync(Action<object> callback, string listenKey)
        {
	        callback.ThrowIfNull(nameof(callback), "Callback must not be null");
	        return OnUserDataWebSocketAsync(callback, listenKey);
        }
		#endregion Web Socket API
	}
}
