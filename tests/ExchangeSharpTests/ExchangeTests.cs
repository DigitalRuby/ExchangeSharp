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
using System.Threading;
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
				tasks.Add(
						Task.Run(async () =>
						{
							try
							{
								string[] symbols = (await api.GetMarketSymbolsAsync()).ToArray();
								lock (allSymbols)
								{
									allSymbols[api.Name] = symbols;
								}
							}
							catch (NotImplementedException) { }
							catch (Exception ex)
							{
								Console.WriteLine("Failed to get symbols for {0}, error: {1}", api, ex);
							}
						})
				);
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
			Dictionary<string, string[]> allSymbols = JsonConvert.DeserializeObject<
					Dictionary<string, string[]>
			>(
					System.IO.File.ReadAllText("TestData/AllSymbols.json"),
					ExchangeAPI.SerializerSettings
			);

			// sanity test that all exchanges return the same global symbol when converted back and forth
			foreach (IExchangeAPI api in await ExchangeAPI.GetExchangeAPIsAsync())
			{
				try
				{
					if (
							api is ExchangeUfoDexAPI
							|| api is ExchangeOKExAPI
							|| api is ExchangeHitBTCAPI
							|| api is ExchangeKuCoinAPI
							|| api is ExchangeOKCoinAPI
							|| api is ExchangeDigifinexAPI
							|| api is ExchangeNDAXAPI
							|| api is ExchangeBL3PAPI
							|| api is ExchangeBinanceUSAPI
							|| api is ExchangeBinanceJerseyAPI
							|| api is ExchangeBinanceDEXAPI
							|| api is ExchangeBinanceAPI
							|| api is ExchangeBitMEXAPI
							|| api is ExchangeBTSEAPI
							|| api is ExchangeBybitAPI
							|| api is ExchangeAquanowAPI
							|| api is ExchangeBitfinexAPI
							|| api is ExchangeBittrexAPI
							|| api is ExchangeFTXAPI
							|| api is ExchangeFTXUSAPI
							|| api is ExchangeGateIoAPI
							|| api is ExchangeCoinmateAPI
							|| api is ExchangeBitflyerApi
							|| api is ExchangeDydxApi
							|| api is ExchangeCryptoComApi
							|| api is ExchangeApolloXApi
					)
					{
						// WIP
						continue;
					}

					bool isBithumb = (api.Name == ExchangeName.Bithumb);
					string exchangeMarketSymbol =
							await api.GlobalMarketSymbolToExchangeMarketSymbolAsync(
									isBithumb ? globalMarketSymbolAlt : globalMarketSymbol
							);
					string globalMarketSymbol2 =
							await api.ExchangeMarketSymbolToGlobalMarketSymbolAsync(
									exchangeMarketSymbol
							);

					if (
							(!isBithumb && globalMarketSymbol2.StartsWith("BTC-"))
							|| globalMarketSymbol2.StartsWith("USD-")
							|| globalMarketSymbol2.StartsWith("USDT-")
					)
					{
						Assert.Fail($"Exchange {api.Name} has wrong SymbolIsReversed parameter");
					}
					try
					{
						if (!allSymbols.ContainsKey(api.Name))
						{
							throw new InvalidOperationException(
									"If new exchange has no symbols, run GetAllSymbolsJson to make a new string "
											+ "then apply this new string to Resources.AllSymbolsJson"
							);
						}
						string[] symbols = allSymbols[api.Name];

						// BL3P does not have usd
						if (api.Name != ExchangeName.BL3P)
						{
							Assert.IsTrue(
									symbols.Contains(exchangeMarketSymbol),
									"Symbols does not contain exchange symbol"
							);
						}
					}
					catch
					{
						Assert.Fail("Error getting symbols");
					}
					Assert.IsTrue(
							globalMarketSymbol == globalMarketSymbol2
									|| globalMarketSymbolAlt == globalMarketSymbol2
					);
				}
				catch (NotImplementedException) { }
				catch (Exception ex)
				{
					Assert.Fail($"Exchange {api.Name} error converting symbol: {ex}");
				}
			}
		}

		[TestMethod]
		public async Task TradesWebsocketTest()
		{
			foreach (IExchangeAPI api in await ExchangeAPI.GetExchangeAPIsAsync())
			{
				if (
						api is ExchangeBinanceDEXAPI // volume too low
						|| api is ExchangeBinanceJerseyAPI // ceased operations
						|| api is ExchangeBittrexAPI // uses SignalR
						|| api is ExchangeBL3PAPI // volume too low
						|| api is ExchangeFTXUSAPI // volume too low. rely on FTX test
						|| api is ExchangeLivecoinAPI // defunct
						|| api is ExchangeOKCoinAPI // volume appears to be too low
						|| api is ExchangeNDAXAPI // volume too low for automated testing
				)
				{
					continue;
				}
				//if (api is ExchangeKrakenAPI)
				try
				{
					var delayCTS = new CancellationTokenSource();
					var marketSymbols = await api.GetMarketSymbolsAsync();
					string testSymbol = null;
					if (api is ExchangeKrakenAPI)
						testSymbol = "XBTUSD";
					if (testSymbol == null)
						testSymbol = marketSymbols
								.Where(
										s => // usually highest volume so we're not waiting around here
												(s.ToUpper().Contains("BTC") || s.ToUpper().Contains("XBT"))
												&& s.ToUpper().Contains("USD")
												&& !(
														s.ToUpper().Contains("TBTC")
														|| s.ToUpper().Contains("WBTC")
														|| s.ToUpper().Contains("NHBTC")
														|| s.ToUpper().Contains("BTC3L")
														|| s.ToUpper().Contains("USDC")
														|| s.ToUpper().Contains("SUSD")
														|| s.ToUpper().Contains("BTC-TUSD")
														|| s.ToUpper().Contains("RENBTC_USDT")
												)
								)
								.FirstOrDefault();
					if (testSymbol == null)
						testSymbol = marketSymbols.First();
					bool thisExchangePassed = false;
					using (
							var socket = await api.GetTradesWebSocketAsync(
									async kvp =>
									{
										if (!kvp.Value.Flags.HasFlag(ExchangeTradeFlags.IsFromSnapshot))
										{ // skip over any snapshot ones bc we cannot test time zone on those
											if (kvp.Value.Timestamp.Hour == DateTime.UtcNow.Hour)
											{
												thisExchangePassed = true;
												delayCTS.Cancel(); // msg received. this exchange passes
											}
											else
												Assert.Fail(
																			$"Trades are not in the UTC time zone for exchange {api.GetType().Name}."
																	);
										}
									},
									testSymbol
							)
					)
					{
						socket.Disconnected += async s =>
								Assert.Fail($"disconnected by exchange {api.GetType().Name}");
						await Task.Delay(100000, delayCTS.Token);
						if (!thisExchangePassed)
							Assert.Fail(
									$"No msgs recieved after 100 seconds for exchange {api.GetType().Name}."
							);
					}
				}
				catch (NotImplementedException) { } // no need to test exchanges where trades websocket is not implemented
				catch (TaskCanceledException) { } // if the delay task is cancelled
				catch (Exception ex)
				{
					Assert.Fail($"For exchange {api.GetType().Name}, encountered exception {ex}.");
				}
			}
		}
	}
}
