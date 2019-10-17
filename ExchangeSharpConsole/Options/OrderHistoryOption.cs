using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("order-history", HelpText = "Prints the orders history from an exchange.")]
	public class OrderHistoryOption : BaseOption, IOptionPerExchange, IOptionPerMarketSymbol, IOptionWithStartDate
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			Authenticate(api);

			DateTime? startDate = null;
			if (!string.IsNullOrWhiteSpace(SinceDateString))
			{
				startDate = DateTime.Parse(SinceDateString).ToUniversalTime();
			}

			var completedOrders = await api.GetCompletedOrderDetailsAsync(MarketSymbol, startDate);
			foreach (var completedOrder in completedOrders)
			{
				Console.WriteLine(completedOrder);
			}

			WaitInteractively();
		}

		public string ExchangeName { get; set; }

		public string MarketSymbol { get; set; }

		public string SinceDateString { get; set; }
	}
}
