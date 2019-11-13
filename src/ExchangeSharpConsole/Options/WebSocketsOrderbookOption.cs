using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("ws-orderbook", HelpText =
		"Connects to the given exchange websocket and keeps printing the first bid and ask prices and amounts for the given market symbols." +
		"If market symbol is not set then uses all.")]
	public class WebSocketsOrderbookOption : BaseOption, IOptionPerExchange, IOptionWithMultipleMarketSymbol
	{
		public override async Task RunCommand()
		{
			async Task<IWebSocket> GetWebSocket(IExchangeAPI api)
			{
				var symbols = await ValidateMarketSymbolsAsync(api, MarketSymbols.ToArray());

				return await api.GetFullOrderBookWebSocketAsync(
					OrderBookCallback,
					symbols: symbols
				);
			}

			await RunWebSocket(ExchangeName, GetWebSocket);
		}

		private static void OrderBookCallback(ExchangeOrderBook msg)
		{
			var (_, bid) = msg.Bids.FirstOrDefault();
			var (_, ask) = msg.Asks.FirstOrDefault();

			Console.WriteLine(
				$"[{msg.MarketSymbol,-8}:{msg.SequenceId,10}] " +
				$"{bid.Price,10} ({bid.Amount,9:N2}) | " +
				$"{ask.Price,10} ({ask.Amount,9:N})"
			);
		}

		public string ExchangeName { get; set; }

		public IEnumerable<string> MarketSymbols { get; set; }
	}
}
