using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithAddress
	{
		[Option('d', "address", Required = true, HelpText = "Crypto address")]
		string Address { get; set; }

		[Option('t', "address-tag", Required = false, HelpText = "Tag describing the address")]
		string Tag { get; set; }
	}
}
