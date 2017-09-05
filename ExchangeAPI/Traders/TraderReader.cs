/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public interface ITradeReader
    {
        /// <summary>
        /// Read the next trade
        /// </summary>
        /// <param name="trade">Trade to read</param>
        /// <returns>false if no more tickers left, true otherwise</returns>
        bool ReadNextTrade(ref Trade trade);
    }

    /// <summary>
    /// Trader that reads from memory
    /// </summary>
    public sealed unsafe class TradeReaderMemory : ITradeReader, IDisposable
    {
        private byte[] tickerData;
        private Trade* tickers;
        private Trade* tickersStart;
        private Trade* tickersEnd;
        private int tickersCount;
        private GCHandle tickersHandle;
        private bool ownsHandle;

        private TradeReaderMemory() { }

        public TradeReaderMemory(byte[] tickerData)
        {
            this.tickerData = tickerData;
            tickersHandle = GCHandle.Alloc(tickerData, GCHandleType.Pinned);
            this.tickers = (Trade*)tickersHandle.AddrOfPinnedObject();
            this.tickersStart = this.tickers;
            this.tickersCount = tickerData.Length / 16;
            this.tickersEnd = this.tickers + this.tickersCount;
            this.ownsHandle = true;
        }

        public void Dispose()
        {
            if (ownsHandle)
            {
                tickersHandle.Free();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadNextTrade(ref Trade ticker)
        {
            if (tickers == tickersEnd)
            {
                ticker.Ticks = 0;
                return false;
            }
            ticker = *tickers++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ITradeReader Clone()
        {
            return new TradeReaderMemory
            {
                tickerData = tickerData,
                tickers = tickersStart,
                tickersStart = tickersStart,
                tickersEnd = tickersEnd,
                tickersHandle = tickersHandle,
                tickersCount = tickersCount
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            tickers = tickersStart;
        }

        public Trade* TickersPtr { get { return tickers; } }
        public int TickersCount {  get { return tickersCount; } }
    }
}
