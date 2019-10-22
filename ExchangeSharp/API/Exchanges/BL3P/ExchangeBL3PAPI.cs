using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp.API.Exchanges.BL3P;
using ExchangeSharp.API.Exchanges.BL3P.Enums;
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

		/// <summary>
		/// The default currency that will be used when calling <see cref="ExchangeAPI.PlaceOrderAsync"/> <para/>
		/// You can also use the parameter <code>fee_currency</code> to set it per request.
		/// </summary>
		public BL3PCurrencyFee DefaultFeeCurrency { get; set; } = BL3PCurrencyFee.BTC;

		public ExchangeBL3PAPI()
		{
			MarketSymbolIsUppercase = true;
			MarketSymbolIsReversed = true;
			MarketSymbolSeparator = string.Empty;
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;

			RateLimit = new RateGate(600, TimeSpan.FromMinutes(10));
		}

		public ExchangeBL3PAPI(ref string publicApiKey, ref string privateApiKey)
			: this()
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

		protected override Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
			request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

			//TODO: Override this method
			if (CanMakeAuthenticatedRequest(payload))
			{
				//TODO: Check if payload is being added to uri.Query
				request.AddHeader("Rest-Key", PublicApiKey.ToUnsecureString());
			}

			var postdata = $"{request.RequestUri.AbsolutePath}\0{request.RequestUri.Query}";
			var decoded = Convert.FromBase64String(PrivateApiKey.ToUnsecureString());
			byte[] hashBytes;
			using (var hmacSha512 = new HMACSHA512(decoded))
			{
				hashBytes = hmacSha512.ComputeHash(Encoding.UTF8.GetBytes(postdata));
			}


			var signKey1 = CryptoUtility.SHA512Sign(postdata, decoded);
			var signKey = hashBytes.ToHexString();

			request.AddHeader("Rest-Sign", signKey);

			//TODO: Calculate
			request.AddHeader("Content-Length", (request.RequestUri.PathAndQuery.Length * sizeof(char)).ToString());

			return base.ProcessRequestAsync(request, payload);
		}

		protected override (string baseCurrency, string quoteCurrency) OnSplitMarketSymbolToCurrencies(
			string marketSymbol)
		{
			//TODO: Implement
			return base.OnSplitMarketSymbolToCurrencies(marketSymbol);
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
		{
			var amountInt = (long) (order.RoundAmount() * 100000000L);

			var data = new Dictionary<string, object>
			{
				{"amount_int", amountInt},
				{"type", order.IsBuy ? "bid" : "ask"},
				{"fee_currency", (order.ExtraParameters["fee_currency"] ?? DefaultFeeCurrency).ToString()},
			};

			switch (order.OrderType)
			{
				case OrderType.Limit:
					data["price_int"] = (long) (order.Price * 100000L);
					break;
				case OrderType.Market:
					data["amount_funds_int"] = (long) ((order.RoundAmount() * order.Price) / 100000L);
					break;
				default:
					throw new NotSupportedException($"{order.OrderType} is not supported");
			}

			var result = await MakeRequestAsync($"/{order.MarketSymbol}/money/order/add", payload: data, method: "POST")
				.ConfigureAwait(false);


			//TODO: Result
			return new ExchangeOrderResult
			{
			};
		}

		protected override Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] order)
		{
			Debug.WriteLine(
				"Splitting orders in single order calls as BL3P does not support batch operations yet",
				"WARN"
			);
			return Task.WhenAll(order.Select(OnPlaceOrderAsync));
		}
	}
}
