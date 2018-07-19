/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Base class for all exchange API
    /// </summary>
    public abstract class ExchangeAPI : BaseAPI, IExchangeAPI
    {
        #region Constants

        /// <summary>
        /// Separator for global symbols
        /// </summary>
        public const char GlobalSymbolSeparator = '-';

        #endregion Constants

        #region Private methods

        private static readonly Dictionary<string, IExchangeAPI> apis = new Dictionary<string, IExchangeAPI>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ExchangeMarket> exchangeMarkets = new Dictionary<string, ExchangeMarket>();
        private readonly SemaphoreSlim exchangeMarketsSemaphore = new SemaphoreSlim(1, 1);
        private bool disposed;

        /// <summary>
        /// Call GetSymbolsMetadataAsync if exchangeMarkets is empty and store the results.
        /// </summary>
        /// <param name="forceRefresh">True to force a network request, false to use existing cache data if it exists</param>
        private async Task PopulateExchangeMarketsAsync(bool forceRefresh)
        {
            // Get the exchange markets if we haven't gotten them yet.
            if (forceRefresh || exchangeMarkets.Count == 0)
            {
                await exchangeMarketsSemaphore.WaitAsync();
                try
                {
                    if (forceRefresh || exchangeMarkets.Count == 0)
                    {
                        foreach (ExchangeMarket market in await GetSymbolsMetadataAsync())
                        {
                            exchangeMarkets[market.MarketName] = market;
                        }
                    }
                }
                finally
                {
                    exchangeMarketsSemaphore.Release();
                }
            }
        }

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
        protected virtual IWebSocket OnGetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback) => throw new NotImplementedException();

        protected class HistoricalTradeHelperState
        {
            private ExchangeAPI api;

            public Func<IEnumerable<ExchangeTrade>, bool> Callback { get; set; }
            public string Symbol { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public string Url { get; set; } // url with format [symbol], {0} = start timestamp, {1} = end timestamp
            public int DelayMilliseconds { get; set; } = 1000;
            public TimeSpan BlockTime { get; set; } = TimeSpan.FromHours(1.0); // how much time to move for each block of data, default 1 hour
            public bool MillisecondGranularity { get; set; } = true;
            public Func<DateTime, string> TimestampFunction { get; set; } // change date time to a url timestamp, use TimestampFunction or UrlFunction
            public Func<HistoricalTradeHelperState, string> UrlFunction { get; set; } // allows returning a custom url, use TimestampFunction or UrlFunction
            public Func<JToken, ExchangeTrade> ParseFunction { get; set; }
            public bool DirectionIsBackwards { get; set; } = true; // some exchanges support going from most recent to oldest, but others, like Gemini must go from oldest to newest

            public HistoricalTradeHelperState(ExchangeAPI api)
            {
                this.api = api;
            }

            public async Task ProcessHistoricalTrades()
            {
                if (Callback == null)
                {
                    throw new ArgumentException("Missing required parameter", nameof(Callback));
                }
                else if (TimestampFunction == null && UrlFunction == null)
                {
                    throw new ArgumentException("Missing required parameters", nameof(TimestampFunction) + "," + nameof(UrlFunction));
                }
                else if (ParseFunction == null)
                {
                    throw new ArgumentException("Missing required parameter", nameof(ParseFunction));
                }
                else if (string.IsNullOrWhiteSpace(Url))
                {
                    throw new ArgumentException("Missing required parameter", nameof(Url));
                }

                Symbol = api.NormalizeSymbol(Symbol);
                string url;
                Url = Url.Replace("[symbol]", Symbol);
                List<ExchangeTrade> trades = new List<ExchangeTrade>();
                ExchangeTrade trade;
                EndDate = (EndDate ?? DateTime.UtcNow);
                StartDate = (StartDate ?? EndDate.Value.Subtract(BlockTime));
                string startTimestamp;
                string endTimestamp;
                HashSet<long> previousTrades = new HashSet<long>();
                HashSet<long> tempTradeIds = new HashSet<long>();
                HashSet<long> tmpIds;
                SetDates(out DateTime startDateMoving, out DateTime endDateMoving);

                while (true)
                {
                    // format url and make request
                    if (TimestampFunction != null)
                    {
                        startTimestamp = TimestampFunction(startDateMoving);
                        endTimestamp = TimestampFunction(endDateMoving);
                        url = string.Format(Url, startTimestamp, endTimestamp);
                    }
                    else if (UrlFunction != null)
                    {
                        url = UrlFunction(this);
                    }
                    else
                    {
                        throw new InvalidOperationException("TimestampFunction or UrlFunction must be specified");
                    }
                    JToken obj = await api.MakeJsonRequestAsync<JToken>(url);

                    // don't add this temp trade as it may be outside of the date/time range
                    tempTradeIds.Clear();
                    foreach (JToken token in obj)
                    {
                        trade = ParseFunction(token);
                        if (!previousTrades.Contains(trade.Id) && trade.Timestamp >= StartDate.Value && trade.Timestamp <= EndDate.Value)
                        {
                            trades.Add(trade);
                        }
                        if (trade.Id != 0)
                        {
                            tempTradeIds.Add(trade.Id);
                        }
                    }
                    previousTrades.Clear();
                    tmpIds = previousTrades;
                    previousTrades = tempTradeIds;
                    tempTradeIds = previousTrades;

                    // set dates to next block
                    if (trades.Count == 0)
                    {
                        if (DirectionIsBackwards)
                        {
                            // no trades found, move the whole block back
                            endDateMoving = startDateMoving.Subtract(BlockTime);
                        }
                        else
                        {
                            // no trades found, move the whole block forward
                            startDateMoving = endDateMoving.Add(BlockTime);
                        }
                    }
                    else
                    {
                        // sort trades in descending order and callback
                        if (DirectionIsBackwards)
                        {
                            trades.Sort((t1, t2) => t2.Timestamp.CompareTo(t1.Timestamp));
                        }
                        else
                        {
                            trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                        }
                        if (!Callback(trades))
                        {
                            break;
                        }

                        trade = trades[trades.Count - 1];
                        if (DirectionIsBackwards)
                        {
                            // set end date to the date of the earliest trade of the block, use for next request
                            if (MillisecondGranularity)
                            {
                                endDateMoving = trade.Timestamp.AddMilliseconds(-1.0);
                            }
                            else
                            {
                                endDateMoving = trade.Timestamp.AddSeconds(-1.0);
                            }
                            startDateMoving = endDateMoving.Subtract(BlockTime);
                        }
                        else
                        {
                            // set start date to the date of the latest trade of the block, use for next request
                            if (MillisecondGranularity)
                            {
                                startDateMoving = trade.Timestamp.AddMilliseconds(1.0);
                            }
                            else
                            {
                                startDateMoving = trade.Timestamp.AddSeconds(1.0);
                            }
                            endDateMoving = startDateMoving.Add(BlockTime);
                        }
                        trades.Clear();
                    }
                    // check for exit conditions
                    if (DirectionIsBackwards)
                    {
                        if (endDateMoving < StartDate.Value)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (startDateMoving > EndDate.Value)
                        {
                            break;
                        }
                    }
                    ClampDates(ref startDateMoving, ref endDateMoving);
                    await Task.Delay(DelayMilliseconds);
                }
            }

            private void SetDates(out DateTime startDateMoving, out DateTime endDateMoving)
            {
                if (DirectionIsBackwards)
                {
                    endDateMoving = EndDate.Value;
                    startDateMoving = endDateMoving.Subtract(BlockTime);
                }
                else
                {
                    startDateMoving = StartDate.Value;
                    endDateMoving = startDateMoving.Add(BlockTime);
                }
                ClampDates(ref startDateMoving, ref endDateMoving);
            }

            private void ClampDates(ref DateTime startDateMoving, ref DateTime endDateMoving)
            {
                if (DirectionIsBackwards)
                {
                    if (startDateMoving < StartDate.Value)
                    {
                        startDateMoving = StartDate.Value;
                    }
                }
                else
                {
                    if (endDateMoving > EndDate.Value)
                    {
                        endDateMoving = EndDate.Value;
                    }
                }
            }
        };

        #endregion API implementation

        #region Protected methods

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
        /// Override to dispose of resources when the exchange is disposed
        /// </summary>
        protected virtual void OnDispose() { }

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

        #endregion Protected methods

        #region Other

        /// <summary>
        /// Static constructor
        /// </summary>
        static ExchangeAPI()
        {
            foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI)) && !type.IsAbstract))
            {
                ExchangeAPI api = Activator.CreateInstance(type) as ExchangeAPI;
                apis[api.Name] = api;
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

        #endregion Other

        #region REST API

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
        /// Gets the exchange market from this exchange's SymbolsMetadata cache. This will make a network request if needed to retrieve fresh markets from the exchange using GetSymbolsMetadataAsync().
        /// Please note that sending a symbol that is not found over and over will result in many network requests. Only send symbols that you are confident exist on the exchange.
        /// </summary>
        /// <param name="symbol">The symbol. Ex. ADA/BTC. This is assumed to be normalized and already correct for the exchange.</param>
        /// <returns>The ExchangeMarket or null if it doesn't exist in the cache or there was an error</returns>
        public ExchangeMarket GetExchangeMarketFromCache(string symbol) => GetExchangeMarketFromCacheAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Gets the exchange market from this exchange's SymbolsMetadata cache. This will make a network request if needed to retrieve fresh markets from the exchange using GetSymbolsMetadataAsync().
        /// Please note that sending a symbol that is not found over and over will result in many network requests. Only send symbols that you are confident exist on the exchange.
        /// </summary>
        /// <param name="symbol">The symbol. Ex. ADA/BTC. This is assumed to be normalized and already correct for the exchange.</param>
        /// <returns>The ExchangeMarket or null if it doesn't exist in the cache or there was an error</returns>
        public async Task<ExchangeMarket> GetExchangeMarketFromCacheAsync(string symbol)
        {
            try
            {
                // not sure if this is needed, but adding it just in case
                await new SynchronizationContextRemover();
                await PopulateExchangeMarketsAsync(false);
                exchangeMarkets.TryGetValue(symbol, out ExchangeMarket market);
                if (market == null)
                {
                    // try again with a fresh request, every symbol *should* be in the response from PopulateExchangeMarketsAsync
                    await PopulateExchangeMarketsAsync(true);

                    // try again to retrieve from dictionary
                    exchangeMarkets.TryGetValue(symbol, out market);
                }
                return market;
            }
            catch
            {
                // TODO: Report the error somehow, for now a failed network request will just return null symbol which fill force the caller to use default handling
            }
            return null;
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
        /// <param name="startDate">Optional UTC start date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        /// <param name="endDate">Optional UTC end date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        public void GetHistoricalTrades(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null) =>
            GetHistoricalTradesAsync(callback, symbol, startDate, endDate).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get historical trades for the exchange
        /// </summary>
        /// <param name="callback">Callback for each set of trades. Return false to stop getting trades immediately.</param>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="startDate">Optional UTC start date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        /// <param name="endDate">Optional UTC end date time to start getting the historical data at, null for the most recent data. Not all exchanges support this.</param>
        public async Task GetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            await new SynchronizationContextRemover();
            await OnGetHistoricalTradesAsync(callback, symbol, startDate, endDate);
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
        ///  Get fees
        /// </summary>
        /// <returns>The customer trading fees</returns>
        public Dictionary<string, decimal> GetFees() => GetFeesAync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get fees
        /// </summary>
        /// <returns>The customer trading fees</returns>
        public async Task<Dictionary<string, decimal>> GetFeesAync()
        {
            await new SynchronizationContextRemover();
            return await OnGetFeesAsync();
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

        /// <summary>
        /// Place a limit order by first querying the order book and then placing the order for a threshold below the bid or above the ask that would fully fulfill the amount.
        /// The order book is scanned until an amount of bids or asks that will fulfill the order is found and then the order is placed at the lowest bid or highest ask price multiplied
        /// by priceThreshold.
        /// </summary>
        /// <param name="symbol">Symbol to sell</param>
        /// <param name="amount">Amount to sell</param>
        /// <param name="isBuy">True for buy, false for sell</param>
        /// <param name="orderBookCount">Amount of bids/asks to request in the order book</param>
        /// <param name="priceThreshold">Threshold below the lowest bid or above the highest ask to set the limit order price at. For buys, this is converted to 1 / priceThreshold.
        /// This can be set to 0 if you want to set the price like a market order.</param>
        /// <param name="thresholdToAbort">If the lowest bid/highest ask price divided by the highest bid/lowest ask price is below this threshold, throw an exception.
        /// This ensures that your order does not buy or sell at an extreme margin.</param>
        /// <param name="abortIfOrderBookTooSmall">Whether to abort if the order book does not have enough bids or ask amounts to fulfill the order.</param>
        /// <returns>Order result</returns>
        public ExchangeOrderResult PlaceSafeMarketOrder(string symbol, decimal amount, bool isBuy, int orderBookCount = 100, decimal priceThreshold = 0.9m, decimal thresholdToAbort = 0.75m)
            => PlaceSafeMarketOrderAsync(symbol, amount, isBuy, orderBookCount, priceThreshold, thresholdToAbort).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Place a limit order by first querying the order book and then placing the order for a threshold below the bid or above the ask that would fully fulfill the amount.
        /// The order book is scanned until an amount of bids or asks that will fulfill the order is found and then the order is placed at the lowest bid or highest ask price multiplied
        /// by priceThreshold.
        /// </summary>
        /// <param name="symbol">Symbol to sell</param>
        /// <param name="amount">Amount to sell</param>
        /// <param name="isBuy">True for buy, false for sell</param>
        /// <param name="orderBookCount">Amount of bids/asks to request in the order book</param>
        /// <param name="priceThreshold">Threshold below the lowest bid or above the highest ask to set the limit order price at. For buys, this is converted to 1 / priceThreshold.
        /// This can be set to 0 if you want to set the price like a market order.</param>
        /// <param name="thresholdToAbort">If the lowest bid/highest ask price divided by the highest bid/lowest ask price is below this threshold, throw an exception.
        /// This ensures that your order does not buy or sell at an extreme margin.</param>
        /// <param name="abortIfOrderBookTooSmall">Whether to abort if the order book does not have enough bids or ask amounts to fulfill the order.</param>
        /// <returns>Order result</returns>
        public async Task<ExchangeOrderResult> PlaceSafeMarketOrderAsync(string symbol, decimal amount, bool isBuy, int orderBookCount = 100, decimal priceThreshold = 0.9m,
            decimal thresholdToAbort = 0.75m, bool abortIfOrderBookTooSmall = false)
        {
            if (priceThreshold > 0.9m)
            {
                throw new APIException("You cannot specify a price threshold above 0.9m, otherwise there is a chance your order will never be fulfilled. For buys, this is " +
                    "converted to 1.0m / priceThreshold, so always specify the value below 0.9m");
            }
            else if (priceThreshold <= 0m)
            {
                priceThreshold = 1m;
            }
            else if (isBuy && priceThreshold > 0m)
            {
                priceThreshold = 1.0m / priceThreshold;
            }
            ExchangeOrderBook book = await GetOrderBookAsync(symbol, orderBookCount);
            if (book == null || (isBuy && book.Asks.Count == 0) || (!isBuy && book.Bids.Count == 0))
            {
                throw new APIException($"Error getting order book for {symbol}");
            }
            decimal counter = 0m;
            decimal highPrice = decimal.MinValue;
            decimal lowPrice = decimal.MaxValue;
            if (isBuy)
            {
                foreach (ExchangeOrderPrice ask in book.Asks.Values)
                {
                    counter += ask.Amount;
                    highPrice = Math.Max(highPrice, ask.Price);
                    lowPrice = Math.Min(lowPrice, ask.Price);
                    if (counter >= amount)
                    {
                        break;
                    }
                }
            }
            else
            {
                foreach (ExchangeOrderPrice bid in book.Bids.Values)
                {
                    counter += bid.Amount;
                    highPrice = Math.Max(highPrice, bid.Price);
                    lowPrice = Math.Min(lowPrice, bid.Price);
                    if (counter >= amount)
                    {
                        break;
                    }
                }
            }
            if (abortIfOrderBookTooSmall && counter < amount)
            {
                throw new APIException($"{(isBuy ? "Buy" : "Sell") } order for {symbol} and amount {amount} cannot be fulfilled because the order book is too thin.");
            }
            else if (lowPrice / highPrice < thresholdToAbort)
            {
                throw new APIException($"{(isBuy ? "Buy" : "Sell")} order for {symbol} and amount {amount} would place for a price below threshold of {thresholdToAbort}, aborting.");
            }
            ExchangeOrderRequest request = new ExchangeOrderRequest
            {
                Amount = amount,
                OrderType = OrderType.Limit,
                Price = CryptoUtility.RoundAmount((isBuy ? highPrice : lowPrice) * priceThreshold),
                ShouldRoundAmount = true,
                Symbol = symbol
            };
            ExchangeOrderResult result = await PlaceOrderAsync(request);

            // wait about 10 seconds until the order is fulfilled
            int i = 0;
            const int maxTries = 20; // 500 ms for each try
            for (; i < maxTries; i++)
            {
                await System.Threading.Tasks.Task.Delay(500);
                result = await GetOrderDetailsAsync(result.OrderId, symbol);
                switch (result.Result)
                {
                    case ExchangeAPIOrderResult.Filled:
                    case ExchangeAPIOrderResult.Canceled:
                    case ExchangeAPIOrderResult.Error:
                        break;
                }
            }

            if (i == maxTries)
            {
                throw new APIException($"{(isBuy ? "Buy" : "Sell")} order for {symbol} and amount {amount} timed out and may not have been fulfilled");
            }

            return result;
        }

        /// <summary>
        /// Get margin amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public Dictionary<string, decimal> GetMarginAmountsAvailableToTrade() => GetMarginAmountsAvailableToTradeAsync().GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get margin amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public async Task<Dictionary<string, decimal>> GetMarginAmountsAvailableToTradeAsync()
        {
            await new SynchronizationContextRemover();
            return await OnGetMarginAmountsAvailableToTradeAsync();
        }

        /// <summary>
        /// Get open margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Open margin position result</returns>
        public ExchangeMarginPositionResult GetOpenPosition(string symbol) => GetOpenPositionAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Get open margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Open margin position result</returns>
        public async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnGetOpenPositionAsync(symbol);
        }

        /// <summary>
        /// Close a margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Close margin position result</returns>
        public ExchangeCloseMarginPositionResult CloseMarginPosition(string symbol) => CloseMarginPositionAsync(symbol).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Close a margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Close margin position result</returns>
        public async Task<ExchangeCloseMarginPositionResult> CloseMarginPositionAsync(string symbol)
        {
            await new SynchronizationContextRemover();
            return await OnCloseMarginPositionAsync(symbol);
        }

        #endregion REST API

        #region Web Socket API

        /// <summary>
        /// Get all tickers via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public IWebSocket GetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback) => OnGetTickersWebSocket(callback);

        /// <summary>
        /// Get information about trades via web socket
        /// </summary>
        /// <param name="callback">Callback (symbol and trade)</param>
        /// <param name="symbols">Symbols</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public IWebSocket GetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols) => OnGetTradesWebSocket(callback, symbols);

        /// <summary>
        /// Get delta order book bids and asks via web socket. Only the deltas are returned for each callback. To manage a full order book, use ExchangeAPIExtensions.GetOrderBookWebSocket.
        /// </summary>
        /// <param name="callback">Callback of symbol, order book</param>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <param name="symbol">Ticker symbols or null/empty for all of them (if supported)</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public IWebSocket GetOrderBookDeltasWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols) => OnGetOrderBookDeltasWebSocket(callback, maxCount, symbols);

        /// <summary>
        /// Get the details of all completed orders via web socket
        /// </summary>
        /// <param name="callback">Callback</param>
        /// <returns>Web socket, call Dispose to close</returns>
        public IWebSocket GetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback) => OnGetCompletedOrderDetailsWebSocket(callback);

        #endregion Web Socket API
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
        /// BitMEX
        /// </summary>
        public const string BitMEX = "BitMEX";

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

        /// <summary>
        /// ZB.com
        /// </summary>
        public const string ZBcom = "ZB.com";
    }
}
