using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("convert", HelpText =
		"Converts csv exchange data to bin files for optimized reading.\n" +
		"Files are converted in place and csv files are left as is.\n" +
		"Example: convert --symbol btcusd --path ../../data/gemini")]
	public class ConvertOption : BaseOption, IOptionWithOutput, IOptionPerSymbol
	{
		public string Symbol { get; set; }
		public string Path { get; set; }

		public override Task RunCommand()
		{
			TraderExchangeExport.ExportExchangeTrades(null, Symbol, Path, CryptoUtility.UtcNow);
			return Task.CompletedTask;
		}
	}
}
