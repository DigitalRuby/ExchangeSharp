using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithMarketSymbol
	{
		[Option('s', "symbol",
			HelpText = "Symbol (currency pair) to be fetched from the exchange.")]
		string MarketSymbol { get; set; }
	}
}
