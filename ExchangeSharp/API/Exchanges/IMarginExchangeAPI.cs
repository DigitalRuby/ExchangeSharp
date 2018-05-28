using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public interface IMarginExchangeAPI : IExchangeAPI
    {
        /// <summary>
        /// Get margin amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade in margin account</returns>
        Dictionary<string, decimal> GetMarginAmountsAvailableToTrade();

        /// <summary>
        /// ASYNC - Get margin amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Dictionary of symbols and amounts available to trade in margin account</returns>
        Task<Dictionary<string, decimal>> GetMarginAmountsAvailableToTradeAsync();
    }
}