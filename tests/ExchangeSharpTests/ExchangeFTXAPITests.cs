using ExchangeSharp.API.Exchanges.FTX.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace ExchangeSharpTests
{
	[TestClass]
	public class ExchangeFTXAPITests
	{
		[TestMethod]
		public async Task TempTest()
		{
			var exchange = new ExchangeFTXAPI();

			exchange.LoadAPIKeysUnsecure("qq0Y6JKAbefgU4GBllT_iGwrhhhJnM7YExkP8q_F", "BvdkXp4vJHxigx5W5d97YmCVEv9TEr944dDQBpLR");

			var result = await exchange.GetAmountsAsync();
		}
	}
}
