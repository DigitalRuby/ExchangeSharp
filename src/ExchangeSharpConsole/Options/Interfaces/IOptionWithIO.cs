using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	// ReSharper disable once InconsistentNaming
	public interface IOptionWithIO
	{
		[Option('p', "path", Default = "output",
			HelpText = "Where the data will be stored or read from.")]
		string Path { get; set; }
	}
}
