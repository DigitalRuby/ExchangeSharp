﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExchangeSharpConsole
{
	public static partial class ExchangeSharpConsoleMain
	{
		[Obsolete("Stop using this", false)]
		private static void RequireArgs(Dictionary<string, string> dict, params string[] args)
		{
			bool fail = false;
			foreach (string arg in args)
			{
				if (!dict.ContainsKey(arg))
				{
					Console.Error.WriteLine("Argument {0} is required.", arg);
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
			var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var a in args)
			{
				var arg = a.TrimStart('-');
				var idx = arg.IndexOf('=');
				var hasDivider = idx > 0;
				var key = (hasDivider ? arg.Substring(0, idx) : arg).ToLowerInvariant();
				var value = hasDivider ? arg.Substring(idx + 1) : string.Empty;
				dict[key] = value;
			}

			return dict;
		}

		/// <summary>
		/// Console app main method
		/// </summary>
		/// <param name="args">Args</param>
		/// <returns>Task</returns>
		public static Task<int> OldMain(string[] args)
		{
			return ExchangeSharpConsoleMain.ConsoleMain(args);
		}

		/// <summary>
		/// Console sub-main entry method
		/// </summary>
		/// <param name="args">Args</param>
		/// <returns>Task</returns>
		public static async Task<int> ConsoleMain(string[] args)
		{
			try
			{
				// swap out to external web socket implementation for older Windows pre 8.1
				// ExchangeSharp.ClientWebSocket.RegisterWebSocketCreator(() => new ExchangeSharpConsole.WebSocket4NetClientWebSocket());
				Console.WriteLine("ExchangeSharp console started.");
				Dictionary<string, string> argsDictionary = ParseCommandLine(args);
				if (argsDictionary.Count == 0 || argsDictionary.ContainsKey("help"))
				{
//					ShowHelp();
				}
				else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("test"))
				{
//					await RunPerformTests(argsDictionary);
				}
				else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("export"))
				{
//					await RunExportData(argsDictionary);
				}
				else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("convert"))
				{
//					await RunConvertData(argsDictionary);
				}
				else if (argsDictionary.Count >= 1 && argsDictionary.ContainsKey("stats"))
				{
//					await RunShowExchangeStats();
				}
				else if (argsDictionary.ContainsKey("example"))
				{
//					await RunExample();
				}
				else if (argsDictionary.ContainsKey("keys"))
				{
//					await RunProcessEncryptedAPIKeys(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("websocket-ticker"))
				{
					await RunWebSocketTickers(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("websocket-trades"))
				{
					await RunTradesWebSocket(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("websocket-orderbook"))
				{
//					await RunOrderBookWebSocket(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("getExchangeNames"))
				{
//					await ShowSupportedExchanges();
				}
				else if (argsDictionary.ContainsKey("showHistoricalTrades"))
				{
//					await RunGetHistoricalTrades(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("getOrderHistory"))
				{
//					await RunGetOrderHistoryAsync(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("getOrderDetails"))
				{
//					await RunGetOrderDetailsAsync(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("symbols-metadata"))
				{
//					await RunGetSymbolsMetadata(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("symbols"))
				{
//					await RunGetMarketSymbols(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("tickers"))
				{
//					await RunGetTickers(argsDictionary);
				}
				else if (argsDictionary.ContainsKey("candles"))
				{
//					await RunGetCandles(argsDictionary);
				}
				else
				{
					Console.Error.WriteLine("Unrecognized command line arguments.");
					return -1;
				}

				return 0;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				return -99;
			}
			finally
			{
				Console.WriteLine("ExchangeSharp console finished.");
			}
		}
	}
}
