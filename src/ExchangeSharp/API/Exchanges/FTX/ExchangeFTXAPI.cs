using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExchangeSharp.API.Exchanges.FTX
{
	public sealed partial class ExchangeFTXAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://ftx.com/api";
		public override string BaseUrlWebSocket { get; set; } = "wss://ftx.com/ws/";

		#region [ Constructor(s) ]

		public ExchangeFTXAPI()
		{
			NonceStyle = NonceStyle.UnixMillisecondsString;
			MarketSymbolSeparator = "/";
			RequestContentType = "application/json";
			//WebSocketOrderBookType = WebSocketOrderBookType.
		}

		#endregion

		#region [ Implementation ]

		/// <inheritdoc />
		protected async override Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var balances = new Dictionary<string, decimal>();

			JToken result = await MakeJsonRequestAsync<JToken>("/wallet/balances", null, await GetNoncePayloadAsync());

			foreach (JObject obj in result)
			{
				decimal amount = obj["total"].ConvertInvariant<decimal>();

				balances[obj["coin"].ToStringInvariant()] = amount;
			}

			return balances;
		}

		/// <inheritdoc />
		protected async override Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
		{
			string baseUrl = $"/markets/{marketSymbol}/trades?";

			if (startDate != null)
			{
				baseUrl += $"&start_time={startDate?.UnixTimestampFromDateTimeMilliseconds()}";
			}

			if (endDate != null)
			{
				baseUrl += $"&end_time={endDate?.UnixTimestampFromDateTimeMilliseconds()}";
			}

			List<ExchangeTrade> trades = new List<ExchangeTrade>();

			while (true)
			{
				JToken result = await MakeJsonRequestAsync<JToken>(baseUrl);

				foreach (JToken trade in result.Children())
				{
					trades.Add(trade.ParseTrade("size", "price", "side", "time", TimestampType.Iso8601, "id", "buy"));
				}

				if (!callback(trades))
				{
					break;
				}

				Task.Delay(1000).Wait();
			}
		}

		/// <inheritdoc />
		protected async override Task<IEnumerable<string>> OnGetMarketSymbolsAsync(bool isWebSocket = false)
		{
			JToken result = await MakeJsonRequestAsync<JToken>("/markets");

			var names = result.Children().Select(x => x["name"].ToStringInvariant()).Where(x => Regex.Match(x, @"[\w\d]*\/[[\w\d]]*").Success).ToList();

			names.Sort();

			return names;
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
		protected async override Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
		{
			// https://docs.ftx.com/#get-open-orders


			var markets = new List<ExchangeOrderResult>();

			JToken result = await MakeJsonRequestAsync<JToken>($"/orders?market={marketSymbol}");

			foreach (JToken token in result.Children())
			{
				var symbol = token["name"].ToStringInvariant();

				if (!Regex.Match(symbol, @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}
				
				markets.Add(new ExchangeOrderResult()
				{
					MarketSymbol = token["market"].ToStringInvariant(),
					Price = token["price"].ConvertInvariant<decimal>(),
					AveragePrice = token["avgFillPrice"].ConvertInvariant<decimal>(),
					OrderDate = token["createdAt"].ConvertInvariant<DateTime>(),
					IsBuy = token["side"].ToStringInvariant().Equals("buy"),
					OrderId = token["id"].ToStringInvariant(),
					Amount = token["size"].ConvertInvariant<decimal>(),
					AmountFilled = token["filledSize"].ConvertInvariant<decimal>(),
					ClientOrderId = token["clientId"].ToStringInvariant()
				});
			}

			return markets;
		}

		/// <inheritdoc />
		protected async override Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
		{
			// https://docs.ftx.com/#get-order-status

			JToken result = await MakeJsonRequestAsync<JToken>($"/orders?{orderId}");

			var resp = result.First();

			return new ExchangeOrderResult()
			{
				OrderId = resp["id"].ToStringInvariant(),
				OrderDate = resp["createdAt"].ConvertInvariant<DateTime>(),
				Result = resp["id"].ToStringLowerInvariant().ToExchangeAPIOrderResult()
			};
		}

		/// <inheritdoc />
		protected async override Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
		{
			// https://docs.ftx.com/#get-balances
			// NOTE there is also is "Get balances of all accounts"?
			// "coin": "USDTBEAR",
			// "free": 2320.2,
			// "spotBorrow": 0.0,
			// "total": 2340.2,
			// "usdValue": 2340.2,
			// "availableWithoutBorrow": 2320.2

			var balances = new Dictionary<string, decimal>();

			JToken result = await MakeJsonRequestAsync<JToken>($"/wallet/balances");

			foreach (JToken token in result.Children())
			{
				if (!Regex.Match(token["coin"].ToStringInvariant(), @"[\w\d]*\/[[\w\d]]*").Success)
				{
					continue;
				}

				balances.Add(token["coin"].ToStringInvariant(),
					token["availableWithoutBorrow"].ConvertInvariant<decimal>());
			}

			return balances;
		}

		protected async override Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
		{
			//{
			//	"market": "XRP-PERP",
			//  "side": "sell",
			//  "price": 0.306525,
			//  "type": "limit",
			//  "size": 31431.0,
			//  "reduceOnly": false,
			//  "ioc": false,
			//  "postOnly": false,
			//  "clientId": null
			//}

			IEnumerable<ExchangeMarket> markets = await OnGetMarketSymbolsMetadataAsync();
			ExchangeMarket market = markets.Where(m => m.MarketSymbol == order.MarketSymbol).First();

			var payload = await GetNoncePayloadAsync();

			var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
			{
				{"market", market.MarketSymbol},
				{"side", order.IsBuy ? "buy" : "sell" },
				{"type", order.OrderType.ToStringLowerInvariant() },
				{"size", order.RoundAmount() },	
			};

			if (!string.IsNullOrEmpty(order.ClientOrderId))
			{
				parameters.Add("clientId", order.ClientOrderId);
			}

			if (order.OrderType != OrderType.Market)
			{
				int precision = BitConverter.GetBytes(decimal.GetBits((decimal)market.PriceStepSize)[3])[2];

				if (order.Price == null) throw new ArgumentNullException(nameof(order.Price));

				parameters.Add("price", Math.Round(order.Price.Value, precision));
			}

			parameters.CopyTo(payload);

			order.ExtraParameters.CopyTo(payload);

			var response = await MakeJsonRequestAsync<JToken>("/orders", null, payload, "POST");

			ExchangeOrderResult result = new ExchangeOrderResult
			{
				OrderId = response["id"].ToStringInvariant(),
				ClientOrderId = response["clientId"].ToStringInvariant(),
				OrderDate = CryptoUtility.ToDateTimeInvariant(response["createdAt"]),
				Price = CryptoUtility.ConvertInvariant<decimal>(response["price"]),
				AmountFilled = CryptoUtility.ConvertInvariant<decimal>(response["filledSize"]),
				AveragePrice = CryptoUtility.ConvertInvariant<decimal>(response["avgFillPrice"]),
				Amount  = CryptoUtility.ConvertInvariant<decimal>(response["size"]),
				MarketSymbol = response["market"].ToStringInvariant(),
				IsBuy = response["side"].ToStringInvariant() == "buy"
			};

			return result;
		}

		/// <inheritdoc />
		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers, params string[] marketSymbols)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync(true)).ToArray();
			}
			return await ConnectPublicWebSocketAsync(null, messageCallback: async (_socket, msg) =>
			{
				JToken parsedMsg = JToken.Parse(msg.ToStringFromUTF8());

				if (parsedMsg["channel"].ToStringInvariant().Equals("ticker"))
				{
					JToken data = parsedMsg["data"];

					var exchangeTicker = await this.ParseTickerAsync(data, parsedMsg["market"].ToStringInvariant(), "ask", "bid", "last", null, null, "time", TimestampType.UnixSecondsDouble);

					var kv = new KeyValuePair<string, ExchangeTicker>(exchangeTicker.MarketSymbol, exchangeTicker);

					tickers(new List<KeyValuePair<string, ExchangeTicker>> { kv });
				}
			}, connectCallback: async (_socket) =>
			{
				List<string> marketSymbolList = marketSymbols.ToList();

				//{'op': 'subscribe', 'channel': 'trades', 'market': 'BTC-PERP'}

				for (int i = 0; i < marketSymbolList.Count; i++)
				{
					await _socket.SendMessageAsync(new
					{
						op = "subscribe",
						market = marketSymbolList[i],
						channel = "ticker"
					});
				}				
			});
		}

		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				string timestamp = payload["nonce"].ToStringInvariant();

				payload.Remove("nonce");

				string form = CryptoUtility.GetJsonForPayload(payload);

				//Create the signature payload
				string toHash = $"{timestamp}{request.Method.ToUpperInvariant()}{request.RequestUri.PathAndQuery}";

				if (request.Method == "POST")
				{
					toHash += form;

					await CryptoUtility.WriteToRequestAsync(request, form);
				}

				byte[] secret = CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey);

				string signatureHexString = CryptoUtility.SHA256Sign(toHash, secret);

				request.AddHeader("FTX-KEY", PublicApiKey.ToUnsecureString());
				request.AddHeader("FTX-SIGN", signatureHexString);
				request.AddHeader("FTX-TS", timestamp);
			}
		}

		protected override Task OnInitializeAsync()
		{
			return base.OnInitializeAsync();
		}

		protected async override Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
		{
			JToken response = await MakeJsonRequestAsync<JToken>($"/markets/{marketSymbol}/orderbook?depth={maxCount}");

			return ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(response,  maxCount: maxCount);
		}

		protected async override Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
		{

			//period options: 15, 60, 300, 900, 3600, 14400, 86400, or any multiple of 86400 up to 30*86400

			var queryUrl = $"/markets/{marketSymbol}/candles?resolution={periodSeconds}";

			if (startDate.HasValue)
			{
				queryUrl += $"&start_time={startDate?.UnixTimestampFromDateTimeSeconds()}"; 
			}

			if (endDate.HasValue)
			{
				queryUrl += $"&end_time={endDate?.UnixTimestampFromDateTimeSeconds()}";
			}

			var candles = new List<MarketCandle>();

			var response = await MakeJsonRequestAsync<JToken>(queryUrl, null, await GetNoncePayloadAsync());

			foreach (JToken candle in response.Children())
			{
				var parsedCandle = this.ParseCandle(candle, marketSymbol, periodSeconds, "open", "high", "low", "close", "startTime", TimestampType.Iso8601, "volume");

				candles.Add(parsedCandle);
			}

			return candles;
		}

		protected override Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			return base.OnGetCompletedOrderDetailsAsync(marketSymbol, afterDate);
		}

		protected async override Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
		{
			var response = await MakeJsonRequestAsync<JToken>($"/orders/{orderId}", null, await GetNoncePayloadAsync(), "DELETE");
		}

		public override Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
		{
			return base.ExchangeMarketSymbolToGlobalMarketSymbolAsync(marketSymbol);
		}

		public override Task<(string baseCurrency, string quoteCurrency)> ExchangeMarketSymbolToCurrenciesAsync(string marketSymbol)
		{
			return base.ExchangeMarketSymbolToCurrenciesAsync(marketSymbol);
		}

		public override Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
		{
			return base.GlobalMarketSymbolToExchangeMarketSymbolAsync(marketSymbol);
		}

		public override Task<IReadOnlyDictionary<string, ExchangeCurrency>> GetCurrenciesAsync()
		{
			return base.GetCurrenciesAsync();
		}

		public override Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync()
		{
			return base.GetTickersAsync();
		}

		#endregion
	}
}
