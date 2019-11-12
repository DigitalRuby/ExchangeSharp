using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithWait
	{
		[Option('w', "wait", Default = false, HelpText = "Waits interactively.")]
		bool Wait { get; set; }
	}
}
