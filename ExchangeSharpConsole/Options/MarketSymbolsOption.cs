using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("market-symbols", HelpText = "Shows all the market symbols (currency pairs) for the selected exchange.")]
	public class MarketSymbolsOption : BaseOption, IOptionPerExchange
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			try
			{
				var marketSymbols = await api.GetMarketSymbolsAsync();

				foreach (var marketSymbol in marketSymbols)
				{
					Console.WriteLine(marketSymbol);
				}

				WaitInteractively();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
			}
		}

		public string ExchangeName { get; set; }
	}
}
