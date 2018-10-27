/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/


using System;
using System.Collections.Generic;

namespace ExchangeSharpConsole
{
	public static partial class ExchangeSharpConsoleMain
    {
        public static void RunShowHelp(Dictionary<string, string> dict)
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("ExchangeSharpConsole v. {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("Command line arguments should be key=value pairs, separated by space. Please add quotes around any key=value pair with a space in it.");
            Console.WriteLine();
            Console.WriteLine("Command categories:");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("help - Show this help screen, or just run without arguments to show help as well.");
            Console.WriteLine();
            Console.WriteLine("test - Run integrations test code against exchanges.");
            Console.WriteLine(" exchangeName - regex of exchanges to test, null/empty for all");
            Console.WriteLine();
            Console.WriteLine("export - export exchange data. CSV files have millisecond timestamp, price and amount columns. The export will also convert the CSV to bin files. This can take a long time depending on your sinceDateTime parameter.");
            Console.WriteLine(" Please note that not all exchanges will let you do this and may ban your IP if you try to grab to much data at once. I've added sensible sleep statements to limit request rates.");
            Console.WriteLine(" export exchange=gemini symbol=btcusd path=../../data/gemini sinceDateTime=20150101");
            Console.WriteLine();
            Console.WriteLine("convert - convert csv exchange data to bin files for optimized reading. Files are converted in place and csv files are left as is.");
            Console.WriteLine(" convert symbol=btcusd path=../../data/gemini");
            Console.WriteLine();
            Console.WriteLine("stats - show stats from all exchanges. This is a great way to see the price, order book and other useful stats.");
            Console.WriteLine(" stats currently has no additional arguments.");
            Console.WriteLine();
            Console.WriteLine("keys - encrypted API key file utility - this file is only valid for the current user and only on the computer it is created on.");
            Console.WriteLine(" Create a key file:");
            Console.WriteLine("  keys mode=create path=pathToKeyFile.bin keylist=key1,key2,key3,key4,etc.");
            Console.WriteLine("  The keys parameter is comma separated and may contain any number of keys in any order.");
            Console.WriteLine(" Display a key file:");
            Console.WriteLine("  keys mode=display path=pathToKeyFile.bin");               
            Console.WriteLine();
            Console.WriteLine("showHistoricalTrades - output historical trades to console");
            Console.WriteLine(" showHistoricalTrades exchangeName=Binance symbol=btcusdt \"startDate=2018-05-17T11:00:00\" \"endDate=2018-05-17T12:00:00\"");
            Console.WriteLine(" startDate and endDate are optional.");
            Console.WriteLine();
            Console.WriteLine("getExchangeNames - get a list of all supported exchange names (no arguments)");
            Console.WriteLine();
            Console.WriteLine("example - simple example showing how to create an API instance and get the ticker, and place an order.");
            Console.WriteLine(" example currently has no additional arguments.");
            Console.WriteLine();
            Console.WriteLine("websocket-ticker - Shows how to connect via web socket and listen to tickers.");
            Console.WriteLine(" websocket-ticker exchangeName=Binance");
            Console.WriteLine();
            Console.WriteLine("websocket-trades - Shows how to connect via web socket and listen to trades.");
            Console.WriteLine(" websocket-trades exchangeName=Binance symbols=btcusdt,ethbtc");
            Console.WriteLine(" symbols is optional, if not provided or empty, all symbols will be queried");
            Console.WriteLine();
            Console.WriteLine("websocket-orderbook - Shows how to connect via web socket and listen to the order book.");
            Console.WriteLine(" websocket-orderbook exchangeName=Binance symbols=btcusdt,ethbtc");
            Console.WriteLine(" symbols is optional, if not provided or empty, all symbols will be queried");
            Console.WriteLine();
        }
    }
}
