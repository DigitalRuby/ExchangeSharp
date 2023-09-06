using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace ExchangeSharpConsole.Utilities
{
	public class ConsoleSessionKeeper : IDisposable
	{
		private readonly Action callback;
		private readonly Thread threadCheckKey;
		private bool shouldStop;
		private readonly bool previousConsoleCtrlCConfig;

		public ConsoleSessionKeeper(Action callback = null)
		{
			this.callback = callback;
			previousConsoleCtrlCConfig = Console.TreatControlCAsInput;
			Console.TreatControlCAsInput = false;

			Console.WriteLine("Press CTRL-C or Q to quit");

			threadCheckKey = new Thread(CheckKeyCombination)
			{
				Name = $"console-waiter-{callback?.Method.Name}",
				IsBackground = false
			};

			Console.CancelKeyPress += OnConsoleOnCancelKeyPress;

			threadCheckKey.Start();
		}

		private void CheckKeyCombination()
		{
			using var stdin = Console.OpenStandardInput();
			var charArr = new byte[2];

			while (stdin.Read(charArr, 0, 2) > 0)
			{
				if (shouldStop)
					return;

				var c = Encoding.UTF8.GetChars(charArr)[0];
				if (c == 'q' || c == 'Q')
					break;
			}

			if (shouldStop)
				return;

			Debug.WriteLine("Q pressed.");
			Dispose();
		}

		private void OnConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
		{
			Debug.WriteLine("CTRL-C pressed.");
			args.Cancel = true;
			Dispose();
		}

		public void Dispose()
		{
			if (shouldStop)
				return;

			callback?.Invoke();
			Console.CancelKeyPress -= OnConsoleOnCancelKeyPress;
			Console.TreatControlCAsInput = previousConsoleCtrlCConfig;
			// this does not work on .net core
			// threadCheckKey.Abort();
			shouldStop = true;
		}
	}
}
