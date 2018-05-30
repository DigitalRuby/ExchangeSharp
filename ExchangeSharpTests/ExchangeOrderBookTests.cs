using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharpTests
{
    using System.Linq;

    using ExchangeSharp;

    using FluentAssertions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExchangeOrderBookTests
    {
        [TestMethod]
        public void AsksSorted_Ascending()
        {
            var exchangeOrderBook = new ExchangeOrderBook();
            exchangeOrderBook.Asks.Add(1.123m, new ExchangeOrderPrice { Price = 1.123m });
            exchangeOrderBook.Asks.Add(0.24m, new ExchangeOrderPrice { Price = 0.24m });
            exchangeOrderBook.Asks.Add(2.85m, new ExchangeOrderPrice { Price = 2.85m });

            exchangeOrderBook.Asks.First().Key.Should().Be(0.24m);
            exchangeOrderBook.Asks.Last().Key.Should().Be(2.85m);
        }

        [TestMethod]
        public void BidsSorted_Descending()
        {
            var exchangeOrderBook = new ExchangeOrderBook();
            exchangeOrderBook.Bids.Add(1.123m, new ExchangeOrderPrice());
            exchangeOrderBook.Bids.Add(0.24m, new ExchangeOrderPrice());
            exchangeOrderBook.Bids.Add(2.85m, new ExchangeOrderPrice());

            exchangeOrderBook.Bids.First().Key.Should().Be(2.85m);
            exchangeOrderBook.Bids.Last().Key.Should().Be(0.24m);
        }
    }
}
