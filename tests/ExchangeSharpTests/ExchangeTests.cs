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
using System.Threading.Tasks;

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
        private async Task<string> GetAllSymbolsJsonAsync()
        {
            Dictionary<string, string[]> allSymbols = new Dictionary<string, string[]>();
            List<Task> tasks = new List<Task>();
            foreach (ExchangeAPI api in await ExchangeAPI.GetExchangeAPIsAsync())
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        string[] symbols = (await api.GetMarketSymbolsAsync()).ToArray();
                        lock (allSymbols)
                        {
                            allSymbols[api.Name] = symbols;
                        }
                    }
                    catch (NotImplementedException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get symbols for {0}, error: {1}", api, ex);
                    }
                }));
            }
            await Task.WhenAll(tasks);
            return JsonConvert.SerializeObject(allSymbols);
        }

		[TestMethod]
		public async Task ExchangeGetCreateTest()
		{
			// make sure get exchange api calls serve up the same instance
			var ex1 = await ExchangeAPI.GetExchangeAPIAsync<ExchangeGeminiAPI>();
			var ex2 = await ExchangeAPI.GetExchangeAPIAsync(ExchangeName.Gemini);
			Assert.AreSame(ex1, ex2);
			Assert.IsInstanceOfType(ex2, typeof(ExchangeGeminiAPI));

			// make sure create exchange serves up new instances
			var ex3 = await ExchangeAPI.CreateExchangeAPIAsync<ExchangeGeminiAPI>();
			Assert.AreNotSame(ex3, ex2);

			// make sure a bad exchange name throws correct exception
			await Assert.ThrowsExceptionAsync<ApplicationException>(() =>
			{
				return ExchangeAPI.GetExchangeAPIAsync("SirExchangeNotAppearingInThisFilm");
			});
		}

        [TestMethod]
        public async Task GlobalSymbolTest()
        {
            // if tests fail, uncomment this and it will save a new test file
            // string allSymbolsJson = await GetAllSymbolsJsonAsync(); System.IO.File.WriteAllText("TestData/AllSymbols.json", allSymbolsJson);

            string globalMarketSymbol = "ETH-BTC"; //1 ETH is worth 0.0192 BTC...
            string globalMarketSymbolAlt = "BTC-KRW"; // WTF Bitthumb... //1 BTC worth 9,783,000 won
            Dictionary<string, string[]> allSymbols = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(System.IO.File.ReadAllText("TestData/AllSymbols.json"));

            // sanity test that all exchanges return the same global symbol when converted back and forth
            foreach (IExchangeAPI api in await ExchangeAPI.GetExchangeAPIsAsync())
            {
                try
                {
                    if (api is ExchangeUfoDexAPI || api is ExchangeOKExAPI || api is ExchangeHitBTCAPI || api is ExchangeKuCoinAPI ||
                        api is ExchangeOKCoinAPI || api is ExchangeDigifinexAPI || api is ExchangeNDAXAPI || api is ExchangeBL3PAPI ||
						api is ExchangeBinanceUSAPI || api is ExchangeBinanceJerseyAPI || api is ExchangeBinanceDEXAPI ||
						api is ExchangeBitMEXAPI || api is ExchangeBTSEAPI || api is ExchangeBybitAPI)
                    {
                        // WIP
                        continue;
                    }

                    bool isBithumb = (api.Name == ExchangeName.Bithumb);
                    string exchangeMarketSymbol = await api.GlobalMarketSymbolToExchangeMarketSymbolAsync(isBithumb ? globalMarketSymbolAlt : globalMarketSymbol);
                    string globalMarketSymbol2 = await api.ExchangeMarketSymbolToGlobalMarketSymbolAsync(exchangeMarketSymbol);

                    if ((!isBithumb && globalMarketSymbol2.StartsWith("BTC-")) ||
                        globalMarketSymbol2.StartsWith("USD-") ||
                        globalMarketSymbol2.StartsWith("USDT-"))
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

						// BL3P does not have usd
						if (api.Name != ExchangeName.BL3P)
						{
							Assert.IsTrue(symbols.Contains(exchangeMarketSymbol), "Symbols does not contain exchange symbol");
						}
                    }
                    catch
                    {
                        Assert.Fail("Error getting symbols");
                    }
                    Assert.IsTrue(globalMarketSymbol == globalMarketSymbol2 || globalMarketSymbolAlt == globalMarketSymbol2);
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
