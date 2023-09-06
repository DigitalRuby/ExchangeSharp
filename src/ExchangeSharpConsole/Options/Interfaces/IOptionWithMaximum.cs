using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithMaximum
	{
		[Option("max", Default = 10, HelpText = "Sets the maximum.")]
		int Max { get; set; }
	}
}
