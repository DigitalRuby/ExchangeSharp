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

			await exchange.GetMarketSymbolsAsync();
		}
	}
}
