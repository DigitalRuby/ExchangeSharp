using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("stats", HelpText = "Show stats from 4 exchanges. " +
	                          "This is a great way to see the price, order book and other useful stats.")]
	public class StatsOption : BaseOption, IOptionWithInterval
	{
		public override async Task RunCommand()
		{
			await ExchangeSharpConsoleMain.RunShowExchangeStats(IntervalMs);
		}

		public int IntervalMs { get; set; }
	}
}
