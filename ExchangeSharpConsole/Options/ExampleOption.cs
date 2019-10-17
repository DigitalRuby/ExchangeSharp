using System.Threading.Tasks;
using CommandLine;

namespace ExchangeSharpConsole.Options
{
	[Verb("example", HelpText =
		"Simple example showing how to create an API instance and get the ticker, and place an order.")]
	public class ExampleOption : BaseOption
	{
		public override async Task RunCommand()
		{
			await ExchangeSharpConsoleMain.RunExample();
		}
	}
}
