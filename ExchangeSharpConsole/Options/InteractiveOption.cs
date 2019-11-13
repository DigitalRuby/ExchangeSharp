using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace ExchangeSharpConsole.Options
{
	[Verb("interactive", HelpText = "Enables an interactive session.")]
	public class InteractiveOption : BaseOption
	{
		internal static readonly string HistoryFilePath =
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				".exchange-sharp-history"
			);

		/// <summary>
		/// UTF-8 No BOM
		/// </summary>
		internal static readonly Encoding HistoryFileEncoding = new UTF8Encoding(false, true);

		internal const int HistoryMax = 100;

		public override async Task RunCommand()
		{
			ReadLine.HistoryEnabled = true;
			ReadLine.AutoCompletionHandler = new AutoCompleter();
			Console.TreatControlCAsInput = true;
			var program = Program.Instance;

			LoadHistory();

			try
			{
				await RunInteractiveSession(program);
				Console.WriteLine();
			}
			finally
			{
				SaveHistory();
			}
		}

		private void LoadHistory()
		{
			if (!File.Exists(HistoryFilePath))
				return;

			var lines = File.ReadLines(HistoryFilePath, HistoryFileEncoding)
				.TakeLast(HistoryMax)
				.ToArray();

			ReadLine.AddHistory(lines);
		}

		private void SaveHistory()
		{
			var lines = ReadLine.GetHistory()
				.TakeLast(HistoryMax)
				.ToArray();

			using var sw = File.CreateText(HistoryFilePath);

			foreach (var line in lines)
			{
				sw.WriteLine(line);
			}
		}

		private static async Task RunInteractiveSession(Program program)
		{
			while (true)
			{
				var command = ReadLine.Read("ExchangeSharp> ", "help");

				if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
					break;

				var (error, help) = program.ParseArguments(
					command.Split(' '),
					out var options
				);

				if (error || help)
					continue;

				await program.Run(options, exitOnError: false);
			}
		}

		public class AutoCompleter : IAutoCompleteHandler
		{
			private readonly string[] options;

			public AutoCompleter()
			{
				var optionsList = Program.Instance.CommandOptions
					.Where(t => typeof(InteractiveOption) != t)
					.Select(t => t.GetCustomAttribute<VerbAttribute>(true))
					.Where(v => !v.Hidden)
					.Select(v => v.Name)
					.ToList();

				optionsList.Add("help");
				optionsList.Add("exit");

				options = optionsList
					.OrderBy(o => o)
					.ToArray();
			}

			public string[] GetSuggestions(string text, int index)
			{
				if (string.IsNullOrWhiteSpace(text))
					return options;

				return options
					.Where(o => o.StartsWith(text, StringComparison.OrdinalIgnoreCase))
					.ToArray();
			}

			public char[] Separators { get; set; } = {' ', '.', '/', '\"', '\''};
		}
	}
}
