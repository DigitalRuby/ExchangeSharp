using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public abstract class MarginExchangeAPI : ExchangeAPI, IMarginExchangeAPI
    {
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
        /// Place a margin order
        /// </summary>
        /// <param name="order">The order request</param>
        /// <returns>Result</returns>
        public ExchangeOrderResult PlaceMarginOrder(ExchangeOrderRequest order) => PlaceMarginOrderAsync(order).GetAwaiter().GetResult();

        /// <summary>
        /// ASYNC - Place a margin order
        /// </summary>
        /// <param name="order">The order request</param>
        /// <returns>Result</returns>
        public async Task<ExchangeOrderResult> PlaceMarginOrderAsync(ExchangeOrderRequest order)
        {
            await new SynchronizationContextRemover();
            return await OnPlaceMarginOrderAsync(order);
        }

        protected virtual Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync() => throw new NotImplementedException();
        protected virtual Task<ExchangeOrderResult> OnPlaceMarginOrderAsync(ExchangeOrderRequest order) => throw new NotImplementedException();
    }
}
