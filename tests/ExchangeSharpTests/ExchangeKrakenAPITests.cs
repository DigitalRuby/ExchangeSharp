using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ExchangeSharpTests
{
	[TestClass]
	public sealed class ExchangeKrakenTests
	{
		private ExchangeKrakenAPI api;

		[TestInitialize()]
		public async Task Startup()
		{
			api = (
					await ExchangeAPI.GetExchangeAPIAsync(ExchangeName.Kraken)
			).As<ExchangeKrakenAPI>();
			return;
		}

		[TestMethod]
		public void ExtendResultsWithOrderDescrTest()
		{
			string toParse = "buy 58.00000000 ADAUSDT @ market";
			var extendedOrder = api.ExtendResultsWithOrderDescr(new ExchangeOrderResult(), toParse);

			extendedOrder.IsBuy.Should().BeTrue();
			extendedOrder.Amount.Should().Be(58);
			extendedOrder.MarketSymbol.Should().Be("ADAUSDT");
		}

		[TestMethod]
		public void ExtendResultsWithOrderDescrAndPriceTest()
		{
			string toParse = "buy 0.001254 BTCUSDT @ limit 1000";
			var extendedOrder = api.ExtendResultsWithOrderDescr(new ExchangeOrderResult(), toParse);

			extendedOrder.IsBuy.Should().BeTrue();
			extendedOrder.Amount.Should().Be(0.001254m);
			extendedOrder.MarketSymbol.Should().Be("BTCUSDT");
			extendedOrder.Price.Should().Be(1000);
		}
	}
}
