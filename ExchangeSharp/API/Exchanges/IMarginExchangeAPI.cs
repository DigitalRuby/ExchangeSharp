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

        /// <summary>
        /// Place a margin order
        /// </summary>
        /// <param name="order">Order request</param>
        /// <returns>Order result and message string if any</returns>
        ExchangeOrderResult PlaceMarginOrder(ExchangeOrderRequest order);

        /// <summary>
        /// ASYNC - Place a margin order
        /// </summary>
        /// <param name="order">Order request</param>
        /// <returns>Order result and message string if any</returns>
        Task<ExchangeOrderResult> PlaceMarginOrderAsync(ExchangeOrderRequest order);

        /// <summary>
        /// Get open margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Open margin position result</returns>
        ExchangeMarginPositionResult GetOpenPosition(string symbol);

        /// <summary>
        /// ASYNC - Get open margin position
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Open margin position result</returns>
        Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string symbol);
    }
}