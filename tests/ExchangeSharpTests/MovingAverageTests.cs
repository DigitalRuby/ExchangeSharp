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
    public class MovingAverageTests
    {
        [TestMethod]
        public void MovingAverageCalculator()
        {
            // no change
            const int len1 = 10;
            var ma1 = new MovingAverageCalculator(len1);
            for (int i = 0; i < len1 * 2; i++)
            {
                ma1.NextValue(5.0);
                Assert.AreNotEqual((i < len1 - 1), ma1.IsMature);
                Assert.AreEqual(ma1.MovingAverage, 5.0);
            }
            Assert.IsTrue(ma1.IsMature);
            Assert.AreEqual(ma1.MovingAverage, 5.0);
            Assert.AreEqual(ma1.Slope, 0.0);
            Assert.AreEqual(ma1.ExponentialMovingAverage, 5.0);
            Assert.AreEqual(ma1.ExponentialSlope, 0.0);

            // constant rise
            const int len2 = 10;
            var ma2 = new MovingAverageCalculator(len2);
            for (int i = 0; i < len2; i++)
            {
                ma2.NextValue(i);
                Assert.AreEqual(ma2.MovingAverage, i / 2.0);
                Assert.AreNotEqual((i < len2 - 1), ma2.IsMature);
            }
            Assert.IsTrue(ma2.IsMature);
            Assert.AreEqual(ma2.MovingAverage, 4.5);
            Assert.AreEqual(ma2.Slope, 0.5);
            Assert.AreEqual(ma2.ExponentialMovingAverage, 5.2393684801212155);
            Assert.AreEqual(ma2.ExponentialSlope, 0.83569589330639626);

            for (int i = len2; i < len2 * 2; i++)
            {
                ma2.NextValue(i);
                Assert.AreEqual(ma2.MovingAverage, i - 4.5);
            }

            // step function
            const int len3 = 10;
            var ma3 = new MovingAverageCalculator(len3);
            for (int i = 0; i < len3; i++)
            {
                ma3.NextValue(i < 5 ? 0 : 1.0);
            }
            Assert.AreEqual(ma3.MovingAverage, 0.5);
            Assert.AreEqual(ma3.Slope, 0.05555555555555558);
            Assert.AreEqual(ma3.ExponentialMovingAverage, 0.63335216794679949);
            Assert.AreEqual(ma3.ExponentialSlope, 0.081477296011822409);

            // inverse step function
            const int len4 = 10;
            var ma4 = new MovingAverageCalculator(len4);
            for (int i = 0; i < len4; i++)
            {
                ma4.NextValue(i < 5 ? 1.0 : 0);
            }
            Assert.AreEqual(ma4.MovingAverage, 0.5);
            Assert.AreEqual(ma4.Slope, -0.05555555555555558);
            Assert.AreEqual(ma4.ExponentialMovingAverage, 0.36664783205320051);
            Assert.AreEqual(ma4.ExponentialSlope, -0.081477296011822353);
        }
    }
}