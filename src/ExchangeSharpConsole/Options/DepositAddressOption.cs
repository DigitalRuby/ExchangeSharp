using CommandLine;
using ExchangeSharpConsole.Options.Interfaces;
using System;
using System.Threading.Tasks;

namespace ExchangeSharpConsole.Options
{
    [Verb("deposit-address", HelpText = "Get a deposit address for given currency")]
    public class DepositAddressOption : BaseOption, IOptionPerExchange, IOptionWithCurrency
    {
        public string ExchangeName { get; set; }

        public string Currency { get; set; }

        public override async Task RunCommand()
        {
            using var api = await GetExchangeInstanceAsync(ExchangeName);

            Authenticate(api);

            var address = await api.GetDepositAddressAsync(Currency);

            Console.WriteLine($"Address: {address}");
        }
    }
}
