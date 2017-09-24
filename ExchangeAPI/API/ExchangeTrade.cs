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
    public class ExchangeTrade
    {
        public DateTime Timestamp { get; set; }
        public long Id { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public bool IsBuy { get; set; }

        public override string ToString()
        {
            return string.Format("{0:s},{1},{2},{3}", Timestamp, Price, Amount, IsBuy ? "Buy" : "Sell");
        }

        public void ToBinary(BinaryWriter writer)
        {
            writer.Write(Timestamp.ToUniversalTime().Ticks);
            writer.Write(Id);
            writer.Write((double)Price);
            writer.Write((double)Amount);
            writer.Write(IsBuy);
        }

        public void FromBinary(BinaryReader reader)
        {
            Timestamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            Id = reader.ReadInt64();
            Price = (decimal)reader.ReadDouble();
            Amount = (decimal)reader.ReadDouble();
            IsBuy = reader.ReadBoolean();
        }
    }
}
