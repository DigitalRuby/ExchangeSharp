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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public class SimplePeakValleyTrader : Trader
    {
        public double AnchorPrice { get; private set; }
        public bool HitValley { get; private set; }
        public bool HitPeak { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void Initialize(ExchangeTradeInfo tradeInfo)
        {
            base.Initialize(tradeInfo);

            SetPlotListCount(1);
            AnchorPrice = TradeInfo.Trade.Price;
            HitValley = HitPeak = false;
            ProcessTrade();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void ProcessTrade()
        {
            double diff = TradeInfo.Trade.Price - AnchorPrice;
            PlotPoints[0].Add(new KeyValuePair<float, float>(TradeInfo.Trade.Ticks, TradeInfo.Trade.Price));
            if (HitValley && diff <= ((BuyThresholdPercent * AnchorPrice) + (BuyReverseThresholdPercent * AnchorPrice)))
            {
                // valley reversal, buy
                // lower anchor price just a bit in case price drops so we will buy more
                AnchorPrice -= (BuyFalseReverseThresholdPercent * AnchorPrice);
                HitPeak = false;
                PerformBuy();
            }
            else if (HitPeak && diff >= ((SellThresholdPercent * AnchorPrice) + (SellReverseThresholdPercent * AnchorPrice)) &&
                BuyPrices.Count != 0 && TradeInfo.Trade.Price > BuyPrices[BuyPrices.Count - 1].Value + (SellReverseThresholdPercent * AnchorPrice))
            {
                // peak reversal, sell
                AnchorPrice = TradeInfo.Trade.Price;
                HitPeak = HitValley = false;
                PerformSell();
            }
            else if (diff < (BuyThresholdPercent * AnchorPrice))
            {
                // valley
                HitValley = true;
                HitPeak = false;
                AnchorPrice = TradeInfo.Trade.Price;
            }
            else if (diff > (SellThresholdPercent * AnchorPrice))
            {
                // peak
                HitPeak = true;
                HitValley = false;
                AnchorPrice = TradeInfo.Trade.Price;
            }
            else
            {
                // watch
            }
        }
    }
}
