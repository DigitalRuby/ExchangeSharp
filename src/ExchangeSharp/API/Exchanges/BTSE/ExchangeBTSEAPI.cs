namespace ExchangeSharp {
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public sealed partial class ExchangeBTSEAPI :ExchangeAPI {
		public override string BaseUrl { get; set; } = "https://api.btse.com";

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await GetTickersAsync()).Select(pair => pair.Key);
		}

		protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			JToken allPairs = await MakeJsonRequestAsync<JArray>("/spot/api/v3/market_summary");
			var tasks =  allPairs.Select(async token => await this.ParseTickerAsync(token, token["symbol"].Value<string>(), "lowestAsk", "highestBid", "last", "volume", null,
				null, TimestampType.UnixMilliseconds, "base", "quote", "symbol"));

			return (await Task.WhenAll(tasks)).Select(ticker =>
				new KeyValuePair<string, ExchangeTicker>(ticker.MarketSymbol, ticker));
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			JToken ticker = await MakeJsonRequestAsync<JObject>("/spot/api/v3/market_summary", null, new Dictionary<string, object>()
			{
				{"symbol", marketSymbol}
			});
			return await this.ParseTickerAsync(ticker, marketSymbol, "lowestAsk", "highestBid", "last", "volume", null,
				null, TimestampType.UnixMilliseconds, "base", "quote", "symbol");
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null,
			int? limit = null)
		{
			var payload = new Dictionary<string, object>()
			{
				{"symbol", marketSymbol},
				{"resolution", periodSeconds}
			};

			if (startDate != null)
			{
				payload.Add("start", startDate.Value.UnixTimestampFromDateTimeMilliseconds());
			}
			if (endDate != null)
			{
				payload.Add("end", startDate.Value.UnixTimestampFromDateTimeMilliseconds());
			}

			JToken ticker = await MakeJsonRequestAsync<JArray>("/spot/api/v3/ohlcv", null, payload, "GET");
			return ticker.Select(token =>
				this.ParseCandle(token, marketSymbol, periodSeconds, 1, 2, 3, 4, 0, TimestampType.UnixMilliseconds, 5));
		}

		protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
		{
			if ( method == "GET" && (payload?.Count??0) != 0)
			{
				url.AppendPayloadToQuery(payload);
			}
			return base.ProcessRequestUrl(url, payload, method);
		}
	}

	public partial class ExchangeName { public const string BTSE = "BTSE"; }
}
