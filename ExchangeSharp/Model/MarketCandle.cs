using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Candlestick data
    /// </summary>
    public class MarketCandle
    {
        /// <summary>
        /// The name of the exchange for this candle
        /// </summary>
        public string ExchangeName { get; set; }

        /// <summary>
        /// The name of the market
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Timestamp, can be the opening or closing time of the candle
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The period in seconds
        /// </summary>
        public int PeriodSeconds { get; set; }

        /// <summary>
        /// Opening price
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// Close price
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// Volume
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("{0}/{1}: {2}, {3}, {4}, {5}, {6}", Timestamp, PeriodSeconds, OpenPrice, HighPrice, LowPrice, Volume);
        }
    }
}
