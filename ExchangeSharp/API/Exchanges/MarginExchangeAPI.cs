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

        protected virtual Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync() => throw new NotImplementedException();
    }
}
