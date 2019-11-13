using System.Threading.Tasks;
using CommandLine;

namespace ExchangeSharpConsole.Options
{
	[Verb("sell", HelpText = "Adds a sell order to a given exchange.\n" +
	                         "This sub-command will perform an action that can lead to loss of funds.\n" +
	                         "Be sure to test it first with a dry-run.")]
	public class SellOption : BuyOption
	{
		public override async Task RunCommand()
		{
			await AddOrder(isBuyOrder: false);
		}
	}
}
