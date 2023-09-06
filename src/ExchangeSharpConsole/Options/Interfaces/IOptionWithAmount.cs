using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithCurrencyAmount : IOptionWithCurrency
	{
		[Option('a', "amount", Required = true, HelpText = "Amount of the currency.")]
		decimal Amount { get; set; }
	}
}
