using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("ws-tickers", HelpText =
		"Connects to the given exchange websocket and keeps printing tickers from that exchange." +
		"If market symbol is not set then uses all.")]
	public class WebSocketsTickersOption : BaseOption, IOptionPerExchange, IOptionWithMultipleMarketSymbol
	{
		public override async Task RunCommand()
		{
			async Task<IWebSocket> GetWebSocket(IExchangeAPI api)
			{
				var symbols = await ValidateMarketSymbolsAsync(api, MarketSymbols.ToArray());

				return await api.GetTickersWebSocketAsync(freshTickers =>
					{
						foreach (var (key, ticker) in freshTickers)
						{
							Console.WriteLine($"Market {key,8}: Ticker {ticker}");
						}
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
