using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options;

namespace ExchangeSharpConsole
{
	public partial class Program
	{
		internal readonly Parser Parser;

		public Type[] CommandOptions { get; } =
		{
			typeof(BuyOption),
			typeof(CancelOrderOption),
			typeof(CandlesOption),
			typeof(ConvertOption),
			typeof(ExampleOption),
			typeof(ExportOption),
			typeof(InteractiveOption),
			typeof(KeysOption),
			typeof(MarketSymbolsMetadataOption),
			typeof(MarketSymbolsOption),
			typeof(OrderBookOption),
			typeof(OrderDetailsOption),
			typeof(OrderHistoryOption),
			typeof(SellOption),
			typeof(StatsOption),
			typeof(SupportedExchangesOption),
			typeof(TestOption),
			typeof(TickerOption),
			typeof(TradeHistoryOption),
			typeof(WebSocketsOrderbookOption),
			typeof(WebSocketsTickersOption),
			typeof(WebSocketsTradesOption)
		};

		public Program()
		{
			Parser = new Parser(c =>
			{
				c.AutoHelp = true;
				c.AutoVersion = true;
				c.CaseSensitive = false;
				c.ParsingCulture = CultureInfo.InvariantCulture;
				c.HelpWriter = Console.Out;
				c.EnableDashDash = true;
				c.IgnoreUnknownArguments = false;
				c.CaseInsensitiveEnumValues = true;
			});
		}

		internal (bool error, bool help) ParseArguments(string[] args, out List<BaseOption> options)
		{
			var error = false;
			var help = false;
			var optionList = new List<BaseOption>();

			Parser
				.ParseArguments(args, CommandOptions)
				.WithParsed(opt => optionList.Add((BaseOption) opt))
				.WithNotParsed(errs => (error, help) = ValidateParseErrors(errs));

			options = optionList;

			return (error, help);
		}

		private (bool error, bool help) ValidateParseErrors(IEnumerable<Error> errs)
		{
			var error = false;
			var help = false;

			foreach (var err in errs)
			{
				switch (err.Tag)
				{
					case ErrorType.HelpVerbRequestedError:
					case ErrorType.HelpRequestedError:
						help = true;
						break;
					default:
						error = true;
						Debugger.Break();
						break;
				}
			}

			return (error, help);
		}

		internal async Task Run(List<BaseOption> actions, bool exitOnError = true)
		{
			foreach (var action in actions)
			{
#if DEBUG
				await action.CheckDebugger();
#endif

				try
				{
					await action.RunCommand();
				}
				catch (Exception e)
				{
					Console.Error.WriteLine(e.Message);
					if (exitOnError)
						Environment.Exit(ExitCodeError);
					return;
				}
			}
		}
	}
}
