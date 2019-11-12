using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("stats", HelpText = "Show stats from 4 exchanges.\n" +
	                          "This is a great way to see the price, order book and other useful stats.")]
	public class StatsOption : BaseOption, IOptionWithInterval
	{
		public override async Task RunCommand()
		{
			var marketSymbol = "BTC-USD";
			var marketSymbol2 = "XXBTZUSD";

			IExchangeAPI
				apiCoinbase = new ExchangeCoinbaseAPI(),
				apiGemini = new ExchangeGeminiAPI(),
				apiKraken = new ExchangeKrakenAPI(),
				apiBitfinex = new ExchangeBitfinexAPI();

			//TODO: Make this multi-threaded and add parameters
			Console.WriteLine("Use CTRL-C to stop.");

			while (true)
			{
				var ticker = await apiCoinbase.GetTickerAsync(marketSymbol);
				var orders = await apiCoinbase.GetOrderBookAsync(marketSymbol);
				var askAmountSum = orders.Asks.Values.Sum(o => o.Amount);
				var askPriceSum = orders.Asks.Values.Sum(o => o.Price);
				var bidAmountSum = orders.Bids.Values.Sum(o => o.Amount);
				var bidPriceSum = orders.Bids.Values.Sum(o => o.Price);

				var ticker2 = await apiGemini.GetTickerAsync(marketSymbol);
				var orders2 = await apiGemini.GetOrderBookAsync(marketSymbol);
				var askAmountSum2 = orders2.Asks.Values.Sum(o => o.Amount);
				var askPriceSum2 = orders2.Asks.Values.Sum(o => o.Price);
				var bidAmountSum2 = orders2.Bids.Values.Sum(o => o.Amount);
				var bidPriceSum2 = orders2.Bids.Values.Sum(o => o.Price);

				var ticker3 = await apiKraken.GetTickerAsync(marketSymbol2);
				var orders3 = await apiKraken.GetOrderBookAsync(marketSymbol2);
				var askAmountSum3 = orders3.Asks.Values.Sum(o => o.Amount);
				var askPriceSum3 = orders3.Asks.Values.Sum(o => o.Price);
				var bidAmountSum3 = orders3.Bids.Values.Sum(o => o.Amount);
				var bidPriceSum3 = orders3.Bids.Values.Sum(o => o.Price);

				var ticker4 = await apiBitfinex.GetTickerAsync(marketSymbol);
				var orders4 = await apiBitfinex.GetOrderBookAsync(marketSymbol);
				var askAmountSum4 = orders4.Asks.Values.Sum(o => o.Amount);
				var askPriceSum4 = orders4.Asks.Values.Sum(o => o.Price);
				var bidAmountSum4 = orders4.Bids.Values.Sum(o => o.Amount);
				var bidPriceSum4 = orders4.Bids.Values.Sum(o => o.Price);

				Console.Clear();
				Console.WriteLine("GDAX: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker.Last,
					ticker.Volume.QuoteCurrencyVolume, askAmountSum, askPriceSum, bidAmountSum, bidPriceSum);
				Console.WriteLine("GEMI: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker2.Last,
					ticker2.Volume.QuoteCurrencyVolume, askAmountSum2, askPriceSum2, bidAmountSum2, bidPriceSum2);
				Console.WriteLine("KRAK: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker3.Last,
					ticker3.Volume.QuoteCurrencyVolume, askAmountSum3, askPriceSum3, bidAmountSum3, bidPriceSum3);
				Console.WriteLine("BITF: {0,13:N}, {1,15:N}, {2,8:N}, {3,13:N}, {4,8:N}, {5,13:N}", ticker4.Last,
					ticker4.Volume.QuoteCurrencyVolume, askAmountSum4, askPriceSum4, bidAmountSum4, bidPriceSum4);
				Thread.Sleep(IntervalMs);
			}
		}

		public int IntervalMs { get; set; }
	}
}
