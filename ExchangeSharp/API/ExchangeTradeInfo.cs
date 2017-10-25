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
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Latest trade info for an exchange
    /// </summary>
    public class ExchangeTradeInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="info">Exchange info</param>
        /// <param name="symbol">The symbol to trade</param>
        public ExchangeTradeInfo(ExchangeInfo info, string symbol)
        {
            ExchangeInfo = info;
            Symbol = symbol;
        }

        /// <summary>
        /// Update the trade info via API
        /// </summary>
        public void Update()
        {
            Ticker = ExchangeInfo.API.GetTicker(Symbol);
            RecentTrades = ExchangeInfo.API.GetRecentTrades(Symbol).ToArray();
            if (RecentTrades.Length == 0)
            {
                Trade = new Trade();
            }
            else
            {
                Trade = new Trade { Amount = (float)RecentTrades[RecentTrades.Length - 1].Amount, Price = (float)RecentTrades[RecentTrades.Length - 1].Price, Ticks = (long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(RecentTrades[RecentTrades.Length - 1].Timestamp) };
            }
            Orders = ExchangeInfo.API.GetOrderBook(Symbol);
        }

        /// <summary>
        /// Exchange info
        /// </summary>
        public ExchangeInfo ExchangeInfo { get; private set; }

        /// <summary>
        /// Ticker for the exchange
        /// </summary>
        public ExchangeTicker Ticker { get; private set; }

        /// <summary>
        /// Recent trades in ascending order
        /// </summary>
        public ExchangeTrade[] RecentTrades { get; private set; }

        /// <summary>
        /// Pending orders on the exchange
        /// </summary>
        public ExchangeOrderBook Orders { get; private set; }

        /// <summary>
        /// The last trade made, allows setting to facilitate fast testing of traders based on price alone
        /// </summary>
        public Trade Trade { get; set; }

        /// <summary>
        /// The current symbol being traded
        /// </summary>
        public string Symbol { get; set; }
    }
}
