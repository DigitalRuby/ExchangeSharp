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
    /// This shows all the methods that can be overriden when implementation a new exchange, along
    /// with all the fields that should be set in the constructor or static constructor if needed.
    /// </summary>
    public abstract partial class ExchangeAPI
    {
        /*
        protected virtual Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync();
        protected virtual Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> OnGetOrderBooksAsync(int maxCount = 100);
        protected virtual Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol);
        protected virtual Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync();
        protected virtual Task<IEnumerable<string>> OnGetSymbolsAsync();
        protected virtual Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync();
        protected virtual Task<ExchangeTicker> OnGetTickerAsync(string symbol);
        protected virtual Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100);
        protected virtual OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null);
        protected virtual Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false);
        protected virtual Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol);
        protected virtual Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null);
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAsync();
        protected virtual Task<Dictionary<string, decimal>> OnGetFeesAsync();
        protected virtual Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync();
        protected virtual Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order);
        protected virtual Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] order);
        protected virtual Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null);
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null);
        protected virtual Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null);
        protected virtual Task OnCancelOrderAsync(string orderId, string symbol = null);
        protected virtual Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest);
        protected virtual Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync();
        protected virtual Task<ExchangeMarginPositionResult> OnGetOpenPositionAsync(string symbol);
        protected virtual Task<ExchangeCloseMarginPositionResult> OnCloseMarginPositionAsync(string symbol);

        protected virtual Task<IWebSocket> OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers);
        protected virtual Task<IWebSocket> OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols);
        protected virtual Task<IWebSocket> OnGetDeltaOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] symbols);
        protected virtual Task<IWebSocket> OnGetOrderDetailsWebSocket(Action<ExchangeOrderResult> callback);
        protected virtual Task<IWebSocket> OnGetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback);

        // these generally do not need to be overriden unless your Exchange does something funny or does not use a symbol separator
        public virtual string NormalizeSymbol(string symbol);
        public virtual string ExchangeSymbolToGlobalSymbol(string symbol);
        public virtual string GlobalSymbolToExchangeSymbol(string symbol);
        public virtual string PeriodSecondsToString(int seconds);

        protected virtual void OnDispose();
        */

        /// <summary>
        /// Dictionary of key (exchange currency) and value (global currency). Add entries in static constructor.
        /// Some exchanges (Yobit for example) use odd names for some currencies like BCC for Bitcoin Cash.
        /// <example><![CDATA[ 
        /// ExchangeGlobalCurrencyReplacements[typeof(ExchangeYobitAPI)] = new KeyValuePair<string, string>[]
        /// {
        ///     new KeyValuePair<string, string>("BCC", "BCH")
        /// };
        /// ]]></example>
        /// </summary>
        protected static readonly Dictionary<Type, KeyValuePair<string, string>[]> ExchangeGlobalCurrencyReplacements = new Dictionary<Type, KeyValuePair<string, string>[]>();

        /// <summary>
        /// Separator for exchange symbol. If not a hyphen, set in constructor. This should be one character and is a string for convenience of concatenation.
        /// </summary>
        public string MarketSymbolSeparator { get; protected set; } = "-";

        /// <summary>
        /// Whether the symbol is reversed. Most exchanges do ETH-BTC, if your exchange does BTC-ETH, set to true in constructor.
        /// </summary>
        public bool MarketSymbolIsReversed { get; protected set; }

        /// <summary>
        /// Whether the symbol is uppercase. Most exchanges are true, but if your exchange is lowercase, set to false in constructor.
        /// </summary>
        public bool MarketSymbolIsUppercase { get; protected set; } = true;

        /// <summary>
        /// The type of web socket order book supported
        /// </summary>
        public WebSocketOrderBookType WebSocketOrderBookType { get; protected set; } = WebSocketOrderBookType.None;
    }

    // implement this and change the field name and value to the name of your exchange
    // public partial class ExchangeName { public const string MyNewExchangeName = "MyNewExchangeName"; }
}
