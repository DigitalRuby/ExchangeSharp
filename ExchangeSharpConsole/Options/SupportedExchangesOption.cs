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
			foreach (var exchangeName in ExchangeName.ExchangeNames)
			{
				Console.WriteLine(exchangeName);
			}

			await Task.CompletedTask;
		}
	}
}
