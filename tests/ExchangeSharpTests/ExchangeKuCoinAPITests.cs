using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    [TestClass]
    public sealed class ExchangeKuCoinAPITests
    {
        private static async Task<ExchangeKuCoinAPI> CreateKuCoinAPI(string response = null)
        {
            var requestMaker = new MockAPIRequestMaker();
            if (response != null)
            {
                requestMaker.GlobalResponse = response;
            }

            var api = (await ExchangeAPI.GetExchangeAPIAsync(ExchangeName.KuCoin) as ExchangeKuCoinAPI)!;
            api.RequestMaker = requestMaker;
            return api;
        }

        [TestMethod]
        public async Task ShouldParseGetTickerResult()
        {
            var json = @"{
                ""code"": ""200000"",
                ""data"": {
                    ""sequence"": ""1545820038784"",
                    ""price"": ""0.07"",
                    ""size"": ""0.001"",
                    ""bestBid"": ""0.069"",
                    ""bestBidSize"": ""0.017"",
                    ""bestAsk"": ""0.07"",
                    ""bestAskSize"": ""0.002""
                }
            }";
            var api = await CreateKuCoinAPI(json);

            var ticker = await api.GetTickerAsync("BTC-USDT");

            ticker.MarketSymbol.Should().Be("BTC-USDT");
            ticker.Ask.Should().Be(0.07m);
            ticker.Bid.Should().Be(0.069m);
            ticker.Last.Should().Be(0.07m);
            ticker.Volume.Should().NotBeNull();
        }

        [TestMethod]
        public async Task ShouldGetOrderBook()
        {
            var json = @"{
                ""code"": ""200000"",
                ""data"": {
                    ""sequence"": ""3262786978"",
                    ""asks"": [
                        [""0.07"", ""0.002""]
                    ],
                    ""bids"": [
                        [""0.069"", ""0.017""]
                    ]
                }
            }";
            var api = await CreateKuCoinAPI(json);

            var orderBook = await api.GetOrderBookAsync("BTC-USDT");

            orderBook.MarketSymbol.Should().Be("BTC-USDT");
            orderBook.Asks.First().Value.Price.Should().Be(0.07m);
            orderBook.Asks.First().Value.Amount.Should().Be(0.002m);
            orderBook.Bids.First().Value.Price.Should().Be(0.069m);
            orderBook.Bids.First().Value.Amount.Should().Be(0.017m);
        }

        [TestMethod]
        public async Task ShouldGetRecentTrades()
        {
            var json = @"{
                ""code"": ""200000"",
                ""data"": [
                    {
                        ""sequence"": ""1545896668571"",
                        ""price"": ""0.07"",
                        ""size"": ""0.004"",
                        ""side"": ""sell"",
                        ""time"": 1545896668571
                    }
                ]
            }";
            var api = await CreateKuCoinAPI(json);

            var trades = (await api.GetRecentTradesAsync("BTC-USDT")).ToArray();

            trades.Should().HaveCount(1);
            trades[0].Price.Should().Be(0.07m);
            trades[0].Amount.Should().Be(0.004m);
            trades[0].IsBuy.Should().BeFalse();
        }

        [TestMethod]
        public async Task ShouldGetCandles()
        {
            var json = @"{
                ""code"": ""200000"",
								""data"": [
									[
										""1545904980"",
										""0.058"", 
										""0.048"",
										""0.059"", 
										""0.049"", 
										""0.018"", 
										""0.000945"" 
									]
								]
            }";
            var api = await CreateKuCoinAPI(json);

            var candles = (await api.GetCandlesAsync("BTC-USDT", 60, CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(1)), CryptoUtility.UtcNow)).ToArray();

            candles.Should().HaveCount(1);
            candles[0].OpenPrice.Should().Be(0.058m);
            candles[0].HighPrice.Should().Be(0.059m);
            candles[0].LowPrice.Should().Be(0.049m);
            candles[0].ClosePrice.Should().Be(0.048m);
						candles[0].BaseCurrencyVolume.Should().Be(0.018m);
						candles[0].QuoteCurrencyVolume.Should().Be(0.000945m);
		}

		[TestMethod]
		public async Task ShouldGetMarketSymbolsMetadata()
		{
			var json = @"{
                ""code"": ""200000"",
                ""data"": [
									{
											""symbol"": ""BTC-USDT"",
											""name"": ""BTC-USDT"",
											""baseCurrency"": ""BTC"",
											""quoteCurrency"": ""USDT"",
											""feeCurrency"": ""USDT"",
											""market"": ""USDS"",
											""baseMinSize"": ""0.00001"",
											""quoteMinSize"": ""0.1"",
											""baseMaxSize"": ""10000000000"",
											""quoteMaxSize"": ""99999999"",
											""baseIncrement"": ""0.00000001"",
											""quoteIncrement"": ""0.000001"",
											""priceIncrement"": ""0.1"",
											""priceLimitRate"": ""0.1"",
											""minFunds"": ""0.1"",
											""isMarginEnabled"": true,
											""enableTrading"": true,
											""st"": false,
											""callauctionIsEnabled"": false,
											""callauctionPriceFloor"": null,
											""callauctionPriceCeiling"": null,
											""callauctionFirstStageStartTime"": null,
											""callauctionSecondStageStartTime"": null,
											""callauctionThirdStageStartTime"": null,
											""tradingStartTime"": null
									}
                ]
            }";
			var api = await CreateKuCoinAPI(json);

			var marketSymbolsMetadata = (await api.GetMarketSymbolsMetadataAsync()).ToArray();

			marketSymbolsMetadata.Should().HaveCount(1);
			marketSymbolsMetadata[0].MarketSymbol.Should().Be("BTC-USDT");
			marketSymbolsMetadata[0].BaseCurrency.Should().Be("BTC");
			marketSymbolsMetadata[0].QuoteCurrency.Should().Be("USDT");
			marketSymbolsMetadata[0].IsActive.Should().BeTrue();
			marketSymbolsMetadata[0].IsDelistingCandidate.Should().BeFalse();
		}
	}
}
