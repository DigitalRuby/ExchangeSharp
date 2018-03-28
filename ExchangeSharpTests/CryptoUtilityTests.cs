namespace ExchangeSharpTests
{
    using System;

    using ExchangeSharp;

    using FluentAssertions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CryptoUtilityTests
    {
        [TestMethod]
        public void RoundDown()
        {
            CryptoUtility.RoundDown(1.2345m, 2).Should().Be(1.23m);
            CryptoUtility.RoundDown(1.2345m, 4).Should().Be(1.2345m);
            CryptoUtility.RoundDown(1.2345m, 5).Should().Be(1.2345m);
            CryptoUtility.RoundDown(1.2345m, 0).Should().Be(1m);
            CryptoUtility.RoundDown(1.2345m).Should().Be(1.234m);
        }

        [TestMethod]
        public void RoundDownDefaultRules()
        {
            CryptoUtility.RoundDown(0.000123456789m).Should().Be(0.0001234m);
            CryptoUtility.RoundDown(1.2345678m).Should().Be(1.234m);
            CryptoUtility.RoundDown(10.2345678m).Should().Be(10m);
        }

        [TestMethod]
        public void RoundDownOutOfRange()
        {
            Action a = () => CryptoUtility.RoundDown(1.2345m, -1);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}