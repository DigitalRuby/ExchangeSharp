using System;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    [TestClass]
    public class CryptoUtilityTests
    {
        private static Action Invoking(Action action) => action;

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
            void a() => CryptoUtility.RoundDown(1.2345m, -1);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void ClampPrice()
        {
            CryptoUtility.ClampPrice(0.00000100m, 100000.00000000m, 0.00000100m, 0.05507632m).Should().Be(0.055076m);
            CryptoUtility.ClampPrice(0.00000010m, 100000.00000000m, 0.00000010m, 0.00052286m).Should().Be(0.0005228m);
            CryptoUtility.ClampPrice(0.00001000m, 100000.00000000m, 0.00001000m, 0.02525215m).Should().Be(0.025252m);
            CryptoUtility.ClampPrice(0.00001000m, 100000.00000000m, null, 0.00401212m).Should().Be(0.00401212m);
        }

        [TestMethod]
        public void ClampPriceOutOfRange()
        {
            void a() => CryptoUtility.ClampPrice(-0.00000100m, 100000.00000000m, 0.00000100m, 0.05507632m);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();

            void b() => CryptoUtility.ClampPrice(0.00000100m, -100000.00000000m, 0.00000100m, 0.05507632m);
            Invoking(b).Should().Throw<ArgumentOutOfRangeException>();

            void c() => CryptoUtility.ClampPrice(0.00000100m, 100000.00000000m, 0.00000100m, -0.05507632m);
            Invoking(c).Should().Throw<ArgumentOutOfRangeException>();

            void d() => CryptoUtility.ClampPrice(0.00000100m, 100000.00000000m, -0.00000100m, 0.05507632m);
            Invoking(d).Should().Throw<ArgumentOutOfRangeException>();

            void e() => CryptoUtility.ClampPrice(100000.00000000m, 0.00000100m, -0.00000100m, 0.05507632m);
            Invoking(e).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void ClampQuantity()
        {
            CryptoUtility.ClampQuantity(0.01000000m, 90000000.00000000m, 0.01000000m, 34.55215m).Should().Be(34.55m);
            CryptoUtility.ClampQuantity(0.00100000m, 90000000.00000000m, 0.00100000m, 941.4192m).Should().Be(941.419m);
            CryptoUtility.ClampQuantity(0.00000100m, 90000000.00000000m, 0.00000100m, 172.94102192m).Should().Be(172.941021m);
            CryptoUtility.ClampQuantity(0.00010000m, 90000000.00000000m, null, 1837.31935m).Should().Be(1837.31935m);
        }

        [TestMethod]
        public void ClampQuantityOutOfRange()
        {
            void a() => CryptoUtility.ClampQuantity(-0.00010000m, 900000.00000000m, 0.00010000m, 33.393832m);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();

            void b() => CryptoUtility.ClampQuantity(0.00010000m, -900000.00000000m, 0.00010000m, 33.393832m);
            Invoking(b).Should().Throw<ArgumentOutOfRangeException>();

            void c() => CryptoUtility.ClampQuantity(0.00010000m, 900000.00000000m, 0.00010000m, -33.393832m);
            Invoking(c).Should().Throw<ArgumentOutOfRangeException>();

            void d() => CryptoUtility.ClampQuantity(0.00010000m, 900000.00000000m, -0.00010000m, 33.393832m);
            Invoking(d).Should().Throw<ArgumentOutOfRangeException>();

            void e() => CryptoUtility.ClampQuantity(900000.00000000m, 0.00010000m, -0.00010000m, 33.393832m);
            Invoking(e).Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}