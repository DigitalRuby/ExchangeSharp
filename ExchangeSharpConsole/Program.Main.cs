using System;
using System.Threading.Tasks;
using ExchangeSharp;

namespace ExchangeSharpConsole
{
	public partial class Program
	{
		public static async Task<int> Main(string[] args)
		{
			var program = new Program();
			var (error, help) = program.ParseArgs(args, out var options);

			if (help)
				return 0;

			if (error)
				return -1;

			Logger.Info("ExchangeSharp console started.");

			try
			{
				await program.Run(options);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
				return -99;
			}
			finally
			{
				Logger.Info("ExchangeSharp console finished.");
			}

			return 0;
		}
	}
}
