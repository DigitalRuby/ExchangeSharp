using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("withdraw", HelpText = "Withdraw given amount in given currency to target address")]
	public class WithdrawOption
			: BaseOption,
					IOptionPerExchange,
					IOptionWithAddress,
					IOptionWithCurrencyAmount
	{
		public string ExchangeName { get; set; }

		public string Address { get; set; }

		public string Tag { get; set; }

		public decimal Amount { get; set; }

		public string Currency { get; set; }

		public override async Task RunCommand()
		{
			using var api = await GetExchangeInstanceAsync(ExchangeName);

			Authenticate(api);

			var result = await api.WithdrawAsync(
					new ExchangeWithdrawalRequest
					{
						Address = Address,
						AddressTag = Tag,
						Amount = Amount,
						Currency = Currency
					}
			);

			Console.WriteLine(
					$"Withdrawal successful: {result.Success}, id: {result.Id}, optional message: {result.Message}"
			);
		}
	}
}
