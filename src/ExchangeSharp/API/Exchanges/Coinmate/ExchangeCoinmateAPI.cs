using ExchangeSharp.API.Exchanges.Coinmate.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExchangeSharp
{
	public class ExchangeCoinmateAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://coinmate.io/api";

		public ExchangeCoinmateAPI()
		{
			MarketSymbolSeparator = "_";
		}

		public override string Name => "Coinmate";

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var response = await MakeCoinmateRequest<JToken>($"/ticker?currencyPair={marketSymbol}");
			return await this.ParseTickerAsync(response, marketSymbol, "ask", "bid", "last", "amount", null, "timestamp", TimestampType.UnixSeconds);
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			var response = await MakeCoinmateRequest<CoinmateSymbol[]>("/products");
			return response.Select(x => $"{x.FromSymbol}{MarketSymbolSeparator}{x.ToSymbol}").ToArray();
		}

		protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			var response = await MakeCoinmateRequest<CoinmateTradingPair[]>("/tradingPairs");
			return response.Select(x => new ExchangeMarket
			{
				IsActive = true,
				BaseCurrency = x.FirstCurrency,
				QuoteCurrency = x.SecondCurrency,
				MarketSymbol = x.Name,
				MinTradeSize = x.MinAmount,
				PriceStepSize = 1 / (decimal)(Math.Pow(10, x.PriceDecimals)),
				QuantityStepSize = 1 / (decimal)(Math.Pow(10, x.LotDecimals))
			}).ToArray();
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
		{
			var book = await MakeCoinmateRequest<CoinmateOrderBook>("/orderBook?&groupByPriceLimit=False&currencyPair=" + marketSymbol);
			var result = new ExchangeOrderBook
			{
				MarketSymbol = marketSymbol,	
			};

			book.Asks
				.GroupBy(x => x.price)
				.ToList()
				.ForEach(x => result.Asks.Add(x.Key, new ExchangeOrderPrice { Amount = x.Sum(x => x.amount), Price = x.Key }));

			book.Bids
				.GroupBy(x => x.price)
				.ToList()
				.ForEach(x => result.Bids.Add(x.Key, new ExchangeOrderPrice { Amount = x.Sum(x => x.amount), Price = x.Key }));

			return result;
		}

		private async Task<T> MakeCoinmateRequest<T>(string url)
		{
			var response = await MakeJsonRequestAsync<CoinmateResponse<T>>(url);

			if (response.Error)
			{
				throw new APIException(response.ErrorMessage);
			}

			return response.Data;
		}
	}
}
