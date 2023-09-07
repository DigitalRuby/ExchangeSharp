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
			"ws-trades",
			HelpText = "Connects to the given exchange websocket and keeps printing the trades from that exchange.\n"
					+ "If market symbol is not set then uses all."
	)]
	public class WebSocketsTradesOption
			: BaseOption,
					IOptionPerExchange,
					IOptionWithMultipleMarketSymbol
	{
		public override async Task RunCommand()
		{
			async Task<IWebSocket> GetWebSocket(IExchangeAPI api)
			{
				var symbols = await ValidateMarketSymbolsAsync(api, MarketSymbols.ToArray(), true);

				return await api.GetTradesWebSocketAsync(
						message =>
						{
							Console.WriteLine($"{message.Key}: {message.Value}");
							return Task.CompletedTask;
						},
						symbols
				);
			}

			await RunWebSocket(ExchangeName, GetWebSocket);
		}

		public string ExchangeName { get; set; }

		public IEnumerable<string> MarketSymbols { get; set; }
	}
}
