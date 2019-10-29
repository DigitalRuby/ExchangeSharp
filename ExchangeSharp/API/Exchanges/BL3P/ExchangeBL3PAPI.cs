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
using ExchangeSharp.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
	// ReSharper disable once InconsistentNaming
	public sealed class ExchangeBL3PAPI : ExchangeAPI
	{
		private readonly FixedIntDecimalConverter converterToEight;

		private readonly FixedIntDecimalConverter converterToFive;

		public override string BaseUrl { get; set; } = "https://api.bl3p.eu/1";

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
			RequestContentType = "application/x-www-form-urlencoded";

			RateLimit = new RateGate(600, TimeSpan.FromMinutes(10));

			converterToEight = new FixedIntDecimalConverter(8);
			converterToFive = new FixedIntDecimalConverter(5);
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

		protected override bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
		{
			return !(PublicApiKey is null) && !(PrivateApiKey is null);
		}

		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
			var formData = await request.WritePayloadFormToRequestAsync(payload)
				.ConfigureAwait(false);

			if (CanMakeAuthenticatedRequest(payload))
			{
				request.AddHeader("Rest-Key", PublicApiKey.ToUnsecureString());

				var signKey = GetSignKey(request, formData);
				request.AddHeader("Rest-Sign", signKey);
			}
		}

		private string GetSignKey(IHttpWebRequest request, string formData)
		{
			//TODO: Use csharp8 ranges
			var index = Array.IndexOf(request.RequestUri.Segments, "1/");
			var callPath = string.Join(string.Empty, request.RequestUri.Segments.Skip(index + 1)).TrimStart('/');
			var postData = $"{callPath}\0{formData}";
			var privBase64 = Convert.FromBase64String(PrivateApiKey.ToUnsecureString());

			byte[] hashBytes;
			using (var hmacSha512 = new HMACSHA512(privBase64))
			{
				hashBytes = hmacSha512.ComputeHash(Encoding.UTF8.GetBytes(postData));
			}

			return Convert.ToBase64String(hashBytes);
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
		{
			var roundedAmount = order.RoundAmount();
			var amountInt = converterToEight.FromDecimal(roundedAmount);

			var feeCurrency = (order.ExtraParameters.ContainsKey("fee_currency")
					? order.ExtraParameters["fee_currency"] ?? DefaultFeeCurrency
					: DefaultFeeCurrency)
				.ToString()
				.ToUpperInvariant();

			var data = new Dictionary<string, object>
			{
				{"amount_int", amountInt},
				{"type", order.IsBuy ? "bid" : "ask"},
				{"fee_currency", feeCurrency},
			};

			switch (order.OrderType)
			{
				case OrderType.Limit:
					data["price_int"] = converterToFive.FromDecimal(order.Price);
					break;
				case OrderType.Market:
					data["amount_funds_int"] = converterToFive.FromDecimal(roundedAmount * order.Price);
					break;
				default:
					throw new NotSupportedException($"{order.OrderType} is not supported");
			}

			var resultBody = await MakeRequestAsync(
					$"/{order.MarketSymbol}/money/order/add",
					payload: data,
					method: "POST"
				)
				.ConfigureAwait(false);
			var result = JsonConvert.DeserializeObject<BL3POrderAddResponse>(resultBody);

			//TODO: Get data for result
			return new ExchangeOrderResult
			{
				OrderId = result.OrderId,
				Amount = order.Amount,
				AmountFilled = 0M,
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
