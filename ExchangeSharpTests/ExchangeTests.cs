/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ExchangeSharp;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;

namespace ExchangeSharpTests
{
    [TestClass]
    public class ExchangeTests
    {
        /// <summary>
        /// Loop through all exchanges, get a json string for all symbols
        /// </summary>
        /// <returns></returns>
        private string GetAllSymbolsJson()
        {
            Dictionary<string, string[]> allSymbols = new Dictionary<string, string[]>();
            foreach (IExchangeAPI api in ExchangeAPI.GetExchangeAPIs())
            {
                allSymbols[api.Name] = api.GetSymbolsAsync().Sync().ToArray();
            }
            return JsonConvert.SerializeObject(allSymbols);
        }

        [TestMethod]
        public void GlobalSymbolTest()
        {
            string globalSymbol = "BTC-ETH";
            string globalSymbolAlt = "KRW-BTC"; // WTF Bitthumb...
            Dictionary<string, string[]> allSymbols = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(Resources.AllSymbolsJson);

            // sanity test that all exchanges return the same global symbol when converted back and forth
            foreach (IExchangeAPI api in ExchangeAPI.GetExchangeAPIs())
            {
                try
                {
                    bool isBithumb = (api.Name == ExchangeName.Bithumb);
                    string exchangeSymbol = api.GlobalSymbolToExchangeSymbol(isBithumb ? globalSymbolAlt : globalSymbol);
                    string globalSymbol2 = api.ExchangeSymbolToGlobalSymbol(exchangeSymbol);
                    if ((!isBithumb && globalSymbol2.EndsWith("-BTC")) ||
                        globalSymbol2.EndsWith("-USD") ||
                        globalSymbol2.EndsWith("-USDT"))
                    {
                        Assert.Fail($"Exchange {api.Name} has wrong SymbolIsReversed parameter");
                    }
                    try
                    {
                        if (!allSymbols.ContainsKey(api.Name))
                        {
                            throw new InvalidOperationException("If new exchange has no symbols, run GetAllSymbolsJson to make a new string " +
                                "then apply this new string to Resources.AllSymbolsJson");
                        }
                        string[] symbols = allSymbols[api.Name];
                        Assert.IsTrue(symbols.Contains(exchangeSymbol), "Symbols does not contain exchange symbol");
                    }
                    catch
                    {
                        Assert.Fail("Error getting symbols");
                    }
                    Assert.IsTrue(globalSymbol == globalSymbol2 || globalSymbolAlt == globalSymbol2);
                }
                catch (NotImplementedException)
                {
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Exchange {api.Name} error converting symbol: {ex}");
                }
            }
        }
    }
}