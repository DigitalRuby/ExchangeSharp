using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithCurrency
	{
		[Option('c', "currency", Required = true, HelpText = "Currency, e.g. BTC.")]
		string Currency { get; set; }
	}
}
