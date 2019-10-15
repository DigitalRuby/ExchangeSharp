using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("test", HelpText = "Run integrations test code against exchanges.")]
	public class TestOption : BaseOption, IOptionPerExchange, IOptionWithFunctionRegex
	{
		public string ExchangeName { get; set; }

		public string FunctionRegex { get; set; }

		public override async Task RunCommand()
		{
			await ExchangeSharpConsoleMain.TestExchanges(ExchangeName, FunctionRegex);
		}
	}
}
