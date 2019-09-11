/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ExchangeSharp;

namespace ExchangeSharpConsole
{
	public static partial class ExchangeSharpConsoleMain
    {
        public static async Task RunExample(Dictionary<string, string> dict)
        {
            ExchangeKrakenAPI api = new ExchangeKrakenAPI();
            ExchangeTicker ticker = await api.GetTickerAsync("XXBTZUSD");
            Logger.Info("On the Kraken exchange, 1 bitcoin is worth {0} USD.", ticker.Bid);

            // load API keys created from ExchangeSharpConsole.exe keys mode=create path=keys.bin keylist=public_key,private_key
            api.LoadAPIKeys("keys.bin");

            /// place limit order for 0.01 bitcoin at ticker.Ask USD
            ExchangeOrderResult result = await api.PlaceOrderAsync(new ExchangeOrderRequest
            {
                Amount = 0.01m,
                IsBuy = true,
                Price = ticker.Ask,
                MarketSymbol = "XXBTZUSD"
            });

            // Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
            //  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
            // I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
            //  their house in order.
            await Task.Delay(500);
            result = await api.GetOrderDetailsAsync(result.OrderId);

            Logger.Info("Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.", ticker.Ask, result.Result, result.OrderId);
        }

        private static void WaitForKey()
        {
            Console.WriteLine("Press any key to quit.");
            Console.ReadKey();
        }

        private static string[] GetMarketSymbols(Dictionary<string, string> dict, bool required = true)
        {
            if (required)
            {
                RequireArgs(dict, "marketSymbols");
            }
            if ((!dict.ContainsKey("marketSymbols") && !required) || dict["marketSymbols"] == "*")
            {
                return null;
            }
            return dict["marketSymbols"].Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] ValidateMarketSymbols(IExchangeAPI api, string[] marketSymbols)
        {
            string[] apiSymbols = api.GetMarketSymbolsAsync().Sync().ToArray();
            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = apiSymbols;
            }
            foreach (string marketSymbol in marketSymbols)
            {
                if (!apiSymbols.Contains(marketSymbol))
                {
                    throw new ArgumentException(string.Format("Symbol {0} does not exist in API {1}, valid symbols: {2}", marketSymbol, api.Name, string.Join(",", apiSymbols.OrderBy(s => s))));
                }
            }
            return marketSymbols;
        }

        private static void SetWebSocketEvents(IWebSocket socket)
        {
            socket.Connected += (s) =>
            {
                Logger.Info("Web socket connected");
                return Task.CompletedTask;
            };
            socket.Disconnected += (s) =>
            {
                Logger.Info("Web socket disconnected");
                return Task.CompletedTask;
            };
        }

