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

using ExchangeSharp;

namespace ExchangeSharpConsoleApp
{
	public static partial class ExchangeSharpConsole
    {
        public static void RunExample(Dictionary<string, string> dict)
        {
            ExchangeKrakenAPI api = new ExchangeKrakenAPI();
            ExchangeTicker ticker = api.GetTicker("XXBTZUSD");
            Console.WriteLine("On the Kraken exchange, 1 bitcoin is worth {0} USD.", ticker.Bid);

            // load API keys created from ExchangeSharpConsole.exe keys mode=create path=keys.bin keylist=public_key,private_key
            api.LoadAPIKeys("keys.bin");

            /// place limit order for 0.01 bitcoin at ticker.Ask USD
            ExchangeOrderResult result = api.PlaceOrder(new ExchangeOrderRequest
            {
                Amount = 0.01m,
                IsBuy = true,
                Price = ticker.Ask,
                Symbol = "XXBTZUSD"
            });

            // Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
            //  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
            // I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
            //  their house in order.
            System.Threading.Thread.Sleep(500);
            result = api.GetOrderDetails(result.OrderId);

            Console.WriteLine("Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.", ticker.Ask, result.Result, result.OrderId);
        }

        public static void RunPoloniexWebSocket()
        {
            var api = new ExchangePoloniexAPI();
            var wss = api.GetTickersWebSocket((t) =>
            {
                // depending on the exchange, the (t) parameter (a collection of tickers) may have one ticker or all of them
                foreach (var ticker in t)
                {
                    Console.WriteLine(ticker);
                }
            });
            Console.WriteLine("Press any key to quit.");
            Console.ReadKey();
            wss.Dispose();
        }

        private static void RunBittrexWebSocket()
        {
            var bittrex = new ExchangeBittrexAPI();
            IDisposable bitSocket = bittrex.GetTickersWebSocket(freshTickers =>
            {
                foreach (KeyValuePair<string, ExchangeTicker> kvp in freshTickers)
                {
                    Console.WriteLine($"market {kvp.Key}, ticker {kvp.Value}");
                }
            });

            Console.WriteLine("Press any key to quit.");
            Console.ReadKey();
            bitSocket.Dispose();
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
                    Console.WriteLine(CryptoUtility.SecureStringToString(s));
                }
            }
            else
            {
                throw new ArgumentException("Invalid mode: " + dict["mode"]);
            }
        }
    }
}
