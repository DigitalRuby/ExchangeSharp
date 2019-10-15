using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionPerSymbol
	{
		[Option('s', "symbol", Required = true,
			HelpText = "Symbol (currency pair) to be fetched from the exchange. " +
			           "(Can be multiple in a comma-separated-list, e.g. -s=btceur,ltceur )",
			Separator = '=')]
		string Symbol { get; set; }
	}
}
