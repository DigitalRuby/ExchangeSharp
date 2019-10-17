using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;

namespace ExchangeSharpConsole.Options
{
	[Verb("supported-exchanges", HelpText = "Get a list of all supported exchange names.")]
	public class SupportedExchangesOption : BaseOption
	{
		public override async Task RunCommand()
		{
			Console.WriteLine("Supported exchanges: {0}", string.Join(", ", ExchangeName.ExchangeNames));
			await Task.CompletedTask;
		}
	}
}
