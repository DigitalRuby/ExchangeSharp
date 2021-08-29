using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExchangeSharp.API.Exchanges.FTX.Models
{
	public sealed partial class ExchangeFTXAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://ftx.com/api";
		public override string BaseUrlWebSocket { get; set; } = "wss://ftx.com/ws/";

		protected async override Task<IEnumerable<string>> OnGetMarketSymbolsAsync(bool isWebSocket = false)
		{
			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			var names = result.Children().Select(x => x["name"].ToStringInvariant()).Where(x => Regex.Match(x, @"[\w\d]*\/[[\w\d]]*").Success).ToList();

			names.Sort();

			return names;
		}

		protected async internal override Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
		{
			//{
			//	"name": "BTC-0628",
			//	"baseCurrency": null,
			//	"quoteCurrency": null,
			//	"quoteVolume24h": 28914.76,
			//	"change1h": 0.012,
			//	"change24h": 0.0299,
			//	"changeBod": 0.0156,
			//	"highLeverageFeeExempt": false,
			//	"minProvideSize": 0.001,
			//	"type": "future",
			//	"underlying": "BTC",
			//	"enabled": true,
			//	"ask": 3949.25,
			//	"bid": 3949,
			//	"last": 10579.52,
			//	"postOnly": false,
			//	"price": 10579.52,
			//	"priceIncrement": 0.25,
			//	"sizeIncrement": 0.0001,
			//	"restricted": false,
			//	"volumeUsd24h": 28914.76
			//}

			var markets = new List<ExchangeMarket>();

			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			foreach (JToken token in result.Children())
			{
				var symbol = token["name"].ToStringInvariant();

				if (!Regex.Match(symbol, @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}

				var market = new ExchangeMarket()
				{
					MarketSymbol = symbol,
					BaseCurrency = token["baseCurrency"].ToStringInvariant(),
					QuoteCurrency = token["quoteCurrency"].ToStringInvariant(),
					PriceStepSize = token["priceIncrement"].ConvertInvariant<decimal>(),
					QuantityStepSize = token["sizeIncrement"].ConvertInvariant<decimal>(),
					MinTradeSize = token["minProvideSize"].ConvertInvariant<decimal>(),
					IsActive = token["enabled"].ConvertInvariant<bool>(),
				};

				markets.Add(market);
			}

			return markets;
		}
	}
}
