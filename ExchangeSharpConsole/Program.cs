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

			void ValidateErrors(IEnumerable<Error> errs)
			{
				error = true;

				foreach (var err in errs)
				{
					switch (err.Tag)
					{
						case ErrorType.HelpVerbRequestedError:
						case ErrorType.HelpRequestedError:
							error = false;
							help = true;
							break;
					}
				}
			}

			parser
				.ParseArguments<ConvertOption, ExportOption, StatsOption, SupportedExchangesOption,
					ObsoleteSupportedExchangesOption, TestOption, TradeHistoryOption>(args)
				.WithParsed(opt => { optionList.Add((BaseOption) opt); })
				.WithNotParsed(ValidateErrors);

			options = optionList;

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
