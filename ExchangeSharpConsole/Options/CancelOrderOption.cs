using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("cancel", HelpText = "Cancel an order in a given exchange.")]
	public class CancelOrderOption : BaseOption,
		IOptionPerOrderId, IOptionPerExchange, IOptionWithKey, IOptionWithMarketSymbol
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			api.LoadAPIKeys(KeyPath);

			await api.CancelOrderAsync(OrderId, api.NormalizeMarketSymbol(MarketSymbol));

			Console.WriteLine("Done.");
		}

		public string OrderId { get; set; }

		public string ExchangeName { get; set; }

		public string MarketSymbol { get; set; }

		public string KeyPath { get; set; }
	}
}
