using System;
using System.Threading.Tasks;

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

			try
			{
				await program.Run(options);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				return -99;
			}

			return 0;
		}
	}
}
