/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Globalization;

using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    [TestClass]
    public class ExchangeTests
    {
        [TestMethod]
        public void GlobalSymbolTest()
        {
            string symbol = "BTC-ETH";
            string altSymbol = "BTC-KRW"; // WTF Bitthumb...

            // sanity test that all exchanges return the same global symbol when converted back and forth
            foreach (IExchangeAPI api in ExchangeAPI.GetExchangeAPIs())
            {
                try
                {
                    string exchangeSymbol = api.GlobalSymbolToExchangeSymbol(symbol);
                    string globalSymbol = api.ExchangeSymbolToGlobalSymbol(exchangeSymbol);
                    Assert.IsTrue(symbol == globalSymbol || altSymbol == globalSymbol);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Exchange {api.Name} error converting symbol: {ex}");
                }
            }
        }
    }
}