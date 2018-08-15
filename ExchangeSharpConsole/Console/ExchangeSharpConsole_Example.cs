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
        public static void RunExample(Dictionary<string, string> dict)
        {
            ExchangeKrakenAPI api = new ExchangeKrakenAPI();
            ExchangeTicker ticker = api.GetTickerAsync("XXBTZUSD").Sync();
            Console.WriteLine("On the Kraken exchange, 1 bitcoin is worth {0} USD.", ticker.Bid);

            // load API keys created from ExchangeSharpConsole.exe keys mode=create path=keys.bin keylist=public_key,private_key
            api.LoadAPIKeys("keys.bin");

            /// place limit order for 0.01 bitcoin at ticker.Ask USD
            ExchangeOrderResult result = api.PlaceOrderAsync(new ExchangeOrderRequest
            {
                Amount = 0.01m,
                IsBuy = true,
                Price = ticker.Ask,
                Symbol = "XXBTZUSD"
            }).Sync();

            // Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
            //  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
            // I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
            //  their house in order.
            System.Threading.Thread.Sleep(500);
            result = api.GetOrderDetailsAsync(result.OrderId).Sync();

            Console.WriteLine("Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.", ticker.Ask, result.Result, result.OrderId);
        }

        private static string[] GetSymbols(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "symbols");
            if (dict["symbols"] == "*")
            {
                return null;
            }
            return dict["symbols"].Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] ValidateSymbols(IExchangeAPI api, string[] symbols)
        {
            string[] apiSymbols = api.GetSymbolsAsync().Sync().ToArray();
            if (symbols == null || symbols.Length == 0)
            {
                symbols = apiSymbols;
            }
            foreach (string symbol in symbols)
            {
                if (!apiSymbols.Contains(symbol))
                {
                    throw new ArgumentException(string.Format("Symbol {0} does not exist in API {1}, valid symbols: {2}", symbol, api.Name, string.Join(",", apiSymbols.OrderBy(s => s))));
                }
            }
            return symbols;
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

        private static void RunWebSocket(Dictionary<string, string> dict, Func<IExchangeAPI, IWebSocket> func)
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
                    using (var socket = func(api))
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

        private static void RunWebSocketTickers(Dictionary<string, string> dict)
        {
            RunWebSocket(dict, (api) => api.GetTickersWebSocket(freshTickers =>
            {
                foreach (KeyValuePair<string, ExchangeTicker> kvp in freshTickers)
                {
                    Console.WriteLine($"market {kvp.Key}, ticker {kvp.Value}");
                }
            }));
        }

        private static void RunTradesWebSocket(Dictionary<string, string> dict)
        {
            string[] symbols = GetSymbols(dict);
            RunWebSocket(dict, (api) =>
            {
                symbols = ValidateSymbols(api, symbols);
                return api.GetTradesWebSocket(message =>
                {
                    Console.WriteLine($"{message.Key}: {message.Value}");
                }, symbols);
            });
        }

        private static void RunOrderBookWebSocket(Dictionary<string, string> dict)
        {
            string[] symbols = GetSymbols(dict);
            RunWebSocket(dict, (api) =>
            {
                symbols = ValidateSymbols(api, symbols);
                return api.GetOrderBookWebSocket(message =>
                {
                    //print the top bid and ask with amount
                    var topBid = message.Bids.FirstOrDefault();
                    var topAsk = message.Asks.FirstOrDefault();
                    Console.WriteLine($"[{message.Symbol}:{message.SequenceId}] {topBid.Value.Price} ({topBid.Value.Amount}) | {topAsk.Value.Price} ({topAsk.Value.Amount})");
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
                    Console.WriteLine(CryptoUtility.ToUnsecureString(s));
                }
            }
            else
            {
                throw new ArgumentException("Invalid mode: " + dict["mode"]);
            }
        }
    }
}
