using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("market-symbols-metadata", HelpText = "Prints the metadata for all market symbols for the given exchange.")]
	public class MarketSymbolsMetadataOption : BaseOption, IOptionPerExchange
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			var marketSymbols = await api.GetMarketSymbolsMetadataAsync();

			foreach (var marketSymbol in marketSymbols)
			{
				Console.WriteLine(marketSymbol.ToString());
			}

			WaitInteractively();
		}

		public string ExchangeName { get; set; }
	}
}
