using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests;

[TestClass]
public class MEXCAPITests
{
	private const string MarketSymbol = "ETHBTC";
	private static IExchangeAPI _api;

	[AssemblyInitialize]
	public static async Task AssemblyInitialize(TestContext testContext)
	{
		_api = await ExchangeAPI.GetExchangeAPIAsync<ExchangeMEXCAPI>();
	}

	[TestMethod]
	public async Task GetMarketSymbolsMetadataAsyncShouldReturnSymbols()
	{
		var symbols = (await _api.GetMarketSymbolsMetadataAsync()).ToImmutableArray();
		symbols.Should().NotBeNull();
		foreach (var symbol in symbols)
		{
			symbol.MarketSymbol.Should().NotBeNull();
			symbol.BaseCurrency.Should().NotBeNull();
			symbol.QuoteCurrency.Should().NotBeNull();
		}
	}

	[TestMethod]
	public async Task GetMarketSymbolsAsyncShouldReturnSymbols()
	{
		var symbols = (await _api.GetMarketSymbolsAsync()).ToImmutableArray();
		symbols.Should().NotBeNull();
		foreach (var symbol in symbols)
		{
			symbol.Should().NotBeNull();
		}
	}

	[TestMethod]
	public async Task GetTickersAsyncShouldReturnTickers()
	{
		var tickers = (await _api.GetTickersAsync()).ToImmutableArray();
		tickers.Should().NotBeNull();
		foreach (var t in tickers)
		{
			t.Key.Should().NotBeNull();
			t.Value.MarketSymbol.Should().NotBeNull();
			t.Value.Exchange.Should().NotBeNull();
			t.Value.Volume.Should().NotBeNull();
		}
	}

	[TestMethod]
	public async Task GetTickerAsyncShouldReturnTicker()
	{
		var ticker = await _api.GetTickerAsync(MarketSymbol);
		ticker.Should().NotBeNull();
		ticker.MarketSymbol.Should().NotBeNull();
		ticker.Exchange.Should().NotBeNull();
		ticker.Volume.Should().NotBeNull();
	}

	[TestMethod]
	public async Task GetOrderBookAsyncShouldReturlOrderBookData()
	{
		var orderBook = await _api.GetOrderBookAsync(MarketSymbol);
		orderBook.MarketSymbol.Should().NotBeNullOrEmpty();
		orderBook.Asks.Should().NotBeNull();
		orderBook.Bids.Should().NotBeNull();
	}

	[TestMethod]
	public async Task GetRecentTradesAsyncShouldReturnTrades()
	{
		var recentTrades = await _api.GetRecentTradesAsync(MarketSymbol);
		recentTrades.Should().NotBeNull();
	}

	[TestMethod]
	public async Task GetCandlesAsyncShouldReturnCandleData()
	{
		var klines = (await _api.GetCandlesAsync(MarketSymbol, 3600)).ToArray();
		klines.Should().NotBeNull();
		klines.Length.Should().NotBe(0);
	}
}
