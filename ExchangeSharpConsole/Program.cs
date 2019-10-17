using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharpConsole.Options;

namespace ExchangeSharpConsole
{
	public partial class Program
	{
		private readonly Parser parser;

		public Program()
		{
			parser = new Parser(c =>
			{
				c.AutoHelp = true;
				c.AutoVersion = true;
				c.CaseSensitive = false;
				c.ParsingCulture = CultureInfo.InvariantCulture;
				c.HelpWriter = Console.Out;
				c.EnableDashDash = true;
				c.IgnoreUnknownArguments = true;
				c.CaseInsensitiveEnumValues = true;
			});
		}

		private (bool error, bool help) ParseArgs(string[] args, out List<BaseOption> options)
		{
			var error = false;
			var help = false;
			var optionList = new List<BaseOption>();

			parser
				.ParseArguments(
					args,
					typeof(ConvertOption),
					typeof(ExampleOption),
					typeof(ExportOption),
					typeof(KeysOption),
					typeof(MarketSymbolsOption),
					typeof(OrderDetailsOption),
					typeof(OrderHistoryOption),
					typeof(StatsOption),
					typeof(SupportedExchangesOption),
					typeof(TestOption),
					typeof(TickerOption),
					typeof(TradeHistoryOption)
				)
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
						break;
				}
			}

			return (error, help);
		}

		private async Task Run(List<BaseOption> actions)
		{
			foreach (var action in actions)
			{
				await action.RunCommand();
			}
		}
	}
}
