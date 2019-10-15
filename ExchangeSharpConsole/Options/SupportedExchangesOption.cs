using System.Threading.Tasks;
using CommandLine;

namespace ExchangeSharpConsole.Options
{
	[Verb("supported-exchanges", HelpText = "Get a list of all supported exchange names.")]
	public class SupportedExchangesOption : BaseOption
	{
		public override async Task RunCommand()
		{
			await ExchangeSharpConsoleMain.ShowSupportedExchanges();
		}
	}

	[Verb("getExchangeNames", HelpText = "Backwards-compatible version of \"supported-exchanges\"")]
	public class ObsoleteSupportedExchangesOption : SupportedExchangesOption
	{
	}
}
