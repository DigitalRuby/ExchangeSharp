using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithKey
	{
		[Option('k', "key", Default = Constants.DefaultKeyPath,
			HelpText = "Path to key file (generated with the key utility).")]
		string KeyPath { get; set; }
	}
}
