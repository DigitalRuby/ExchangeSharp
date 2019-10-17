using System;
using System.Security;
using System.Threading.Tasks;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	public abstract class BaseOption
	{
		public abstract Task RunCommand();

		protected void Authenticate(IExchangeAPI api)
		{
			ExchangeSharpConsoleMain.Authenticate(api);
		}

		protected SecureString GetSecureInput()
		{
			return ExchangeSharpConsoleMain.GetSecureInput();
		}

		protected void WaitInteractively()
		{
			//TODO: Implement check to ignore interactive actions when the console doesn't support it
			Console.Write("Press enter to continue...");
			Console.ReadLine();
		}

		protected IExchangeAPI GetExchangeInstance(string exchangeName)
		{
			return ExchangeAPI.GetExchangeAPI(exchangeName);
		}
	}
}
