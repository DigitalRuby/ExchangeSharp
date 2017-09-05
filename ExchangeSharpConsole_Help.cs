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

namespace ExchangeSharpConsole
{
    public partial class ExchangeSharpConsoleApp
    {
        private static void RunShowHelp(Dictionary<string, string> dict)
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("ExchangeSharpConsole v. {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("Command line arguments should be key=value pairs, separated by space. Please add quotes around any key/value pair with a space in it.");
            Console.WriteLine();
            Console.WriteLine("Command categories:");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("help - Show this help screen, or just run without arguments to show help as well.");
            Console.WriteLine();
            Console.WriteLine("test - Run test code.");
            Console.WriteLine("test currently has no additional arguments.");
            Console.WriteLine();
            Console.WriteLine("export - export exchange data. CSV files have millisecond timestamp, price and amount columns. The export will also convert the CSV to bin files. This can take a long time depending on your sinceDateTime parameter.");
            Console.WriteLine("export exchange=gemini symbol=btcusd path=../../data/gemini sinceDateTime=20150101");
            Console.WriteLine();
            Console.WriteLine("convert - convert csv exchange data to bin files for optimized reading. Files are converted in place and csv files are left as is.");
            Console.WriteLine("convert symbol=btcusd path=../../data/gemini");
            Console.WriteLine();
            Console.WriteLine("stats - show stats from all exchanges. This is a great way to see the price, order book and other useful stats.");
            Console.WriteLine("stats currently has no additional arguments.");
        }
    }
}
