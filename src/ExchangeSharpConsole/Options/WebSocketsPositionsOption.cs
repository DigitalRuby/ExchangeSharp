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
			"ws-positions",
			HelpText = "Connects to the given exchange private websocket and keeps printing your position updates from that exchange."
	)]
	public class WebSocketsPositionsOption : BaseOption, IOptionPerExchange, IOptionWithKey
	{
		public override async Task RunCommand()
		{
			async Task<IWebSocket> GetWebSocket(IExchangeAPI api)
			{
				api.LoadAPIKeys(KeyPath);

				return await api.GetPositionsWebSocketAsync(position =>
				{
					Console.WriteLine(
											$"Open TimeStamp: {position.TimeStamp} Market: {position.MarketSymbol}: Amount: {position.Amount} Average Price: {position.AveragePrice}"
									);
				});
			}

			await RunWebSocket(ExchangeName, GetWebSocket);
		}

		public string ExchangeName { get; set; }

		public string KeyPath { get; set; }
	}
}
