using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithKey
	{
		[Option('k', "key", Default = "keys.bin",
			HelpText = "Path to key file (generated with the key utility).")]
		string KeyPath { get; set; }
	}
}
