/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using ExchangeSharp;

namespace ExchangeSharpConsole
{
    public static partial class ExchangeSharpConsoleMain
    {
        public static int Main(string[] args)
        {
            return ExchangeSharpConsoleMain.ConsoleMain(args);
        }

        private static void RequireArgs(Dictionary<string, string> dict, params string[] args)
        {
            bool fail = false;
            foreach (string arg in args)
            {
                if (!dict.ContainsKey(arg))
                {
                    Logger.Error("Argument {0} is required.", arg);
                    fail = true;
                }
            }
            if (fail)
            {
                throw new ArgumentException("Missing required arguments");
            }
        }

        private static Dictionary<string, string> ParseCommandLine(string[] args)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string a in args)
            {
                int idx = a.IndexOf('=');
                string key = (idx < 0 ? a.Trim('-') : a.Substring(0, idx)).ToLowerInvariant();
                string value = (idx < 0 ? string.Empty : a.Substring(idx + 1));
                dict[key] = value;
            }
            return dict;
        }

        private static void TestMethod()
        {
        }

        public static int ConsoleMain(string[] args)
        {
            try
            {
                // swap out to external web socket implementation for older Windows pre 8.1
                // ExchangeSharp.ClientWebSocket.RegisterWebSocketCreator(() => new ExchangeSharpConsole.WebSocket4NetClientWebSocket());
                // TestMethod(); return 0; // uncomment for ad-hoc code testing

                Logger.Info("ExchangeSharp console started.");
                Dictionary<string, string> argsDictionary = ParseCommandLine(args);
                if (argsDictionary.Count == 0 || argsDictionary.ContainsKey("help"))
                {
                    RunShowHelp(argsDictionary);
                }
                else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("test"))
                {
                    RunPerformTests(argsDictionary);
                }
                else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("export"))
                {
                    RunExportData(argsDictionary);
                }
                else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("convert"))
                {
                    RunConvertData(argsDictionary);
                }
                else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("stats"))
                {
                    RunShowExchangeStats(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("example"))
                {
                    RunExample(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("keys"))
                {
                    RunProcessEncryptedAPIKeys(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("websocket-ticker"))
                {
                    RunWebSocketTickers(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("websocket-trades"))
                {
                    RunTradesWebSocket(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("websocket-orderbook"))
                {
                    RunOrderBookWebSocket(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("getExchangeNames"))
                {
                    Console.WriteLine("Supported exchanges: {0}", string.Join(", ", ExchangeName.ExchangeNames));
                }
                else if (argsDictionary.ContainsKey("showHistoricalTrades"))
                {
                    RunGetHistoricalTrades(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("getOrderHistory"))
                {
                    RunGetOrderHistory(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("getOrderDetails"))
                {
                    RunGetOrderDetails(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("symbols-metadata"))
                {
                    RunGetSymbolsMetadata(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("symbols"))
                {
                    RunGetMarketSymbols(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("tickers"))
                {
                    RunGetTickers(argsDictionary);
                }
                else if (argsDictionary.ContainsKey("candles"))
                {
                    RunGetCandles(argsDictionary);
                }
                else
                {
                    Logger.Error("Unrecognized command line arguments.");
                    return -1;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return -99;
            }
            finally
            {
                Logger.Info("ExchangeSharp console finished.");
            }
        }
    }
}
