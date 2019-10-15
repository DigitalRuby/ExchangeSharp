using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithInterval
	{
		[Option('i', "interval", Default = 5000,
			HelpText = "Interval to fetch data in milliseconds.")]
		int IntervalMs { get; set; }
	}
}
