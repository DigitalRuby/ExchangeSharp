using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp.BL3P;
using ExchangeSharp.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace
namespace ExchangeSharp
{
#nullable enable
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
			RequestMethod = "POST";

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
			return (await OnGetMarketSymbolsMetadataAsync())
				.Select(em => em.MarketSymbol);
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			var result = await MakeJsonRequestAsync<JObject>($"/{marketSymbol}/ticker");

			return await this.ParseTickerAsync(
				result,
				marketSymbol,
				askKey: "ask",
				bidKey: "bid",
				lastKey: "last",
				baseVolumeKey: "volume.24h",
				timestampKey: "timestamp",
				timestampType: TimestampType.UnixSeconds
			);
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
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}

			Task MessageCallback(IWebSocket _, byte[] msg)
			{
				var bl3POrderBook = JsonConvert.DeserializeObject<BL3POrderBook>(msg.ToStringFromUTF8());

				var exchangeOrderBook = ConvertToExchangeOrderBook(maxCount, bl3POrderBook);

				callback(exchangeOrderBook);

				return Task.CompletedTask;
			}

			return new MultiWebsocketWrapper(
				await Task.WhenAll(
					marketSymbols.Select(ms => ConnectWebSocketAsync($"{ms}/orderbook", MessageCallback))
				)
			);
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			Task MessageCallback(IWebSocket _, byte[] msg)
			{ // {{	"date": 1573255932, "marketplace": "BTCEUR", "price_int": 802466000, "type": "buy", "amount_int": 6193344 }	}
				var token = JToken.Parse(msg.ToStringFromUTF8());
				var symbol = token["marketplace"].ToStringInvariant();
				var trade = token.ParseTrade(amountKey: "amount_int", priceKey: "price_int", typeKey: "type", timestampKey: "date",
					timestampType: TimestampType.UnixSeconds,
					idKey: null, // + TODO: add Id Key when BL3P starts providing this info
					typeKeyIsBuyValue: "buy");
				callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
				return Task.CompletedTask;
			}

			return new MultiWebsocketWrapper(
				await Task.WhenAll(
					marketSymbols.Select(ms => ConnectWebSocketAsync($"{ms}/trades", MessageCallback))
				)
			);
		}

		private static ExchangeOrderBook ConvertToExchangeOrderBook(int maxCount, BL3POrderBook bl3POrderBook)
		{
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

			return exchangeOrderBook;
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
			var privateKeyBase64 = Convert.FromBase64String(PrivateApiKey.ToUnsecureString());

			byte[] hashBytes;
			using (var hmacSha512 = new HMACSHA512(privateKeyBase64))
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
					payload: data
				);

			var result = JsonConvert.DeserializeObject<BL3POrderAddResponse>(resultBody)
				.Except();

			var orderDetails = await GetOrderDetailsAsync(result.OrderId, order.MarketSymbol);

			return orderDetails;
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
		{
			if (string.IsNullOrWhiteSpace(marketSymbol))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(marketSymbol));

			var resultBody = await MakeRequestAsync($"/{marketSymbol}/money/depth/full");

			var bl3pOrderBook = JsonConvert.DeserializeObject<BL3PReponseFullOrderBook>(resultBody)
				.Except();

			bl3pOrderBook.MarketSymbol??= marketSymbol;

			return ConvertToExchangeOrderBook(maxCount, bl3pOrderBook);
		}

		protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
		{
			if (string.IsNullOrWhiteSpace(marketSymbol))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(marketSymbol));

			var resultBody = await MakeRequestAsync(
					$"/{marketSymbol}/money/order/cancel",
					payload: new Dictionary<string, object>
					{
						{"order_id", orderId}
					}
				);

			JsonConvert.DeserializeObject<BL3PEmptyResponse>(resultBody)
				.Except();
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
		{
			if (string.IsNullOrWhiteSpace(marketSymbol))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(marketSymbol));

			var data = new Dictionary<string, object>
			{
				{"order_id", orderId}
			};

			var resultBody = await MakeRequestAsync(
					$"/{marketSymbol}/money/order/result",
					payload: data
				);


			var result = JsonConvert.DeserializeObject<BL3POrderResultResponse>(resultBody)
				.Except();

			return new ExchangeOrderResult
			{
				Amount = result.Amount.Value,
				Fees = result.TotalFee.Value,
				Message = $"Order created via: \"{result.APIKeyLabel}\"",
				Price = result.Price.Value,
				Result = result.Status.ToResult(result.TotalAmount),
				AmountFilled = result.TotalAmount.Value,
				AveragePrice = result.AverageCost?.Value ?? 0M,
				FeesCurrency = result.TotalFee.Currency,
				FillDate = result.DateClosed ?? DateTime.MinValue,
				IsBuy = result.Type == BL3POrderType.Bid,
				MarketSymbol = marketSymbol,
				OrderDate = result.Date,
				OrderId = result.OrderId,
				TradeId = result.TradeId
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
#nullable disable
}
