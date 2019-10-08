using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
	// ReSharper disable once InconsistentNaming
	public sealed class ExchangeBL3PAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.bl3p.eu/1/";

		public ExchangeBL3PAPI()
		{
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
			var result = await MakeJsonRequestAsync<JObject>($"/{marketSymbol}/ticker");

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
				Ask = result["ask"].ConvertInvariant<decimal>(),
				Bid = result["bid"].ConvertInvariant<decimal>(),
				Last = result["last"].ConvertInvariant<decimal>(),
				Volume = new ExchangeVolume
				{
					Timestamp = CryptoUtility.UtcNow,
					BaseCurrency = marketSymbol.Substring(0, 3),
					BaseCurrencyVolume = result["volume"]["24h"].ConvertInvariant<decimal>()
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