        private static async Task RunWebSocket(Dictionary<string, string> dict, Func<IExchangeAPI, Task<IWebSocket>> func)
        {
            RequireArgs(dict, "exchangeName");
            using (var api = ExchangeAPI.GetExchangeAPI(dict["exchangeName"]))
            {
                if (api == null)
                {
                    throw new ArgumentException("Cannot find exchange with name {0}", dict["exchangeName"]);
                }
                try
                {
                    Logger.Info("Connecting web socket to {0}...", api.Name);
                    using (var socket = await func(api))
                    {
                        SetWebSocketEvents(socket);
                        Console.WriteLine("Press any key to quit.");
                        Console.ReadKey();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        private static async Task RunWebSocketTickers(Dictionary<string, string> dict)
        {
            string[] symbols = GetMarketSymbols(dict, false);
            await RunWebSocket(dict, (api) =>
            {
                if (symbols != null)
                {
                    symbols = ValidateMarketSymbols(api, symbols);
                }
                return api.GetTickersWebSocket(freshTickers =>
                {
                    foreach (KeyValuePair<string, ExchangeTicker> kvp in freshTickers)
                    {
                        Logger.Info($"market {kvp.Key}, ticker {kvp.Value}");
                    }
                }, symbols);
            });
        }

        private static async Task RunTradesWebSocket(Dictionary<string, string> dict)
        {
            string[] symbols = GetMarketSymbols(dict);
            await RunWebSocket(dict, (api) =>
            {
                symbols = ValidateMarketSymbols(api, symbols);
                return api.GetTradesWebSocket(message =>
                {
                    Logger.Info($"{message.Key}: {message.Value}");
                    return Task.CompletedTask;
                }, symbols);
            });
        }

        private static async Task RunOrderBookWebSocket(Dictionary<string, string> dict)
        {
            string[] symbols = GetMarketSymbols(dict);
            await RunWebSocket(dict, (api) =>
            {
                symbols = ValidateMarketSymbols(api, symbols);
                return ExchangeAPIExtensions.GetFullOrderBookWebSocket(api, message =>
                {
                   //print the top bid and ask with amount
                   var topBid = message.Bids.FirstOrDefault();
                   var topAsk = message.Asks.FirstOrDefault();
                   Logger.Info($"[{message.MarketSymbol}:{message.SequenceId}] {topBid.Value.Price} ({topBid.Value.Amount}) | {topAsk.Value.Price} ({topAsk.Value.Amount})");
                }, symbols: symbols);
            });
        }

        public static void RunProcessEncryptedAPIKeys(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "path", "mode");
            if (dict["mode"].Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                RequireArgs(dict, "keylist");
                CryptoUtility.SaveUnprotectedStringsToFile(dict["path"], dict["keylist"].Split(','));
            }
            else if (dict["mode"].Equals("display", StringComparison.OrdinalIgnoreCase))
            {
                System.Security.SecureString[] secureStrings = CryptoUtility.LoadProtectedStringsFromFile(dict["path"]);
                foreach (System.Security.SecureString s in secureStrings)
                {
                    Logger.Info(CryptoUtility.ToUnsecureString(s));
                }
            }
            else
            {
                throw new ArgumentException("Invalid mode: " + dict["mode"]);
            }
        }

        public static async Task RunGetSymbolsMetadata(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "exchangeName");
            using (var api = ExchangeAPI.GetExchangeAPI(dict["exchangeName"]))
            {
                if (api == null)
                {
                    throw new ArgumentException("Cannot find exchange with name {0}", dict["exchangeName"]);
                }

                try
                {
                    var marketSymbols = await api.GetMarketSymbolsMetadataAsync();

                    foreach (var marketSymbol in marketSymbols)
                    {
                        Logger.Info(marketSymbol.ToString());
                    }

                    Console.WriteLine("Press any key to quit.");
                    Console.ReadKey();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        public static async Task RunGetMarketSymbols(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "exchangeName");
            using (var api = ExchangeAPI.GetExchangeAPI(dict["exchangeName"]))
            {
                if (api == null)
                {
                    throw new ArgumentException("Cannot find exchange with name {0}", dict["exchangeName"]);
                }

                try
                {
                    var marketSymbols = await api.GetMarketSymbolsAsync();

                    foreach (var marketSymbol in marketSymbols)
                    {
                        Logger.Info(marketSymbol);
                    }

                    WaitForKey();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        public static async Task RunGetTickers(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "exchangeName");
            using (var api = ExchangeAPI.GetExchangeAPI(dict["exchangeName"]))
            {
                if (api == null)
                {
                    throw new ArgumentException("Cannot find exchange with name {0}", dict["exchangeName"]);
                }

                try
                {
                    IEnumerable<KeyValuePair<string, ExchangeTicker>> tickers;
                    if (dict.ContainsKey("marketSymbol"))
                    {
                        var marketSymbol = dict["marketSymbol"];
                        var ticker = await api.GetTickerAsync(marketSymbol);
                        tickers = new List<KeyValuePair<string, ExchangeTicker>>()
                        {
                            new KeyValuePair<string, ExchangeTicker>(marketSymbol, ticker)
                        };
                    }
                    else
                    {
                        tickers = await api.GetTickersAsync();
                    }
                    
                    foreach (var ticker in tickers)
                    {
                        Logger.Info(ticker.ToString());
                    }

                    WaitForKey();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        public static async Task RunGetCandles(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "exchangeName", "marketSymbol");
            using (var api = ExchangeAPI.GetExchangeAPI(dict["exchangeName"]))
            {
                if (api == null)
                {
                    throw new ArgumentException("Cannot find exchange with name {0}", dict["exchangeName"]);
                }

                try
                {
                    var marketSymbol = dict["marketSymbol"];
                    var candles = await api.GetCandlesAsync(marketSymbol, 1800, CryptoUtility.UtcNow.AddDays(-12), CryptoUtility.UtcNow);
                    
                    foreach (var candle in candles)
                    {
                        Logger.Info(candle.ToString());
                    }

                    WaitForKey();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }
    }
}
