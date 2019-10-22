using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.API.Exchanges.BL3P;
using ExchangeSharp.API.Exchanges.BL3P.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
	// ReSharper disable once InconsistentNaming
	public sealed class ExchangeBL3PAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.bl3p.eu/";

		public override string BaseUrlWebSocket { get; set; } = "wss://api.bl3p.eu/1/";

		public ExchangeBL3PAPI()
		{
			MarketSymbolIsUppercase = true;
			MarketSymbolIsReversed = true;
			MarketSymbolSeparator = string.Empty;
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;
		}

		public ExchangeBL3PAPI(ref string publicApiKey, ref string privateApiKey)
		{
			if (publicApiKey == null)
				throw new ArgumentNullException(nameof(publicApiKey));
			if (privateApiKey == null)
				throw new ArgumentNullException(nameof(privateApiKey));

			PublicApiKey = publicApiKey.ToSecureString();
			publicApiKey = null;
			PrivateApiKey = privateApiKey.ToSecureString();
			privateApiKey = null;
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await OnGetMarketSymbolsMetadataAsync().ConfigureAwait(false))
				.Select(em => em.MarketSymbol);
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var result = await MakeJsonRequestAsync<JObject>($"/{marketSymbol}/ticker")
				.ConfigureAwait(false);

			return await this.ParseTickerAsync(
				result,
				marketSymbol,
				askKey: "ask",
				bidKey: "bid",
				lastKey: "last",
				baseVolumeKey: "volume.24h",
				timestampKey: "timestamp",
				timestampType: TimestampType.UnixSeconds
			).ConfigureAwait(false);
		}

		protected internal override Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			return Task.FromResult(new[]
			{
				// For now we only support these two coins
				new ExchangeMarket
				{
					BaseCurrency = "BTC",
					IsActive = true,
					MarketSymbol = "BTCEUR",
					QuoteCurrency = "EUR"
				},
				new ExchangeMarket
				{
					BaseCurrency = "LTC",
					IsActive = true,
					MarketSymbol = "LTCEUR",
					QuoteCurrency = "EUR"
				}
			} as IEnumerable<ExchangeMarket>);
		}

		protected override Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
		{
			return Task.WhenAll(
				OnGetTickerAsync("BTCEUR"),
				OnGetTickerAsync("LTCEUR")
			).ContinueWith(
				r => r.Result.ToDictionary(t => t.MarketSymbol, t => t).AsEnumerable(),
				TaskContinuationOptions.OnlyOnRanToCompletion
			);
		}

		protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(
			Action<ExchangeOrderBook> callback,
			int maxCount = 20,
			params string[] marketSymbols
		)
		{
			Task MessageCallback(IWebSocket _, byte[] msg)
			{
				var bl3POrderBook = JsonConvert.DeserializeObject<BL3POrderBook>(msg.ToStringFromUTF8());

				var exchangeOrderBook = new ExchangeOrderBook
				{
					MarketSymbol = bl3POrderBook.MarketSymbol,
					LastUpdatedUtc = CryptoUtility.UtcNow
				};

				var asks = bl3POrderBook.Asks
					.OrderBy(b => b.Price, exchangeOrderBook.Asks.Comparer)
					.Take(maxCount);
				foreach (var ask in asks)
				{
					exchangeOrderBook.Asks.Add(ask.Price, ask.ToExchangeOrder());
				}

				var bids = bl3POrderBook.Bids
					.OrderBy(b => b.Price, exchangeOrderBook.Bids.Comparer)
					.Take(maxCount);
				foreach (var bid in bids)
				{
					exchangeOrderBook.Bids.Add(bid.Price, bid.ToExchangeOrder());
				}

				callback(exchangeOrderBook);

				return Task.CompletedTask;
			}

			return new MultiWebsocketWrapper(
				await Task.WhenAll(
					marketSymbols.Select(ms => ConnectWebSocketAsync($"{ms}/orderbook", MessageCallback))
				).ConfigureAwait(false)
			);
		}

		public partial class ExchangeName
		{
			public const string BL3P = "BL3P";
		}
	}
}
