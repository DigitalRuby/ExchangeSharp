using System;
using System.Threading.Tasks;

namespace ExchangeSharpConsole
{
	public partial class Program
	{
		internal const int ExitCodeError = -99;
		internal const int ExitCodeOk = 0;
		internal const int ExitCodeErrorParsing = -1;

		public static Program Instance { get; } = new Program();

		public static async Task<int> Main(string[] args)
		{
			var program = Instance;
			var (error, help) = program.ParseArguments(args, out var options);

			if (help)
				return ExitCodeOk;

			if (error)
				return ExitCodeErrorParsing;

			try
			{
				await program.Run(options);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				return ExitCodeError;
			}

			return ExitCodeOk;
		}
	}
}
