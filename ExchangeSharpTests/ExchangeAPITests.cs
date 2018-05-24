namespace ExchangeSharpTests
{
    using System.Collections.Generic;

    using ExchangeSharp;

    using FluentAssertions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExchangeAPITests
    {
        [TestMethod]
        public void PopulateExchangeMarkets_CacheMissEmptyList_Refreshes()
        {
            var mockApi = new MockExchangeAPI();
            mockApi.SetExchangeMarkets(new List<ExchangeMarket>());
            mockApi.GetExchangeMarket("ADA/BTC").Should().BeNull();
            mockApi.OnGetSymbolsMetadataAsyncCalls.Should().Be(1);
        }

        [TestMethod]
        public void PopulateExchangeMarkets_CacheHit_NoRefresh()
        {
            var mockApi = new MockExchangeAPI();
            var cardano = new ExchangeMarket { MarketName = "ADA/BTC" };
            mockApi.SetExchangeMarkets(new List<ExchangeMarket> { cardano });
            mockApi.GetExchangeMarket("ADA/BTC").Should().Be(cardano);
            mockApi.OnGetSymbolsMetadataAsyncCalls.Should().Be(0);
        }

        [TestMethod]
        public void PopulateExchangeMarkets_CacheMissNonEmptyList_RefreshesWhenMarketNotFound()
        {
            var mockApi = new MockExchangeAPI();
            var eth = new ExchangeMarket { MarketName = "ETH/BTC" };
            mockApi.SetExchangeMarkets(new List<ExchangeMarket> { eth });
            mockApi.GetExchangeMarket("ADA/BTC").Should().BeNull();
            mockApi.OnGetSymbolsMetadataAsyncCalls.Should().Be(1);
        }

        [TestMethod]
        public void PopulateExchangeMarkets_MarketsEmptiedOnRefresh()
        {
            var mockApi = new MockExchangeAPI();
            var cardano = new ExchangeMarket { MarketName = "ADA/BTC" };
            mockApi.SetExchangeMarkets(new List<ExchangeMarket> { cardano });
            mockApi.GetExchangeMarket("ADA/BTC").Should().Be(cardano);
            mockApi.OnGetSymbolsMetadataAsyncCalls.Should().Be(0);

            mockApi.GetExchangeMarket("DOGE/BTC").Should().BeNull();
            mockApi.OnGetSymbolsMetadataAsyncCalls.Should().Be(1);
            mockApi.GetExchangeMarket("ADA/BTC").Should().BeNull();
            mockApi.OnGetSymbolsMetadataAsyncCalls.Should().Be(2);
        }
    }
}