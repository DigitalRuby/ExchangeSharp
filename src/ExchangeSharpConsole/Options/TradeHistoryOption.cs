using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("trade-history", HelpText = "Print trade history from an Exchange to output.\n" +
	                                  "Example: trade-history -e Binance -s btcusdt --since \"2018-05-17\" --to \"2018-05-18\"")]
	public class TradeHistoryOption : BaseOption, IOptionPerExchange, IOptionPerMarketSymbol, IOptionWithStartDate,
		IOptionWithEndDate
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			Console.WriteLine($"Showing historical trades for exchange {ExchangeName}...");

			DateTime? startDate = null;
			DateTime? endDate = null;

			if (!string.IsNullOrWhiteSpace(SinceDateString))
			{
				startDate = DateTime.Parse(SinceDateString).ToUniversalTime();
			}

			if (!string.IsNullOrWhiteSpace(ToDateString))
			{
				endDate = DateTime.Parse(ToDateString).ToUniversalTime();
			}

			await api.GetHistoricalTradesAsync(
				PrintTrades,
				MarketSymbol,
				startDate,
				endDate
			);
		}

		private static bool PrintTrades(IEnumerable<ExchangeTrade> trades)
		{
			foreach (var trade in trades)
			{
				Console.WriteLine(
					$"Trade at timestamp {trade.Timestamp.ToLocalTime()}: "
					+ $"{trade.Id}/{trade.Price}/{trade.Amount}"
				);
			}

			return true;
		}

		public string ExchangeName { get; set; }

		public string MarketSymbol { get; set; }

		public string SinceDateString { get; set; }

		public string ToDateString { get; set; }
	}
}
