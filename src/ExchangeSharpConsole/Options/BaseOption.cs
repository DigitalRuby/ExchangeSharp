using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using ExchangeSharp;
using ExchangeSharpConsole.Utilities;
#if DEBUG
using System.Diagnostics;
using CommandLine;

#endif

namespace ExchangeSharpConsole.Options
{
	public abstract class BaseOption
	{
#if DEBUG
		[Option("debug", HelpText = "Waits for a debugger to be attached.")]
		public bool Debug { get; set; }

		public async Task CheckDebugger()
		{
			if (!Debug)
			{
				return;
			}

			using (var proc = Process.GetCurrentProcess())
			{
				Console.Error.WriteLine($"Connect debugger to PID: {proc.Id}.");
				Console.Error.WriteLine("Waiting...");
			}

			while (!Debugger.IsAttached)
			{
				await Task.Delay(100);
			}
		}
#endif
		public bool IsInteractiveSession
			=> !(Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected);

		public abstract Task RunCommand();

		protected void Authenticate(IExchangeAPI api)
		{
			Console.Write("Enter Public Api Key: ");
			var publicApiKey = GetSecureInput();
			api.PublicApiKey = publicApiKey;
			Console.WriteLine();
			Console.Write("Enter Private Api Key: ");
			var privateApiKey = GetSecureInput();
			api.PrivateApiKey = privateApiKey;
			Console.WriteLine();
			Console.Write("Enter Passphrase: ");
			var passphrase = GetSecureInput();
			api.Passphrase = passphrase;
			Console.WriteLine();
		}

		protected SecureString GetSecureInput()
		{
			if (!IsInteractiveSession)
			{
				throw new NotSupportedException(
					"It still not supported to read secure input without a interactive terminal session."
				);
			}

			var pwd = new SecureString();
			while (true)
			{
				var i = Console.ReadKey(true);
				if (i.Key == ConsoleKey.Enter)
				{
					break;
				}

				if (i.Key == ConsoleKey.Backspace)
				{
					if (pwd.Length <= 0)
						continue;

					pwd.RemoveAt(pwd.Length - 1);
					Console.Write("\b \b");
				}
				// KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
				else if (i.KeyChar != '\u0000')
				{
					pwd.AppendChar(i.KeyChar);
					Console.Write("*");
				}
			}

			return pwd;
		}

		protected void Assert(bool expression)
		{
			if (!expression)
			{
				throw new ApplicationException("Test failure, unexpected result");
			}
		}

		protected void WaitInteractively()
		{
			if (!IsInteractiveSession)
			{
				Console.Error.WriteLine("Ignoring interactive action.");
				return;
			}

			Console.Write("Press any key to continue...");
			Console.ReadKey(true);
			Console.CursorLeft = 0;
		}

		protected IExchangeAPI GetExchangeInstance(string exchangeName)
		{
			return ExchangeAPI.GetExchangeAPI(exchangeName);
		}

		protected async Task RunWebSocket(string exchangeName, Func<IExchangeAPI, Task<IWebSocket>> getWebSocket)
		{
			using var api = ExchangeAPI.GetExchangeAPI(exchangeName);

			Console.WriteLine("Connecting web socket to {0}...", api.Name);

			IWebSocket socket = null;
			var tcs = new TaskCompletionSource<bool>();

			// ReSharper disable once AccessToModifiedClosure
			var disposable = KeepSessionAlive(() =>
			{
				socket?.Dispose();
				tcs.TrySetResult(true);
			});

			try
			{
				socket = await getWebSocket(api);

				socket.Connected += _ =>
				{
					Console.WriteLine("Web socket connected.");
					return Task.CompletedTask;
				};
				socket.Disconnected += _ =>
				{
					Console.WriteLine("Web socket disconnected.");

					// ReSharper disable once AccessToDisposedClosure
					disposable.Dispose();

					return Task.CompletedTask;
				};

				await tcs.Task;
			}
			catch
			{
				disposable.Dispose();
				throw;
			}
		}


		/// <summary>
		/// Makes the app keep running after main thread has exited
		/// </summary>
		/// <param name="callback">A callback for when the user press CTRL-C or Q</param>
		protected static IDisposable KeepSessionAlive(Action callback = null)
			=> new ConsoleSessionKeeper(callback);

		protected static async Task<string[]> ValidateMarketSymbolsAsync(IExchangeAPI api, string[] marketSymbols)
		{
			var apiSymbols = (await api.GetMarketSymbolsAsync()).ToArray();

			if (marketSymbols is null || marketSymbols.Length == 0)
			{
				return apiSymbols;
			}

			return ValidateMarketSymbolsInternal(api, marketSymbols, apiSymbols)
				.ToArray();
		}

		private static IEnumerable<string> ValidateMarketSymbolsInternal(
			IExchangeAPI api,
			string[] marketSymbols,
			string[] apiSymbols
		)
		{
			foreach (var marketSymbol in marketSymbols)
			{
				var apiSymbol = apiSymbols.FirstOrDefault(
					a => a.Equals(marketSymbol, StringComparison.OrdinalIgnoreCase)
				);

				if (!string.IsNullOrWhiteSpace(apiSymbol))
				{
					//return this for proper casing
					yield return apiSymbol;
					continue;
				}

				var validSymbols = string.Join(",", apiSymbols.OrderBy(s => s));

				throw new ArgumentException(
					$"Symbol {marketSymbol} does not exist in API {api.Name}.\n" +
					$"Valid symbols: {validSymbols}"
				);
			}
		}

		protected void PrintOrderBook(ExchangeOrderBook orderBook)
		{
			Console.WriteLine($"{"BID",-22} | {"ASK",22}");

			Console.WriteLine("---");

			var length = orderBook.Asks.Count;

			if (orderBook.Bids.Count > length)
			{
				length = orderBook.Bids.Count;
			}

			for (var i = 0; i < length; i++)
			{
				var (_, ask) = orderBook.Asks.ElementAtOrDefault(i);
				var (_, bid) = orderBook.Bids.ElementAtOrDefault(i);
				Console.WriteLine($"{bid.Price,10} ({bid.Amount,9:N2}) | " +
				                  $"{ask.Price,10} ({ask.Amount,9:N})");
			}
		}
	}
}
