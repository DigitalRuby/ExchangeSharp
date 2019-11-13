using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
	[TestClass]
	public sealed class ExchangeBL3PAPITests
	{
		private static ExchangeBL3PAPI CreateBL3PAPI(string response = null)
		{
			var requestMaker = new MockAPIRequestMaker();
			if (response != null)
			{
				requestMaker.GlobalResponse = response;
			}

			var api = new ExchangeBL3PAPI
			{
				RequestMaker = requestMaker
			};
			return api;
		}

		[TestMethod]
		public async Task ShouldParseGetTickerResult()
		{
			var json = @"{
  ""currency"": ""BTC"",
  ""last"": 7472.28,
  ""bid"": 7436.76,
  ""ask"": 7474.76,
  ""high"": 7615.15,
  ""low"": 7410,
  ""timestamp"": 1570628705,
  ""volume"": {
    ""24h"": 23.89647397,
    ""30d"": 2297.00207822
  }
}";
			var api = CreateBL3PAPI(json);

			var ticker = await api.GetTickerAsync("BTCEUR");

			ticker.MarketSymbol.Should().Be("BTCEUR");
			ticker.Ask.Should().Be(7474.76M);
			ticker.Bid.Should().Be(7436.76M);
			ticker.Id.Should().BeNull();
			ticker.Last.Should().Be(7472.28M);
			ticker.Volume.Should().NotBeNull();

			var timestamp = CryptoUtility.UnixEpoch
				.AddSeconds(1570628705);
			ticker.Volume.Timestamp.Should().Be(timestamp);
			ticker.Volume.BaseCurrency.Should().Be("BTC");
			ticker.Volume.BaseCurrencyVolume.Should().Be(23.89647397M);
			ticker.Volume.QuoteCurrency.Should().Be("EUR");
			// base volume * last
			ticker.Volume.QuoteCurrencyVolume.Should().Be(23.89647397M * 7472.28M);
		}
	}
}
