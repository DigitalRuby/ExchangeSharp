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
    /// A price entry in an exchange order book
    /// </summary>
    public struct ExchangeOrderPrice
    {
        /// <summary>
        /// Price
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return "Price: " + Price + ", Amount: " + Amount;
        }

        /// <summary>
        /// Write to a binary writer
        /// </summary>
        /// <param name="writer">Binary writer</param>
        public void ToBinary(BinaryWriter writer)
        {
            writer.Write((double)Price);
            writer.Write((double)Amount);
        }

        /// <summary>
        /// Constructor from a binary reader
        /// </summary>
        /// <param name="reader">Binary reader to read from</param>
        public ExchangeOrderPrice(BinaryReader reader)
        {
            Price = (decimal)reader.ReadDouble();
            Amount = (decimal)reader.ReadDouble();
        }
    }

    /// <summary>
    /// Represents all the asks (sells) and bids (buys) for an exchange asset
    /// </summary>
    public class ExchangeOrderBook
    {
        /// <summary>
        /// List of asks (sells)
        /// </summary>
        public List<ExchangeOrderPrice> Asks { get; } = new List<ExchangeOrderPrice>();

        /// <summary>
        /// List of bids (buys)
        /// </summary>
        public List<ExchangeOrderPrice> Bids { get; } = new List<ExchangeOrderPrice>();

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("Asks: {0}, Bids: {1}", Asks.Count, Bids.Count);
        }

        /// <summary>
        /// Write to a binary writer
        /// </summary>
        /// <param name="writer">Binary writer</param>
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

        /// <summary>
        /// Read from a binary reader
        /// </summary>
        /// <param name="reader">Binary reader</param>
        public void FromBinary(BinaryReader reader)
        {
            Asks.Clear();
            Bids.Clear();
            int askCount = reader.ReadInt32();
            int bidCount = reader.ReadInt32();
            while (askCount-- > 0)
            {
                Asks.Add(new ExchangeOrderPrice(reader));
            }
            while (bidCount-- > 0)
            {
                Bids.Add(new ExchangeOrderPrice(reader));
            }
        }

        /// <summary>
        /// Get the price necessary to buy at to acquire an equivelant amount of currency from the order book, i.e. amount of 2 BTC could acquire x amount of other currency by buying at a certain BTC price.
        /// You would place a limit buy order for buyAmount of alt coin at buyPrice.
        /// </summary>
        /// <param name="amount">Amount of currency to trade, i.e. you have 0.1 BTC and want to buy an equivelant amount of alt coins</param>
        /// <param name="buyAmount">The amount of new currency that will be acquired</param>
        /// <param name="buyPrice">The price necessary to buy at to acquire buyAmount of currency</param>
        public void GetPriceToBuy(decimal amount, out decimal buyAmount, out decimal buyPrice)
        {
            ExchangeOrderPrice ask;
            decimal spent;
            buyAmount = 0m;
            buyPrice = 0m;

            for (int i = 0; i < Asks.Count && amount > 0m; i++)
            {
                ask = Asks[i];
                spent = Math.Min(amount, ask.Amount * ask.Price);
                buyAmount += spent / ask.Price;
                buyPrice = ask.Price;
                amount -= spent;
            }
        }

        /// <summary>
        /// Get the price necessary to sell amount currency. You would place a limit sell order for amount at the returned price to sell all of the amount.
        /// </summary>
        /// <param name="amount">Amount to sell</param>
        /// <returns>The price necessary to sell at to sell amount currency</returns>
        public decimal GetPriceToSell(decimal amount)
        {
            ExchangeOrderPrice bid;
            decimal sellPrice = 0m;

            for (int i = 0; i < Bids.Count && amount > 0m; i++)
            {
                bid = Bids[i];
                sellPrice = bid.Price;
                amount -= bid.Amount;
            }

            return sellPrice;
        }
    }
}
