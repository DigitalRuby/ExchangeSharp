using System;
using System.Diagnostics;
using System.Threading;

namespace ExchangeSharpConsole.Utilities
{
	public class ConsoleSessionKeeper : IDisposable
	{
		private readonly Action callback;
		private readonly Thread threadCheckKey;
		private bool shouldStop;

		public ConsoleSessionKeeper(Action callback = null)
		{
			this.callback = callback;

			Console.WriteLine("Press CTRL-C or Q to quit");

			threadCheckKey = new Thread(CheckKeyCombination)
			{
				Name = "console-waiter",
				IsBackground = false
			};

			Console.CancelKeyPress += OnConsoleOnCancelKeyPress;

			threadCheckKey.Start();
		}

		private void CheckKeyCombination()
		{
			ConsoleKeyInfo cki;
			do
			{
				while (Console.KeyAvailable == false)
				{
					if (shouldStop)
					{
						return;
					}

					Thread.Sleep(100);
					Thread.Yield();
				}

				cki = Console.ReadKey(true);
			} while (!(cki.Key == ConsoleKey.Q || cki.Key == ConsoleKey.Escape));

			Debug.WriteLine("Q pressed.");
			callback?.Invoke();
			Dispose();
		}

		private void OnConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
		{
			Debug.WriteLine("CTRL-C pressed.");
			args.Cancel = true;
			callback?.Invoke();
			Dispose();
		}

		public void Dispose()
		{
			if (shouldStop)
				return;

			Console.CancelKeyPress -= OnConsoleOnCancelKeyPress;
			// this does not work on .net core
			// threadCheckKey.Abort();
			shouldStop = true;
		}
	}
}
