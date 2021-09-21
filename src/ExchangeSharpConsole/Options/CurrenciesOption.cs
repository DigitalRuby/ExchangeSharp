using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace ExchangeSharpConsole.Options
{
	[Verb("currencies", HelpText = "Gets a list of currencies available on the exchange")]
	public class CurrenciesOption : BaseOption, IOptionPerExchange
	{
		public string ExchangeName { get; set; }

		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			Authenticate(api);

			var currencies = await api.GetCurrenciesAsync();

			foreach (var c in currencies)
			{
				Console.WriteLine($"{c.Key}: {c.Value.FullName}, {c.Value.AltName}");
			}
		}
	}
}
