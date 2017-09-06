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
    public struct ExchangeOrderPrice
    {
        public double Price { get; set; }
        public double Amount { get; set; }

        public override string ToString()
        {
            return "Price: " + Price + ", Amount: " + Amount;
        }

        public void ToBinary(BinaryWriter writer)
        {
            writer.Write(Price);
            writer.Write(Amount);
        }

        public ExchangeOrderPrice(BinaryReader reader)
        {
            Price = reader.ReadDouble();
            Amount = reader.ReadDouble();
        }
    }

    public class ExchangeOrderBook
    {
        public List<ExchangeOrderPrice> Asks { get; } = new List<ExchangeOrderPrice>();
        public List<ExchangeOrderPrice> Bids { get; } = new List<ExchangeOrderPrice>();

        public void ToBinary(BinaryWriter writer)
        {
            writer.Write(Asks.Count);
            writer.Write(Bids.Count);
            foreach (ExchangeOrderPrice price in Asks)
            {
                price.ToBinary(writer);
            }
            foreach (ExchangeOrderPrice price in Bids)
            {
                price.ToBinary(writer);
            }
        }

        public void FromBinary(BinaryReader reader)
        {
            Asks.Clear();
            Bids.Clear();
            for (int i = 0; i < reader.ReadInt32(); i++)
            {
                Asks.Add(new ExchangeOrderPrice(reader));
            }
            for (int i = 0; i < reader.ReadInt32(); i++)
            {
                Bids.Add(new ExchangeOrderPrice(reader));
            }
        }
    }
}
