using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb(
			"ws-candles",
			HelpText = "Connects to the given exchange websocket and keeps printing the candles from that exchange.\n"
					+ "If market symbol is not set then uses all."
	)]
	public class WebSocketsCandlesOption
			: BaseOption,
					IOptionPerExchange,
					IOptionWithMultipleMarketSymbol,
					IOptionWithPeriod
	{
		public override async Task RunCommand()
		{
			async Task<IWebSocket> GetWebSocket(IExchangeAPI api)
			{
				var symbols = await ValidateMarketSymbolsAsync(api, MarketSymbols.ToArray(), true);

				return await api.GetCandlesWebSocketAsync(
						candle =>
						{
							Console.WriteLine($"Market {candle.Name,8}: {candle}");
							return Task.CompletedTask;
						},
						Period,
						symbols
				);
			}

			await RunWebSocket(ExchangeName, GetWebSocket);
		}

		public string ExchangeName { get; set; }

		public IEnumerable<string> MarketSymbols { get; set; }

		public int Period { get; set; }
	}
}
