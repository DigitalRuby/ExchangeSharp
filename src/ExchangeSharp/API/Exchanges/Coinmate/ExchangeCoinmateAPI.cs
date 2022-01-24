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
			RequestContentType = "application/x-www-form-urlencoded";
			MarketSymbolSeparator = "_";
			NonceStyle = NonceStyle.UnixMilliseconds;
		}

		public override string Name => "Coinmate";

		/// <summary>
		/// Coinmate private API requires a client id. Internally this is secured in the PassPhrase property.
		/// </summary>
		public string ClientId
		{
			get { return Passphrase.ToUnsecureString(); }
			set { Passphrase = value.ToSecureString(); }
		}

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

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
		{
			var txs = await MakeCoinmateRequest<CoinmateTransaction[]>("/transactions?minutesIntoHistory=1440&currencyPair=" + marketSymbol);
			return txs.Select(x => new ExchangeTrade
			{
				Amount = x.Amount,
				Id = x.TransactionId,
				IsBuy = x.TradeType == "BUY",
				Price = x.Price,
				Timestamp = CryptoUtility.ParseTimestamp(x.Timestamp, TimestampType.UnixMilliseconds)
			})
			.Take(limit ?? int.MaxValue)
			.ToArray();
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var payload = await GetNoncePayloadAsync();
			var balances = await MakeCoinmateRequest<Dictionary<string, CoinmateBalance>>("/balances", payload, "POST");

			return balances.ToDictionary(x => x.Key, x => x.Value.Balance);
		}

		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				if (string.IsNullOrWhiteSpace(ClientId))
				{
					throw new APIException("Client ID is not set for Coinmate");
				}

				var apiKey = PublicApiKey.ToUnsecureString();
				var messageToSign = payload["nonce"].ToStringInvariant() + ClientId + apiKey;
				var signature = CryptoUtility.SHA256Sign(messageToSign, PrivateApiKey.ToUnsecureString()).ToUpperInvariant();
				payload["signature"] = signature;
				payload["clientId"] = ClientId;
				payload["publicKey"] = apiKey;
				await CryptoUtility.WritePayloadFormToRequestAsync(request, payload);
			}
		}

		private async Task<T> MakeCoinmateRequest<T>(string url, Dictionary<string, object> payload = null, string method = null)
		{
			var response = await MakeJsonRequestAsync<CoinmateResponse<T>>(url, null, payload, method);

			if (response.Error)
			{
				throw new APIException(response.ErrorMessage);
			}

			return response.Data;
		}
	}
}
