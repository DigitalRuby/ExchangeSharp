using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionPerMarketSymbol
	{
		[Option('s', "symbol", Required = true,
			HelpText = "Symbol (currency pair) to be fetched from the exchange.")]
		string MarketSymbol { get; set; }
	}
}
