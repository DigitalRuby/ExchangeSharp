using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.API.Exchanges.Coinmate.Models;
using Newtonsoft.Json.Linq;

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
			var response = await MakeCoinmateRequest<JToken>(
					$"/ticker?currencyPair={marketSymbol}"
			);
			return await this.ParseTickerAsync(
					response,
					marketSymbol,
					"ask",
					"bid",
					"last",
					"amount",
					null,
					"timestamp",
					TimestampType.UnixSeconds
			);
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			var response = await MakeCoinmateRequest<CoinmateSymbol[]>("/products");
			return response
					.Select(x => $"{x.FromSymbol}{MarketSymbolSeparator}{x.ToSymbol}")
					.ToArray();
		}

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			var response = await MakeCoinmateRequest<CoinmateTradingPair[]>("/tradingPairs");
			return response
					.Select(
							x =>
									new ExchangeMarket
									{
										IsActive = true,
										BaseCurrency = x.FirstCurrency,
										QuoteCurrency = x.SecondCurrency,
										MarketSymbol = x.Name,
										MinTradeSize = x.MinAmount,
										PriceStepSize = 1 / (decimal)(Math.Pow(10, x.PriceDecimals)),
										QuantityStepSize = 1 / (decimal)(Math.Pow(10, x.LotDecimals))
									}
					)
					.ToArray();
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			var book = await MakeCoinmateRequest<CoinmateOrderBook>(
					"/orderBook?&groupByPriceLimit=False&currencyPair=" + marketSymbol
			);
			var result = new ExchangeOrderBook { MarketSymbol = marketSymbol, };

			book.Asks
					.GroupBy(x => x.Price)
					.ToList()
					.ForEach(
							x =>
									result.Asks.Add(
											x.Key,
											new ExchangeOrderPrice { Amount = x.Sum(x => x.Amount), Price = x.Key }
									)
					);

			book.Bids
					.GroupBy(x => x.Price)
					.ToList()
					.ForEach(
							x =>
									result.Bids.Add(
											x.Key,
											new ExchangeOrderPrice { Amount = x.Sum(x => x.Amount), Price = x.Key }
									)
					);

			return result;
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(
				string marketSymbol,
				int? limit = null
		)
		{
			var txs = await MakeCoinmateRequest<CoinmateTransaction[]>(
					"/transactions?minutesIntoHistory=1440&currencyPair=" + marketSymbol
			);
			return txs.Select(
							x =>
									new ExchangeTrade
									{
										Amount = x.Amount,
										Id = x.TransactionId,
										IsBuy = x.TradeType == "BUY",
										Price = x.Price,
										Timestamp = CryptoUtility.ParseTimestamp(
													x.Timestamp,
													TimestampType.UnixMilliseconds
											)
									}
					)
					.Take(limit ?? int.MaxValue)
					.ToArray();
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			var payload = await GetNoncePayloadAsync();
			var balances = await MakeCoinmateRequest<Dictionary<string, CoinmateBalance>>(
					"/balances",
					payload,
					"POST"
			);

			return balances.ToDictionary(x => x.Key, x => x.Value.Balance);
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			var payload = await GetNoncePayloadAsync();
			var balances = await MakeCoinmateRequest<Dictionary<string, CoinmateBalance>>(
					"/balances",
					payload,
					"POST"
			);

			return balances.ToDictionary(x => x.Key, x => x.Value.Available);
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			var payload = await GetNoncePayloadAsync();

			CoinmateOrder o;

			if (isClientOrderId)
			{
				payload["clientOrderId"] = orderId;
				var orders = await MakeCoinmateRequest<CoinmateOrder[]>("/order", payload, "POST");
				o = orders.OrderByDescending(x => x.Timestamp).FirstOrDefault();
			}
			else
			{
				payload["orderId"] = orderId;
				o = await MakeCoinmateRequest<CoinmateOrder>("/orderById", payload, "POST");
			}

			if (o == null)
				return null;

			return new ExchangeOrderResult
			{
				Amount = o.OriginalAmount,
				AmountFilled = o.OriginalAmount - o.RemainingAmount,
				AveragePrice = o.AvgPrice,
				ClientOrderId = isClientOrderId ? orderId : null,
				OrderId = o.Id.ToString(),
				Price = o.Price,
				IsBuy = o.Type == "BUY",
				OrderDate = CryptoUtility.ParseTimestamp(
							o.Timestamp,
							TimestampType.UnixMilliseconds
					),
				ResultCode = o.Status,
				Result = o.Status switch
				{
					"CANCELLED" => ExchangeAPIOrderResult.Canceled,
					"FILLED" => ExchangeAPIOrderResult.Filled,
					"PARTIALLY_FILLED" => ExchangeAPIOrderResult.FilledPartially,
					"OPEN" => ExchangeAPIOrderResult.Open,
					_ => ExchangeAPIOrderResult.Unknown
				},
				MarketSymbol = marketSymbol
			};
		}

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			var payload = await GetNoncePayloadAsync();
			payload["orderId"] = orderId;

			await MakeCoinmateRequest<bool>("/cancelOrder", payload, "POST");
		}

		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			var payload = await GetNoncePayloadAsync();

			if (order.OrderType != OrderType.Limit && order.OrderType != OrderType.Stop)
			{
				throw new NotSupportedException("This type of order is currently not supported.");
			}

			payload["amount"] = order.Amount;
			payload["price"] = order.Price;
			payload["currencyPair"] = order.MarketSymbol;
			payload["postOnly"] = order.IsPostOnly.GetValueOrDefault() ? 1 : 0;

			if (order.OrderType == OrderType.Stop)
			{
				payload["stopPrice"] = order.StopPrice;
			}

			if (order.ClientOrderId != null)
			{
				if (!long.TryParse(order.ClientOrderId, out var clientOrderId))
				{
					throw new ArgumentException("ClientId must be numerical for Coinmate");
				}

				payload["clientOrderId"] = clientOrderId;
			}

			var url = order.IsBuy ? "/buyLimit" : "/sellLimit";
			var id = await MakeCoinmateRequest<long?>(url, payload, "POST");

			try
			{
				return await GetOrderDetailsAsync(id?.ToString(), marketSymbol: order.MarketSymbol);
			}
			catch
			{
				return new ExchangeOrderResult { OrderId = id?.ToString() };
			}
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			var payload = await GetNoncePayloadAsync();
			payload["currencyPair"] = marketSymbol;

			var orders = await MakeCoinmateRequest<CoinmateOpenOrder[]>(
					"/openOrders",
					payload,
					"POST"
			);

			return orders
					.Select(
							x =>
									new ExchangeOrderResult
									{
										Amount = x.Amount,
										ClientOrderId = x.ClientOrderId?.ToString(),
										IsBuy = x.Type == "BUY",
										MarketSymbol = x.CurrencyPair,
										OrderDate = CryptoUtility.ParseTimestamp(
													x.Timestamp,
													TimestampType.UnixMilliseconds
											),
										OrderId = x.Id.ToString(),
										Price = x.Price,
									}
					)
					.ToArray();
		}

		protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(
				string currency,
				bool forceRegenerate = false
		)
		{
			var payload = await GetNoncePayloadAsync();
			var currencyName = GetCurrencyName(currency);
			var addresses = await MakeCoinmateRequest<string[]>(
					$"/{currencyName}DepositAddresses",
					payload,
					"POST"
			);

			return new ExchangeDepositDetails
			{
				Address = addresses.FirstOrDefault(),
				Currency = currency,
			};
		}

		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(
				ExchangeWithdrawalRequest withdrawalRequest
		)
		{
			var payload = await GetNoncePayloadAsync();
			var currencyName = GetCurrencyName(withdrawalRequest.Currency);

			payload["amount"] = withdrawalRequest.Amount;
			payload["address"] = withdrawalRequest.Address;
			payload["amountType"] = withdrawalRequest.TakeFeeFromAmount ? "NET" : "GROSS";

			var id = await MakeCoinmateRequest<long?>(
					$"/{currencyName}Withdrawal",
					payload,
					"POST"
			);

			return new ExchangeWithdrawalResponse { Id = id?.ToString(), Success = id != null };
		}

		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				if (string.IsNullOrWhiteSpace(ClientId))
				{
					throw new APIException("Client ID is not set for Coinmate");
				}

				var apiKey = PublicApiKey.ToUnsecureString();
				var messageToSign = payload["nonce"].ToStringInvariant() + ClientId + apiKey;
				var signature = CryptoUtility
						.SHA256Sign(messageToSign, PrivateApiKey.ToUnsecureString())
						.ToUpperInvariant();
				payload["signature"] = signature;
				payload["clientId"] = ClientId;
				payload["publicKey"] = apiKey;
				await CryptoUtility.WritePayloadFormToRequestAsync(request, payload);
			}
		}

		private async Task<T> MakeCoinmateRequest<T>(
				string url,
				Dictionary<string, object> payload = null,
				string method = null
		)
		{
			var response = await MakeJsonRequestAsync<CoinmateResponse<T>>(
					url,
					null,
					payload,
					method
			);

			if (response.Error)
			{
				throw new APIException(response.ErrorMessage);
			}

			return response.Data;
		}

		private string GetCurrencyName(string currency)
		{
			return currency.ToUpper() switch
			{
				"BTC" => "bitcoin",
				"LTC" => "litecoin",
				"BCH" => "bitcoinCash",
				"ETH" => "ethereum",
				"XRP" => "ripple",
				"DASH" => "dash",
				"DAI" => "dai",
				_ => throw new NotImplementedException("Unsupported currency")
			};
		}

		public partial class ExchangeName
		{
			public const string Coinmate = "Coinmate";
		}
	}
}
