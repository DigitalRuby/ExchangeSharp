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
    public abstract class Trader
    {
        // state
        public long LastTradeTimestamp { get; protected set; }
        public int Buys { get; protected set; }
        public int Sells { get; protected set; }
        public decimal ItemCount { get; protected set; }
        public decimal Profit { get; protected set; }
        public decimal Spend { get; protected set; }
        public decimal StartCashFlow { get; protected set; }

#if DEBUG

        protected long lastTradeTicks;

#endif

        // configuration
        public decimal CashFlow { get; set; } // can be set for testing but the API will typically grab this
        public long Interval { get; set; } // milliseconds
        public decimal BuyUnits { get; set; } = 1.0m;
        public decimal SellUnits { get; set; } = 1.0m;
        public double BuyThresholdPercent { get; set; } // how low price must go from baseline before considering a buy
        public double SellThresholdPercent { get; set; } // how high price must go from purchase point before considering a sell
        public double BuyReverseThresholdPercent { get; set; } // how high the price must go up from BuyThreshold to do a buy, this tries to predict when a valley is ending
        public double BuyFalseReverseThresholdPercent { get; set; } // how low the price and go from BuyReverseThresholdPercent to tell the trader the price is continuing to drop and buy more
        public double SellReverseThresholdPercent { get; set; } // how low the price mus go down from a SellThreshold to do a sell, this tries to predict when a peak is ending
        public decimal FeePercentage { get; set; } = 0.0025m; // fee percent * price of a trade = fee for the trade
        public decimal OrderPriceDifferentialPercentage { get; set; } = 0.001m; // lower sell orders and increase buy orders by this amount to ensure they get filled
        public bool ProductionMode { get; set; } // default is false, no trades or API calls

        public ExchangeTradeInfo TradeInfo { get; private set; }

        public List<List<KeyValuePair<float, float>>> PlotPoints { get; set; } = new List<List<KeyValuePair<float, float>>>();
        public List<KeyValuePair<float, float>> BuyPrices { get; } = new List<KeyValuePair<float, float>>();
        public List<KeyValuePair<float, float>> SellPrices { get; } = new List<KeyValuePair<float, float>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void Initialize(ExchangeTradeInfo info)
        {
            TradeInfo = info;
            LastTradeTimestamp = info.Trade.Ticks;
            UpdateAmounts();
            StartCashFlow = CashFlow;
            Profit = (CashFlow - StartCashFlow) + (ItemCount * (decimal)info.Trade.Price);
			ItemCount = 0.0m;
			Buys = Sells = 0;
            Spend = 0.0m;

#if DEBUG

            lastTradeTicks = info.Trade.Ticks;

#endif

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAmounts()
        {
            if (ProductionMode)
            {
                var dict = TradeInfo.ExchangeInfo.API.GetAmountsAvailableToTrade();
                dict.TryGetValue("BTC", out decimal itemCount);
                dict.TryGetValue("USD", out decimal cashFlow);
                ItemCount = itemCount;
                CashFlow = cashFlow;
            }
            Profit = (CashFlow - StartCashFlow) + (ItemCount * (decimal)TradeInfo.Trade.Price);
        }

        protected void SetPlotListCount(int count)
        {
            while (count-- > 0)
            {
                PlotPoints.Add(new List<KeyValuePair<float, float>>());
            }
        }

        /// <summary>
        /// Use TradeInfo property to update the trader
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void ProcessTrade();

        public override string ToString()
        {
            return string.Format("Profit: {0}, Cash Flow: {1}, Item Count: {2}, Buys: {3}, Sells: {4}", Profit, CashFlow, ItemCount, Buys, Sells);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            CashFlow = StartCashFlow;
            LastTradeTimestamp = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            if (TradeInfo.Trade.Ticks <= 0)
            {
                return;
            }

            // init
            else if (LastTradeTimestamp == 0)
            {
                Initialize(TradeInfo);
                return;
            }
            else if (TradeInfo.Trade.Ticks - LastTradeTimestamp >= Interval)
            {

#if DEBUG

                System.Diagnostics.Debug.Assert(TradeInfo.Trade.Ticks >= lastTradeTicks, "Out of order timestamps on trades!");
                lastTradeTicks = TradeInfo.Trade.Ticks;

#endif

                LastTradeTimestamp = TradeInfo.Trade.Ticks;
                ProcessTrade();
            }

            UpdateAmounts();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal PerformBuy(decimal count = -1)
        {
            count = (count <= 0m ? BuyUnits : count);
            if (CashFlow >= ((decimal)TradeInfo.Trade.Price * count))
            {
                // buy one
                decimal actualBuyPrice = ((decimal)TradeInfo.Trade.Price * count);
                actualBuyPrice += (actualBuyPrice * OrderPriceDifferentialPercentage);
                if (ProductionMode)
                {
                    TradeInfo.ExchangeInfo.API.PlaceOrder(new ExchangeOrderRequest
                    {
                        Amount = count,
                        IsBuy = true,
                        Price = actualBuyPrice,
                        ShouldRoundAmount = false,
                        Symbol = TradeInfo.Symbol
                    });
                }
                else
                {
                    actualBuyPrice += (actualBuyPrice * FeePercentage);
                    CashFlow -= actualBuyPrice;
                    ItemCount += count;
                    Spend += actualBuyPrice;
                    BuyPrices.Add(new KeyValuePair<float, float>(TradeInfo.Trade.Ticks, TradeInfo.Trade.Price));
                }
                Buys++;
                UpdateAmounts();
                return count;
            }
            return 0m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal PerformSell(decimal count = -1)
        {
            count = (count <= 0m ? SellUnits : count);
            if (ItemCount >= count)
            {
                decimal actualSellPrice = ((decimal)TradeInfo.Trade.Price * count);
                actualSellPrice -= (actualSellPrice * OrderPriceDifferentialPercentage);
                if (ProductionMode)
                {
                    TradeInfo.ExchangeInfo.API.PlaceOrder(new ExchangeOrderRequest
                    {
                        Amount = count,
                        IsBuy = false,
                        Price = actualSellPrice,
                        ShouldRoundAmount = false,
                        Symbol = TradeInfo.Symbol
                    });
                }
                else
                {
                    actualSellPrice -= (actualSellPrice * FeePercentage);
                    CashFlow += actualSellPrice;
                    ItemCount -= count;
                    SellPrices.Add(new KeyValuePair<float, float>(TradeInfo.Trade.Ticks, TradeInfo.Trade.Price));
                }
                Sells++;
                UpdateAmounts();
                return count;
            }
            return 0m;
        }
    }
}
