using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("example", HelpText =
		"Simple example showing how to create an API instance and get the ticker, and place an order.")]
	public class ExampleOption : BaseOption, IOptionWithKey
	{
		public override async Task RunCommand()
		{
			using var api = new ExchangeKrakenAPI();
			var ticker = await api.GetTickerAsync("XXBTZUSD");

			Console.WriteLine("On the Kraken exchange, 1 bitcoin is worth {0} USD.", ticker.Bid);

			try
			{
				// load API keys created from ExchangeSharpConsole.exe keys mode=create path=keys.bin keylist=public_key,private_key
				api.LoadAPIKeys(KeyPath);
			}
			catch (ArgumentException)
			{
				Console.Error.WriteLine(
					"Invalid key file.\n" +
					"Try generating a key file with the \"keys\" utility."
				);
				Environment.Exit(Program.ExitCodeError);
				return;
			}

			// place limit order for 0.01 bitcoin at ticker.Ask USD
			var result = await api.PlaceOrderAsync(new ExchangeOrderRequest
			{
				Amount = 0.01m,
				IsBuy = true,
				Price = ticker.Ask,
				MarketSymbol = "XXBTZUSD"
			});

			// Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
			//  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
			// I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
			//  their house in order.
			await Task.Delay(500);

			result = await api.GetOrderDetailsAsync(result.OrderId);

			Console.WriteLine(
				"Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.",
				ticker.Ask, result.Result, result.OrderId
			);
		}

		public string KeyPath { get; set; }
	}
}
