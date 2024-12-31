using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharpConsole.Options;

[Verb(
		"balances",
		HelpText = "Displays the account balances for an exchange."
)]
public class BalancesOption : BaseOption, IOptionPerExchange, IOptionWithKey
{
	public override async Task RunCommand()
	{
		using var api = await GetExchangeInstanceAsync(ExchangeName);
		
		api.LoadAPIKeys(KeyPath);

		if (Mode.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine("All Balances:");

			var balances = await api.GetAmountsAsync();

			foreach (var balance in balances)
			{
				Console.WriteLine($"{balance.Key}: {balance.Value}");
			}

			Console.WriteLine();
		}
		else if (Mode.Equals("trade", StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine("Balances available for trading:");

			var balances = await api.GetAmountsAvailableToTradeAsync();

			foreach (var balance in balances)
			{
				Console.WriteLine($"{balance.Key}: {balance.Value}");
			}

			Console.WriteLine();
		}
		else
		{
			throw new ArgumentException($"Invalid mode: {Mode}");
		}
	}

	public string ExchangeName { get; set; }

	[Option(
		'm',
		"mode",
		Required = true,
		HelpText = "Mode of execution."
				+ "\n\tPossible values are \"all\" or \"trade\"."
				+ "\n\t\tall: Displays all funds, regardless if they are locked or not."
				+ "\n\t\ttrade: Displays the funds that can be used, at the current moment, to trade."
	)]

	public string Mode { get; set; }

	public string KeyPath { get; set; }
}
