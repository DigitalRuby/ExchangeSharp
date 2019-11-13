using CommandLine;
using ExchangeSharp;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithOrderInfo : IOptionPerMarketSymbol
	{
		[Option('r', "price", Required = true, HelpText = "The price to buy or sell at.")]
		decimal Price { get; set; }

		[Option('a', "amount", Required = true, HelpText = "Amount to buy or sell.")]
		decimal Amount { get; set; }

		[Option("stop-price", HelpText = "The price to trigger a stop.\n" +
		                                 "This has to be implemented and supported by the chosen exchange.")]
		decimal StopPrice { get; set; }

		[Option("order-type", Required = true, Default = OrderType.Limit, HelpText =
			"The type of order.\n" +
			"Possible values: 'limit', 'market' and 'stop'"
		)]
		OrderType OrderType { get; set; }

		[Option("margin", Default = false, HelpText =
			"Whether the order is a margin order. Not all exchanges support margin orders, so this parameter may be ignored.\n" +
			"You should verify that your exchange supports margin orders before passing this field as true and expecting it to be a margin order.\n" +
			"The best way to determine this in code is to call one of the margin account balance methods and see if it fails."
		)]
		bool IsMargin { get; set; }

		[Option("round", Default = true, HelpText =
			"Whether the amount should be rounded.\n" +
			"Set to false if you know the exact amount, otherwise leave as true so that the exchange does not reject the order due to too many decimal places."
		)]
		bool ShouldRoundAmount { get; set; }

		//TODO: Create a better way to describe extra parameters so it is possible to convert them from a dictionary<str,str> to something that the exchange impl expects
// [Option("extra-params", Required = false, HelpText =
// 	"Additional order parameters specific to the exchange that don't fit in common order properties. These will be forwarded on to the exchange as key=value pairs.\n" +
// 	"Not all exchanges will use this dictionary.\n" +
// 	"These are added after all other parameters and will replace existing properties, such as order type."
// )]
// string ExtraParameters { get; set; }
	}
}
