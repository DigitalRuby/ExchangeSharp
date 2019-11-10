using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharp.BinanceGroup;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("test", HelpText = "Run integrations test code against exchanges.")]
	public class TestOption : BaseOption, IOptionPerExchange, IOptionWithFunctionRegex
	{
		public string ExchangeName { get; set; }

		public Regex ExchangeNameRegex { get; }

		public string FunctionRegex { get; set; }

		public override async Task RunCommand()
		{
			var apis = ExchangeAPI.GetExchangeAPIs();

			foreach (var api in apis)
			{
				// WIP exchanges...
				if (api is ExchangeUfoDexAPI)
				{
					continue;
				}

				if (ExchangeName != null && !Regex.IsMatch(api.Name, ExchangeName, RegexOptions.IgnoreCase))
				{
					continue;
				}

				// test all public API for each exchange
				try
				{
					var marketSymbol = api.NormalizeMarketSymbol(GetSymbol(api));

					await TestMarketSymbols(api, marketSymbol);

					await TestCurrencies(api);

					await TestOrderBook(api, marketSymbol);

					await TestTicker(api, marketSymbol);

					await TestTrade(api, marketSymbol);

					await TestCandle(api, marketSymbol);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Request failed, api: {0}, error: {1}", api.Name, ex.Message);
				}
			}
		}

		private async Task TestMarketSymbols(IExchangeAPI api, string marketSymbol)
		{
			if (FunctionRegex == null || Regex.IsMatch("symbol", FunctionRegex, RegexOptions.IgnoreCase))
			{
				Console.Write("Test {0} GetSymbolsAsync... ", api.Name);
				IReadOnlyCollection<string> symbols = (await api.GetMarketSymbolsAsync())
					.ToArray();
				Assert(symbols.Count != 0 &&
				       symbols.Contains(marketSymbol, StringComparer.OrdinalIgnoreCase));
				Console.WriteLine($"OK (default: {marketSymbol}; {symbols.Count} symbols)");
			}
		}

		private async Task TestCurrencies(IExchangeAPI api)
		{
			if (FunctionRegex == null || Regex.IsMatch("currencies", FunctionRegex, RegexOptions.IgnoreCase))
			{
				try
				{
					Console.Write("Test {0} GetCurrenciesAsync... ", api.Name);
					var currencies = await api.GetCurrenciesAsync();
					Assert(currencies.Count != 0);
					Console.WriteLine($"OK ({currencies.Count} currencies)");
				}
				catch (NotImplementedException)
				{
					Console.WriteLine("Not implemented");
				}
			}
		}

		private async Task TestOrderBook(IExchangeAPI api, string marketSymbol)
		{
			if (FunctionRegex == null || Regex.IsMatch("orderbook", FunctionRegex, RegexOptions.IgnoreCase))
			{
				try
				{
					Console.Write("Test {0} GetOrderBookAsync... ", api.Name);
					var book = await api.GetOrderBookAsync(marketSymbol);
					Assert(book.Asks.Count != 0 && book.Bids.Count != 0 &&
					       book.Asks.First().Value.Amount > 0m &&
					       book.Asks.First().Value.Price > 0m && book.Bids.First().Value.Amount > 0m &&
					       book.Bids.First().Value.Price > 0m);
					Console.WriteLine($"OK ({book.Asks.Count} asks, {book.Bids.Count} bids)");
				}
				catch (NotImplementedException)
				{
					Console.WriteLine("Not implemented");
				}
			}
		}

		private async Task TestTicker(IExchangeAPI api, string marketSymbol)
		{
			if (FunctionRegex == null || Regex.IsMatch("ticker", FunctionRegex, RegexOptions.IgnoreCase))
			{
				try
				{
					Console.Write("Test {0} GetTickerAsync... ", api.Name);
					var ticker = await api.GetTickerAsync(marketSymbol);
					Assert(ticker != null && ticker.Ask > 0m && ticker.Bid > 0m && ticker.Last > 0m &&
					       ticker.Volume != null && ticker.Volume.QuoteCurrencyVolume > 0m &&
					       ticker.Volume.BaseCurrencyVolume > 0m);
					Console.WriteLine($"OK (ask: {ticker.Ask}, bid: {ticker.Bid}, last: {ticker.Last})");
				}
				catch
				{
					Console.WriteLine("Data invalid or empty");
				}
			}
		}

		private async Task TestTrade(IExchangeAPI api, string marketSymbol)
		{
			if (FunctionRegex == null || Regex.IsMatch("trade", FunctionRegex, RegexOptions.IgnoreCase))
			{
				try
				{
					ExchangeTrade[] trades = null;
					Console.Write("Test {0} GetHistoricalTradesAsync... ", api.Name);
					await api.GetHistoricalTradesAsync(tradeEnum =>
					{
						trades = tradeEnum.ToArray();
						return true;
					}, marketSymbol);
					Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
					Console.WriteLine($"OK ({trades.Length})");

					Console.Write("Test {0} GetRecentTradesAsync... ", api.Name);
					trades = (await api.GetRecentTradesAsync(marketSymbol)).ToArray();
					Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
					Console.WriteLine($"OK ({trades.Length} trades)");
				}
				catch (NotImplementedException)
				{
					Console.WriteLine("Not implemented");
				}
			}
		}

		private async Task TestCandle(IExchangeAPI api, string marketSymbol)
		{
			if (FunctionRegex == null || Regex.IsMatch("candle", FunctionRegex, RegexOptions.IgnoreCase))
			{
				try
				{
					Console.Write("Test {0} GetCandlesAsync... ", api.Name);
					var candles = (await api.GetCandlesAsync(marketSymbol, 86400,
						CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(7.0)), null)).ToArray();
					Assert(candles.Length != 0 && candles[0].ClosePrice > 0m && candles[0].HighPrice > 0m &&
					       candles[0].LowPrice > 0m && candles[0].OpenPrice > 0m &&
					       candles[0].HighPrice >= candles[0].LowPrice &&
					       candles[0].HighPrice >= candles[0].ClosePrice &&
					       candles[0].HighPrice >= candles[0].OpenPrice &&
					       !string.IsNullOrWhiteSpace(candles[0].Name) && candles[0].ExchangeName == api.Name &&
					       candles[0].PeriodSeconds == 86400 && candles[0].BaseCurrencyVolume > 0.0 &&
					       candles[0].QuoteCurrencyVolume > 0.0 && candles[0].WeightedAverage >= 0m);

					Console.WriteLine($"OK ({candles.Length})");
				}
				catch (NotImplementedException)
				{
					Console.WriteLine("Not implemented");
				}
				catch
				{
					// These API require private access to get candles end points
					if (!(api is ExchangeKuCoinAPI))
					{
						throw;
					}
				}
			}
		}

		private string GetSymbol(IExchangeAPI api)
		{
			if (api is ExchangeLivecoinAPI || api is ExchangeZBcomAPI)
			{
				return "LTC-BTC";
			}

			if (api is ExchangeKrakenAPI)
			{
				return "XXBTZ-USD";
			}

			if (api is ExchangeBittrexAPI || api is ExchangePoloniexAPI)
			{
				return "BTC-LTC";
			}

			if (api is BinanceGroupCommon || api is ExchangeOKExAPI || /* api is ExchangeBleutradeAPI ||*/
			    api is ExchangeKuCoinAPI || api is ExchangeHuobiAPI)
			{
				return "ETH-BTC";
			}

			if (api is ExchangeYobitAPI || api is ExchangeBitBankAPI)
			{
				return "LTC-BTC";
			}

			if (api is ExchangeBitMEXAPI)
			{
				return "XBT-USD";
			}

			return "BTC-USD";
		}
	}
}
