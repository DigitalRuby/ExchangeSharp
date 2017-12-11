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
    /// Information about an exchange
    /// </summary>
    public class ExchangeInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">Exchange API</param>
        /// <param name="symbol">The symbol to trade by default, can be null</param>
        public ExchangeInfo(IExchangeAPI api, string symbol = null)
        {
            API = api;
            Symbols = api.GetSymbols().ToArray();
            TradeInfo = new ExchangeTradeInfo(this, symbol);
        }

        /// <summary>
        /// Update the exchange info - get new trade info, etc.
        /// </summary>
        public void Update()
        {
            TradeInfo.Update();
        }

        /// <summary>
        /// API to interact with the exchange
        /// </summary>
        public IExchangeAPI API { get; private set; }

        /// <summary>
        /// User assigned identifier of the exchange, can be left at zero if not needed
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Symbols of the exchange
        /// </summary>
        public IReadOnlyCollection<string> Symbols { get; private set; }

        /// <summary>
        /// Latest trade info for the exchange
        /// </summary>
        public ExchangeTradeInfo TradeInfo { get; private set; }
    }
}
