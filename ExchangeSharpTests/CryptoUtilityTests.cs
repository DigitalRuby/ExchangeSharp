/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    using System.Globalization;

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
            CryptoUtility.ClampDecimal(0.00000100m, 100000.00000000m, 0.00000100m, 0.05507632m).Should().Be(0.055076m);
            CryptoUtility.ClampDecimal(0.00000010m, 100000.00000000m, 0.00000010m, 0.00052286m).Should().Be(0.0005228m);
            CryptoUtility.ClampDecimal(0.00001000m, 100000.00000000m, 0.00001000m, 0.02525215m).Should().Be(0.02525m);
            CryptoUtility.ClampDecimal(0.00001000m, 100000.00000000m, null, 0.00401212m).Should().Be(0.00401212m);
        }

        [TestMethod]
        public void ClampDecimalTrailingZeroesRemoved()
        {
            decimal result = CryptoUtility.ClampDecimal(0, Decimal.MaxValue, 0.01m, 1.23456789m);
            result.Should().Be(1.23m);
            result.ToString(CultureInfo.InvariantCulture).Should().NotEndWith("0");
        }

        [TestMethod]
        public void ClampPriceOutOfRange()
        {
            void a() => CryptoUtility.ClampDecimal(-0.00000100m, 100000.00000000m, 0.00000100m, 0.05507632m);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();

            void b() => CryptoUtility.ClampDecimal(0.00000100m, -100000.00000000m, 0.00000100m, 0.05507632m);
            Invoking(b).Should().Throw<ArgumentOutOfRangeException>();

            void c() => CryptoUtility.ClampDecimal(0.00000100m, 100000.00000000m, 0.00000100m, -0.05507632m);
            Invoking(c).Should().Throw<ArgumentOutOfRangeException>();

            void d() => CryptoUtility.ClampDecimal(0.00000100m, 100000.00000000m, -0.00000100m, 0.05507632m);
            Invoking(d).Should().Throw<ArgumentOutOfRangeException>();

            void e() => CryptoUtility.ClampDecimal(100000.00000000m, 0.00000100m, -0.00000100m, 0.05507632m);
            Invoking(e).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void ClampQuantity()
        {
            CryptoUtility.ClampDecimal(0.01000000m, 90000000.00000000m, 0.01000000m, 34.55215m).Should().Be(34.55m);
            CryptoUtility.ClampDecimal(0.00100000m, 90000000.00000000m, 0.00100000m, 941.4192m).Should().Be(941.419m);
            CryptoUtility.ClampDecimal(0.00000100m, 90000000.00000000m, 0.00000100m, 172.94102192m).Should().Be(172.941021m);
            CryptoUtility.ClampDecimal(0.00010000m, 90000000.00000000m, null, 1837.31935m).Should().Be(1837.31935m);
        }

        [TestMethod]
        public void ClampQuantityOutOfRange()
        {
            void a() => CryptoUtility.ClampDecimal(-0.00010000m, 900000.00000000m, 0.00010000m, 33.393832m);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();

            void b() => CryptoUtility.ClampDecimal(0.00010000m, -900000.00000000m, 0.00010000m, 33.393832m);
            Invoking(b).Should().Throw<ArgumentOutOfRangeException>();

            void c() => CryptoUtility.ClampDecimal(0.00010000m, 900000.00000000m, 0.00010000m, -33.393832m);
            Invoking(c).Should().Throw<ArgumentOutOfRangeException>();

            void d() => CryptoUtility.ClampDecimal(0.00010000m, 900000.00000000m, -0.00010000m, 33.393832m);
            Invoking(d).Should().Throw<ArgumentOutOfRangeException>();

            void e() => CryptoUtility.ClampDecimal(900000.00000000m, 0.00010000m, -0.00010000m, 33.393832m);
            Invoking(e).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void CalculatePrecision_NoDecimals_Returns1()
        {
            CryptoUtility.CalculatePrecision("24").Should().Be(1);
            CryptoUtility.CalculatePrecision("1000").Should().Be(1);
            CryptoUtility.CalculatePrecision("123456789123456789465132").Should().Be(1);
        }

        [TestMethod]
        public void CalculatePrecision_WithDecimals()
        {
            CryptoUtility.CalculatePrecision("1.12").Should().Be(0.01m);
            CryptoUtility.CalculatePrecision("1.123456789").Should().Be(0.000000001m);
            CryptoUtility.CalculatePrecision("1.0").Should().Be(0.1m);
            CryptoUtility.CalculatePrecision("0.00000").Should().Be(0.00001m);
        }
    }
}