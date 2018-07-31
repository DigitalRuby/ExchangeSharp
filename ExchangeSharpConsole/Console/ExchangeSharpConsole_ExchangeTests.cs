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

using ExchangeSharp;

namespace ExchangeSharpConsoleApp
{
    public static partial class ExchangeSharpConsole
    {
        private static void Assert(bool expression)
        {
            if (!expression)
            {
                throw new ApplicationException("Test failure, unexpected result");
            }
        }

        private static void TestExchanges(string nameRegex = null)
        {
            string GetSymbol(IExchangeAPI api)
            {
                if (api is ExchangeCryptopiaAPI || api is ExchangeLivecoinAPI)
                {
                    return "LTC/BTC";
                }
                else if (api is ExchangeKrakenAPI)
                {
                    return api.NormalizeSymbol("XXBTZUSD");
                }
                else if (api is ExchangeBittrexAPI || api is ExchangePoloniexAPI)
                {
                    return api.NormalizeSymbol("BTC-LTC");
                }
                else if (api is ExchangeBinanceAPI || api is ExchangeOkexAPI || api is ExchangeBleutradeAPI ||
                    api is ExchangeKucoinAPI || api is ExchangeHuobiAPI)
                {
                    return api.NormalizeSymbol("ETH-BTC");
                }
                else if (api is ExchangeYobitAPI)
                {
                    return api.NormalizeSymbol("LTC_BTC");
                }
                else if (api is ExchangeTuxExchangeAPI || api is ExchangeAbucoinsAPI)
                {
                    return api.NormalizeSymbol("BTC_ETH");
                }
                else if (api is ExchangeBitMEXAPI)
                {
                    return api.NormalizeSymbol("XBTUSD");
                }
                return api.NormalizeSymbol("BTC-USD");
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
                if (nameRegex != null && !System.Text.RegularExpressions.Regex.IsMatch(api.Name, nameRegex, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    continue;
                }

                // test all public API for each exchange
                try
                {
                    string symbol = GetSymbol(api);

                    IReadOnlyCollection<string> symbols = api.GetSymbols().ToArray();
                    Assert(symbols != null && symbols.Count != 0 && symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase));
                    Console.WriteLine($"API {api.Name} GetSymbols OK (default: {symbol}; {symbols.Count} symbols)");

                    try
                    {
                        var book = api.GetOrderBook(symbol);
                        Assert(book.Asks.Count != 0 && book.Bids.Count != 0 && book.Asks.First().Value.Amount > 0m &&
                            book.Asks.First().Value.Price > 0m && book.Bids.First().Value.Amount > 0m && book.Bids.First().Value.Price > 0m);
                        Console.WriteLine($"API {api.Name} GetOrderBook OK ({book.Asks.Count} asks, {book.Bids.Count} bids)");
                    }
                    catch (NotImplementedException)
                    {
                        Console.WriteLine($"API {api.Name} GetOrderBook not implemented");
                    }

                    try
                    {
                        var ticker = api.GetTicker(symbol);
                        Assert(ticker != null && ticker.Ask > 0m && ticker.Bid > 0m && ticker.Last > 0m &&
                            ticker.Volume != null && ticker.Volume.BaseVolume > 0m && ticker.Volume.ConvertedVolume > 0m);
                        Console.WriteLine($"API {api.Name} GetTicker OK (ask: {ticker.Ask}, bid: {ticker.Bid}, last: {ticker.Last})");
                    }
                    catch
                    {
                        Console.WriteLine($"API {api.Name} GetTicker data invalid or empty");
                    }

                    try
                    {
                        api.GetHistoricalTrades(histTradeCallback, symbol);
                        trades = api.GetRecentTrades(symbol).ToArray();
                        Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
                        Console.WriteLine($"API {api.Name} GetHistoricalTrades OK ({trades.Length})");
                        Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
                        Console.WriteLine($"API {api.Name} GetRecentTrades OK ({trades.Length} trades)");
                    }
                    catch (NotImplementedException)
                    {
                        if (api is ExchangeHuobiAPI || api is ExchangeBithumbAPI || api is ExchangeBitMEXAPI)
                        {
                            Console.WriteLine($"API {api.Name} GetHistoricalTrades/GetRecentTrades not implemented");
                        }
                        else
                        {
                            Console.WriteLine($"API {api.Name} GetHistoricalTrades/GetRecentTrades data invalid or empty");
                        }
                    }

                    try
                    {
                        var candles = api.GetCandles(symbol, 86400, DateTime.UtcNow.Subtract(TimeSpan.FromDays(7.0)), null).ToArray();
                        Assert(candles.Length != 0 && candles[0].ClosePrice > 0m && candles[0].HighPrice > 0m && candles[0].LowPrice > 0m && candles[0].OpenPrice > 0m &&
                            candles[0].HighPrice >= candles[0].LowPrice && candles[0].HighPrice >= candles[0].ClosePrice && candles[0].HighPrice >= candles[0].OpenPrice &&
                            !string.IsNullOrWhiteSpace(candles[0].Name) && candles[0].ExchangeName == api.Name && candles[0].PeriodSeconds == 86400 && candles[0].BaseVolume > 0.0 &&
                            candles[0].ConvertedVolume > 0.0 && candles[0].WeightedAverage >= 0m);

                        Console.WriteLine($"API {api.Name} GetCandles OK ({candles.Length})");
                    }
                    catch (NotImplementedException)
                    {
                        Console.WriteLine($"API {api.Name} GetCandles not implemented");
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
                catch (Exception ex)
                {
                    Console.WriteLine("Request failed, api: {0}, error: {1}", api, ex.Message);
                }
            }
        }

        public static void RunPerformTests(Dictionary<string, string> dict)
        {
            dict.TryGetValue("exchangeName", out string exchangeNameRegex);
            TestExchanges(exchangeNameRegex);
        }
    }
}

