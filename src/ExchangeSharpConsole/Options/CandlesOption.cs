using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("candles", HelpText = "Prints all candle data from a 12 days period for the given exchange.")]
	public class CandlesOption : BaseOption, IOptionPerExchange, IOptionPerMarketSymbol
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			var candles = await api.GetCandlesAsync(
				MarketSymbol,
				1800,
				//TODO: Add interfaces for start and end date
				CryptoUtility.UtcNow.AddDays(-12),
				CryptoUtility.UtcNow
			);

			foreach (var candle in candles)
			{
				Console.WriteLine(candle.ToString());
			}

			WaitInteractively();
		}

		public string ExchangeName { get; set; }

		public string MarketSymbol { get; set; }
	}
}
