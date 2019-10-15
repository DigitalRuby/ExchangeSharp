using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("trade-history", HelpText = "Print trade history from an Exchange to output.\n" +
	                                  "Example: trade-history -e Binance -s btcusdt --since \"20180517\" --to \"20180518\"")]
	public class TradeHistoryOption : BaseOption, IOptionPerExchange, IOptionPerSymbol, IOptionWithStartDate,
		IOptionWithEndDate
	{
		public override async Task RunCommand()
		{
			var api = ExchangeAPI.GetExchangeAPI(ExchangeName);

			Logger.Info($"Showing historical trades for exchange {ExchangeName}...");

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
				Symbol,
				startDate,
				endDate
			);
		}

		private static bool PrintTrades(IEnumerable<ExchangeTrade> trades)
		{
			foreach (var trade in trades)
			{
				Logger.Info(
					$"Trade at timestamp {trade.Timestamp.ToLocalTime()}: "
					+ $"{trade.Id}/{trade.Price}/{trade.Amount}"
				);
			}

			return true;
		}

		public string ExchangeName { get; set; }
		public string Symbol { get; set; }
		public string SinceDateString { get; set; }
		public string ToDateString { get; set; }
	}
}
