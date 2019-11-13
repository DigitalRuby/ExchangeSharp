using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("orderbook", HelpText = "Prints the order book from an exchange.")]
	public class OrderBookOption : BaseOption,
		IOptionPerExchange, IOptionWithMultipleMarketSymbol, IOptionWithMaximum, IOptionWithKey
	{
		public override async Task RunCommand()
		{
			using var api = GetExchangeInstance(ExchangeName);

			if (!string.IsNullOrWhiteSpace(KeyPath)
			    && !KeyPath.Equals(Constants.DefaultKeyPath, StringComparison.Ordinal))
			{
				api.LoadAPIKeys(KeyPath);
			}

			var marketSymbols = MarketSymbols.ToArray();

			var orderBooks = await GetOrderBooks(marketSymbols, api);

			foreach (var (marketSymbol, orderBook) in orderBooks)
			{
				Console.WriteLine($"Order Book for market: {marketSymbol} {orderBook}");
				PrintOrderBook(orderBook);
				Console.WriteLine();
			}
		}

		private async Task<IEnumerable<KeyValuePair<string, ExchangeOrderBook>>> GetOrderBooks(
			string[] marketSymbols,
			IExchangeAPI api
		)
		{
			IEnumerable<KeyValuePair<string, ExchangeOrderBook>> orderBooks;

			if (marketSymbols.Length == 0)
			{
				orderBooks = await api.GetOrderBooksAsync(Max);
			}
			else
			{
				var orderBooksList = await Task.WhenAll(
					marketSymbols.Select(async ms =>
					{
						var orderBook = await api.GetOrderBookAsync(ms, Max);

						orderBook.MarketSymbol ??= ms;

						return orderBook;
					})
				);
				orderBooks = orderBooksList.ToDictionary(k => k.MarketSymbol, v => v);
			}

			return orderBooks;
		}

		public string ExchangeName { get; set; }

		public IEnumerable<string> MarketSymbols { get; set; }

		public int Max { get; set; }

		public string KeyPath { get; set; }
	}
}
