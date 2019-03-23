/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Details of an exchangetrade
    /// </summary>
    public sealed class ExchangeTrade
    {
        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Trade id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Price
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// True if buy, false if sell - for some exchanges (Binance) the meaning can be different, i.e. is the buyer the maker
        /// </summary>
        public bool IsBuy
        {
            get { return ((Flags & ExchangeTradeFlags.IsBuy) == ExchangeTradeFlags.IsBuy); }
            set { Flags = (value ? Flags | ExchangeTradeFlags.IsBuy : Flags & (~ExchangeTradeFlags.IsBuy)); }
        }

        /// <summary>
        /// Flags - note that only the IsBuy bit of flags is persisted in the ToBinary and FromBinary methods.
        /// </summary>
        public ExchangeTradeFlags Flags { get; set; }

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("{0:s},{1},{2},{3}", Timestamp, Price, Amount, IsBuy ? "Buy" : "Sell");
        }

        /// <summary>
        /// Write to binary writer
        /// </summary>
        /// <param name="writer">Binary writer</param>
        public void ToBinary(BinaryWriter writer)
        {
            writer.Write(Timestamp.ToUniversalTime().Ticks);
            writer.Write(Id);
            writer.Write((double)Price);
            writer.Write((double)Amount);
            writer.Write(IsBuy);
        }

        /// <summary>
        /// Read from binary reader
        /// </summary>
        /// <param name="reader">Binary reader</param>
        public void FromBinary(BinaryReader reader)
        {
            Timestamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            Id = reader.ReadInt64();
            Price = (decimal)reader.ReadDouble();
            Amount = (decimal)reader.ReadDouble();
            IsBuy = reader.ReadBoolean();
        }
    }

    /// <summary>
    /// Exchange trade flags
    /// </summary>
    [Flags]
    public enum ExchangeTradeFlags
    {
        /// <summary>
        /// Whether the trade is a buy, if not it is a sell
        /// </summary>
        IsBuy = 1,

        /// <summary>
        /// Whether the trade is from a snapshot
        /// </summary>
        IsFromSnapshot = 2,

        /// <summary>
        /// Whether the trade is the last trade from a snapshot
        /// </summary>
        IsLastFromSnapshot = 4
    }
}
