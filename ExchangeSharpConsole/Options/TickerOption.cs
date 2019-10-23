using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("ticker", HelpText = "Gets the ticker for the selected exchange.")]
	public class TickerOption : BaseOption, IOptionPerExchange, IOptionWithMarketSymbol
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			try
			{
				IEnumerable<KeyValuePair<string, ExchangeTicker>> tickers;
				if (!string.IsNullOrWhiteSpace(MarketSymbol))
				{
					var ticker = await api.GetTickerAsync(MarketSymbol);
					tickers = new List<KeyValuePair<string, ExchangeTicker>>
					{
						new KeyValuePair<string, ExchangeTicker>(MarketSymbol, ticker)
					};
				}
				else
				{
					tickers = await api.GetTickersAsync();
				}

				foreach (var ticker in tickers)
				{
					Console.WriteLine(ticker.ToString());
				}

				WaitInteractively();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
			}
		}

		public string ExchangeName { get; set; }

		public string MarketSymbol { get; set; }
	}
}
