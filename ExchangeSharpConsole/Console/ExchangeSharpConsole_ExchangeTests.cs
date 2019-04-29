/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using ExchangeSharp;

namespace ExchangeSharpConsole
{
    public static partial class ExchangeSharpConsoleMain
    {
        private static void Assert(bool expression)
        {
            if (!expression)
            {
                throw new ApplicationException("Test failure, unexpected result");
            }
        }

        private static void TestExchanges(string nameRegex = null, string functionRegex = null)
        {
            string GetSymbol(IExchangeAPI api)
            {
                if (api is ExchangeCryptopiaAPI || api is ExchangeLivecoinAPI || api is ExchangeZBcomAPI)
                {
                    return "LTC-BTC";
                }
                else if (api is ExchangeKrakenAPI)
                {
                    return "XXBTZ-USD";
                }
                else if (api is ExchangeBittrexAPI || api is ExchangePoloniexAPI)
                {
                    return "BTC-LTC";
                }
                else if (api is ExchangeBinanceAPI || api is ExchangeOkexAPI ||/* api is ExchangeBleutradeAPI ||*/
                    api is ExchangeKucoinAPI || api is ExchangeHuobiAPI || api is ExchangeAbucoinsAPI)
                {
                    return "ETH-BTC";
                }
                else if (api is ExchangeYobitAPI || api is ExchangeBitBankAPI)
                {
                    return "LTC-BTC";
                }
                else if (api is ExchangeTuxExchangeAPI)
                {
                    return "BTC-ETH";
                }
                else if (api is ExchangeBitMEXAPI)
                {
                    return "XBT-USD";
                }
                return "BTC-USD";
            }

            ExchangeTrade[] trades = null;
            bool histTradeCallback(IEnumerable<ExchangeTrade> tradeEnum)
            {
                trades = tradeEnum.ToArray();
                return true;
            }

            IExchangeAPI[] apis = ExchangeAPI.GetExchangeAPIs();
            foreach (IExchangeAPI api in apis)
            {
                // WIP exchanges...
                if (api is ExchangeUfoDexAPI)
                {
                    continue;
                }
                else if (nameRegex != null && !Regex.IsMatch(api.Name, nameRegex, RegexOptions.IgnoreCase))
                {
                    continue;
                }

                // test all public API for each exchange
                try
                {
                    string marketSymbol = api.NormalizeMarketSymbol(GetSymbol(api));

                    if (functionRegex == null || Regex.IsMatch("symbol", functionRegex, RegexOptions.IgnoreCase))
                    {
                        Console.Write("Test {0} GetSymbolsAsync... ", api.Name);
                        IReadOnlyCollection<string> symbols = api.GetMarketSymbolsAsync().Sync().ToArray();
                        Assert(symbols != null && symbols.Count != 0 && symbols.Contains(marketSymbol, StringComparer.OrdinalIgnoreCase));
                        Console.WriteLine($"OK (default: {marketSymbol}; {symbols.Count} symbols)");
                    }

                    if (functionRegex == null || Regex.IsMatch("currencies", functionRegex, RegexOptions.IgnoreCase))
                    {
                        try
                        {
                            Console.Write("Test {0} GetCurrenciesAsync... ", api.Name);
                            var currencies = api.GetCurrenciesAsync().Sync();
                            Assert(currencies.Count != 0);
                            Console.WriteLine($"OK ({currencies.Count} currencies)");
                        }
                        catch (NotImplementedException)
                        {
                            Console.WriteLine($"Not implemented");
                        }
                    }

                    if (functionRegex == null || Regex.IsMatch("orderbook", functionRegex, RegexOptions.IgnoreCase))
                    {
                        try
                        {
                            Console.Write("Test {0} GetOrderBookAsync... ", api.Name);
                            var book = api.GetOrderBookAsync(marketSymbol).Sync();
                            Assert(book.Asks.Count != 0 && book.Bids.Count != 0 && book.Asks.First().Value.Amount > 0m &&
                                book.Asks.First().Value.Price > 0m && book.Bids.First().Value.Amount > 0m && book.Bids.First().Value.Price > 0m);
                            Console.WriteLine($"OK ({book.Asks.Count} asks, {book.Bids.Count} bids)");
                        }
                        catch (NotImplementedException)
                        {
                            Console.WriteLine($"Not implemented");
                        }
                    }

                    if (functionRegex == null || Regex.IsMatch("ticker", functionRegex, RegexOptions.IgnoreCase))
                    {
                        try
                        {
                            Console.Write("Test {0} GetTickerAsync... ", api.Name);
                            var ticker = api.GetTickerAsync(marketSymbol).Sync();
                            Assert(ticker != null && ticker.Ask > 0m && ticker.Bid > 0m && ticker.Last > 0m &&
                                ticker.Volume != null && ticker.Volume.QuoteCurrencyVolume > 0m && ticker.Volume.BaseCurrencyVolume > 0m);
                            Console.WriteLine($"OK (ask: {ticker.Ask}, bid: {ticker.Bid}, last: {ticker.Last})");
                        }
                        catch
                        {
                            Console.WriteLine($"Data invalid or empty");
                        }
                    }

                    if (functionRegex == null || Regex.IsMatch("trade", functionRegex, RegexOptions.IgnoreCase))
                    {
                        try
                        {
                            Console.Write("Test {0} GetHistoricalTradesAsync... ", api.Name);
                            api.GetHistoricalTradesAsync(histTradeCallback, marketSymbol).Sync();
                            Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
                            Console.WriteLine($"OK ({trades.Length})");

                            Console.Write("Test {0} GetRecentTradesAsync... ", api.Name);
                            trades = api.GetRecentTradesAsync(marketSymbol).Sync().ToArray();
                            Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
                            Console.WriteLine($"OK ({trades.Length} trades)");
                        }
                        catch (NotImplementedException)
                        {
                            Console.WriteLine($"Not implemented");
                        }
                    }

                    if (functionRegex == null || Regex.IsMatch("candle", functionRegex, RegexOptions.IgnoreCase))
                    {
                        try
                        {
                            Console.Write("Test {0} GetCandlesAsync... ", api.Name);
                            var candles = api.GetCandlesAsync(marketSymbol, 86400, CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(7.0)), null).Sync().ToArray();
                            Assert(candles.Length != 0 && candles[0].ClosePrice > 0m && candles[0].HighPrice > 0m && candles[0].LowPrice > 0m && candles[0].OpenPrice > 0m &&
                                candles[0].HighPrice >= candles[0].LowPrice && candles[0].HighPrice >= candles[0].ClosePrice && candles[0].HighPrice >= candles[0].OpenPrice &&
                                !string.IsNullOrWhiteSpace(candles[0].Name) && candles[0].ExchangeName == api.Name && candles[0].PeriodSeconds == 86400 && candles[0].BaseCurrencyVolume > 0.0 &&
                                candles[0].QuoteCurrencyVolume > 0.0 && candles[0].WeightedAverage >= 0m);

                            Console.WriteLine($"OK ({candles.Length})");
                        }
                        catch (NotImplementedException)
                        {
                            Console.WriteLine($"Not implemented");
                        }
                        catch
                        {
                            // These API require private access to get candles end points
                            if (!(api is ExchangeKucoinAPI))
                            {
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Request failed, api: {0}, error: {1}", api, ex.Message);
                }
            }
        }

        public static void RunPerformTests(Dictionary<string, string> dict)
        {
            dict.TryGetValue("exchangeName", out string exchangeNameRegex);
            dict.TryGetValue("function", out string functionRegex);
            TestExchanges(exchangeNameRegex, functionRegex);
        }
    }
}