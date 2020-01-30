using System;
using System.Globalization;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("export", HelpText =
		"Export exchange data. CSV files have millisecond timestamp, price and amount columns. " +
		"The export will also convert the CSV to bin files. " +
		"This can take a long time depending on your sinceDateTime parameter.\n" +
		"Please note that not all exchanges will let you do this and may ban your IP if you try to grab to much data at once. " +
		"I've added sensible sleep statements to limit request rates.\n" +
		"Example: export -e gemini --since 2015-01-01 -s btcusd -p \"./data/gemini\"")]
	public class ExportOption : BaseOption, IOptionPerExchange, IOptionPerMarketSymbol, IOptionWithIO,
		IOptionWithStartDate
	{
		public override Task RunCommand()
		{
			long total = 0;

			TraderExchangeExport.ExportExchangeTrades(
				ExchangeAPI.GetExchangeAPI(ExchangeName),
				MarketSymbol,
				Path,
				DateTime.Parse(SinceDateString, CultureInfo.InvariantCulture),
				count =>
				{
					total = count;
					Console.WriteLine($"Exporting {ExchangeName}: {total}");
				}
			);

			Console.WriteLine($"Finished Exporting {ExchangeName}: {total}");

			return Task.CompletedTask;
		}

		public string ExchangeName { get; set; }

		public string MarketSymbol { get; set; }

		public string Path { get; set; }

		public string SinceDateString { get; set; }
	}
}
