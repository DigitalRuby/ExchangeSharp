using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp.API.Exchanges.Bl3p
{
	public sealed class ExchangeBl3pAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.bl3p.eu/";

		public ExchangeBl3pAPI()
		{
		}

		public ExchangeBl3pAPI(ref string publicApiKey, ref string privateApiKey)
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

		protected override Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return Task.FromResult(new[]
			{
				// For now we only support these two coins
				"BTCEUR",
				"LTCEUR"
			} as IEnumerable<string>);
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var result = await MakeJsonRequestAsync<JObject>($"/1/{marketSymbol}/ticker");

			return ParseTickerResponse(result, marketSymbol);
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

		private ExchangeTicker ParseTickerResponse(JObject result, string marketSymbol)
		{
			if (result == null)
				return null;

			return new ExchangeTicker
			{
				Ask = result.Value<decimal>("ask"),
				Bid = result.Value<decimal>("bid"),
				Last = result.Value<decimal>("last"),
				Volume = new ExchangeVolume
				{
					Timestamp = CryptoUtility.UtcNow,
					BaseCurrency = marketSymbol.Substring(0, 3),
					BaseCurrencyVolume = result["volume"].Value<decimal>("24h")
				},
				MarketSymbol = marketSymbol
			};
		}

		public partial class ExchangeName
		{
			public const string Bl3p = "BL3P";
		}
	}
}
