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
using System.Runtime.CompilerServices;
using System.Threading;

using ExchangeSharp;

namespace ExchangeSharpConsoleApp
{
    public static partial class ExchangeSharpConsole
    {
        private static void RequireArgs(Dictionary<string, string> dict, params string[] args)
        {
            bool fail = false;
            foreach (string arg in args)
            {
                if (!dict.ContainsKey(arg))
                {
                    Console.WriteLine("Argument {0} is required.", arg);
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

        public static int ConsoleMain(string[] args)
        {
            try
            {
                Dictionary<string, string> dict = ParseCommandLine(args);
                if (dict.Count == 0 || dict.ContainsKey("help"))
                {
                    RunShowHelp(dict);
                }
                else if (dict.Count >= 1 && dict.ContainsKey("test"))
                {
                    RunPerformTests(dict);
                }
                else if (dict.Count >= 1 && dict.ContainsKey("export"))
                {
                    RunExportData(dict);
                }
                else if (dict.Count >= 1 && dict.ContainsKey("convert"))
                {
                    RunConvertData(dict);
                }
                else if (dict.Count >= 1 && dict.ContainsKey("stats"))
                {
                    RunShowExchangeStats(dict);
                }
                else if (dict.ContainsKey("example"))
                {
                    RunExample(dict);
                }
                else if (dict.ContainsKey("keys"))
                {
                    RunProcessEncryptedAPIKeys(dict);
                }
                else if (dict.ContainsKey("poloniex-websocket"))
                {
                    RunPoloniexWebSocket();
                }
                else if (dict.ContainsKey("bittrex-websocket"))
                {
                    RunBittrexWebSocket();
                }
                else
                {
                    Console.WriteLine("Unrecognized command line arguments.");
                    return -1;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal error: {0}", ex);
                return -99;
            }
        }
    }
}

