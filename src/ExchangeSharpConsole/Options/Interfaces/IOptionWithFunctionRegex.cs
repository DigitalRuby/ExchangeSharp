using CommandLine;

namespace ExchangeSharpConsole.Options.Interfaces
{
	public interface IOptionWithFunctionRegex
	{
		//TODO: Help text
		[Option("function", HelpText = "TODO")]
		string FunctionRegex { get; set; }
	}
}
