using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
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
					Logger.Info(marketSymbol);
				}

				WaitInteractively();
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

		public string ExchangeName { get; set; }
	}
}
