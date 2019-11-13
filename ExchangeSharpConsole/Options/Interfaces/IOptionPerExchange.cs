using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionPerExchange
	{
		[Option('e', "exchanges", Required = true, HelpText = "Regex of exchanges to test, null/empty for all.")]
		string ExchangeName { get; set; }
	}
}
