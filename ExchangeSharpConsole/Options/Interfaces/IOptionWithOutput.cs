using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithOutput
	{
		[Option('o', "output", Default = "output",
			HelpText = "Where the data will be stored")]
		string Path { get; set; }
	}
}
