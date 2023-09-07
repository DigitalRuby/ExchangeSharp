using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithPeriod
	{
		[Option('p', "period", Default = 1800, HelpText = "Period in seconds.")]
		int Period { get; set; }
	}
}
