/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;
	using System.Web;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	public sealed partial class ExchangeBittrexAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.bittrex.com/v3";

		private ExchangeBittrexAPI()
		{
			RateLimit = new RateGate(60, TimeSpan.FromSeconds(60));
			RequestContentType = "application/json";
			MarketSymbolIsReversed = false;
			WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;
		}

		#region Utilities
		public override string PeriodSecondsToString(int seconds)
		{
			string periodString;
			switch (seconds)
			{
				case 60:
					periodString = "MINUTE_1";
					break;
				case 300:
					periodString = "MINUTE_5";
					break;
				case 3600:
					periodString = "HOUR_1";
					break;
				case 86400:
					periodString = "DAY_1";
					break;
				default:
					throw new ArgumentException(
							$"{nameof(seconds)} must be one of 60 (min), 300 (fiveMin) 3600 (hour), 86400 (day)"
					);
			}
			return periodString;
		}

		private ExchangeOrderResult ParseOrder(JToken token)
		{
			/*
			{
					"id": "string (uuid)",
					"marketSymbol": "string",
					"direction": "string",
					"type": "string",
					"quantity": "number (double)",
					"limit": "number (double)",
					"ceiling": "number (double)",
					"timeInForce": "string",
					"clientOrderId": "string (uuid)",
					"fillQuantity": "number (double)",
					"commission": "number (double)",
					"proceeds": "number (double)",
					"status": "string",
					"createdAt": "string (date-time)",
					"updatedAt": "string (date-time)",
					"closedAt": "string (date-time)",
					"orderToCancel": {
					"type": "string",
					"id": "string (uuid)"
															}
			}
			 */
			ExchangeOrderResult order = new ExchangeOrderResult();
			decimal amount = token["quantity"].ConvertInvariant<decimal>();
			decimal amountFilled = token["fillQuantity"].ConvertInvariant<decimal>();
			order.Amount = amount;
			order.AmountFilled = amountFilled;
			order.Price = token["limit"].ConvertInvariant<decimal>(order.AveragePrice.Value);
			order.Message = string.Empty;
			order.OrderId = token["id"].ToStringInvariant();

			if (amountFilled >= amount)
			{
				order.Result = ExchangeAPIOrderResult.Filled;
			}
			else if (amountFilled == 0m)
			{
				order.Result = ExchangeAPIOrderResult.Open;
			}
			else
			{
				order.Result = ExchangeAPIOrderResult.FilledPartially;
			}
			order.OrderDate = token["createdAt"].ToDateTimeInvariant();
			order.MarketSymbol = token["marketSymbol"].ToStringInvariant();
			order.Fees = token["commission"].ConvertInvariant<decimal>(); // This is always in the base pair (e.g. BTC, ETH, USDT)

			if (!string.IsNullOrWhiteSpace(order.MarketSymbol))
			{
				string[] pairs = order.MarketSymbol.Split('-');
				if (pairs.Length == 2)
				{
					order.FeesCurrency = pairs[0];
				}
			}
			order.IsBuy = token["direction"].ToStringInvariant() == "BUY";
			return order;
		}

		private string ByteToString(byte[] buff)
		{
			var result = "";
			foreach (var t in buff)
				result += t.ToString("X2"); /* hex format */
			return result;
		}
		#endregion

		#region RequestHelper
		protected override async Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object> payload
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				var timeStamp = Math.Round(DateTime.UtcNow.UnixTimestampFromDateTimeMilliseconds());
				var message = string.Empty;
				payload.Remove("nonce");
				if (request.Method != "GET" && request.Method != "DELETE")
				{
					if (payload.Count > 0)
						message = JsonConvert.SerializeObject(payload);
				}
				byte[] sourceBytes = Encoding.UTF8.GetBytes(message);
				byte[] hashBytes = SHA512.Create().ComputeHash(sourceBytes);
				string hash = ByteToString(hashBytes).Replace("-", string.Empty);
				string url = request.RequestUri.ToStringInvariant();
				string sign = timeStamp + url + request.Method + hash;

				request.AddHeader("Api-Key", PublicApiKey.ToUnsecureString());
				request.AddHeader("Api-Timestamp", timeStamp.ToStringInvariant());
				request.AddHeader("Api-Content-Hash", hash);
				request.AddHeader(
						"Api-Signature",
						CryptoUtility.SHA512Sign(sign, PrivateApiKey.ToUnsecureString())
				);
				if (request.Method == "POST")
					await CryptoUtility.WriteToRequestAsync(
							request,
							JsonConvert.SerializeObject(payload)
					);
			}
			//Console.WriteLine(request.RequestUri);
			//return base.ProcessRequestAsync(request, payload);
		}
		#endregion

		#region CurrencyData
		protected override async Task<
				IReadOnlyDictionary<string, ExchangeCurrency>
		> OnGetCurrenciesAsync()
		{
			var currencies = new Dictionary<string, ExchangeCurrency>(
					StringComparer.OrdinalIgnoreCase
			);
			JToken array = await MakeJsonRequestAsync<JToken>("/currencies");
			foreach (JToken token in array)
			{
				bool enabled = token["status"].ToStringLowerInvariant() == "online" ? true : false;
				var coin = new ExchangeCurrency
				{
					CoinType = token["coinType"].ToStringInvariant(),
					FullName = token["name"].ToStringInvariant(),
					DepositEnabled = enabled,
					WithdrawalEnabled = enabled,
					MinConfirmations = token["minConfirmations"].ConvertInvariant<int>(),
					Name = token["symbol"].ToStringUpperInvariant(),
					Notes = token["notice"].ToStringInvariant(),
					TxFee = token["txFee"].ConvertInvariant<decimal>(),
				};

				currencies[coin.Name] = coin;
			}

			return currencies;
		}
		#endregion

		#region MarketMeta
		/// <summary>
		/// Get exchange symbols including available metadata such as min trade size and whether the market is active
		/// </summary>
		/// <returns>Collection of ExchangeMarkets</returns>
		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			var markets = new List<ExchangeMarket>();
			JToken array = await MakeJsonRequestAsync<JToken>("/markets");

			// StepSize is 8 decimal places for both price and amount on everything at Bittrex
			const decimal StepSize = 0.00000001m;
			foreach (JToken token in array)
			{
				var market = new ExchangeMarket
				{
					//NOTE: Bittrex is weird in that they call the QuoteCurrency the "BaseCurrency" and the BaseCurrency the "MarketCurrency".
					QuoteCurrency = token["quoteCurrencySymbol"].ToStringUpperInvariant(),
					IsActive = token["status"].ToStringLowerInvariant() == "online" ? true : false,
					BaseCurrency = token["baseCurrencySymbol"].ToStringUpperInvariant(),
					//NOTE: They also reverse the order of the currencies in the MarketName
					MarketSymbol = token["symbol"].ToStringUpperInvariant(),
					MinTradeSize = token["minTradeSize"].ConvertInvariant<decimal>(),
					MinPrice = StepSize,
					PriceStepSize = StepSize,
					QuantityStepSize = StepSize
				};

				markets.Add(market);
			}

			return markets;
		}
		#endregion

		#region MarketSymbols
		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			return (await GetMarketSymbolsMetadataAsync()).Select(x => x.MarketSymbol);
		}
		#endregion

		#region Tickers
		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			JToken ticker = await MakeJsonRequestAsync<JToken>(
					"/markets/" + marketSymbol + "/ticker"
			);
			//NOTE: Bittrex uses the term "BaseVolume" when referring to the QuoteCurrencyVolume
			return await this.ParseTickerAsync(
					ticker,
					marketSymbol,
					"askRate",
					"bidRate",
					"lastTradeRate",
					"volume",
					"quoteVolume",
					"updatedAt",
					TimestampType.Iso8601UTC
			);
		}

		protected override async Task<
				IEnumerable<KeyValuePair<string, ExchangeTicker>>
		> OnGetTickersAsync()
		{
			JToken tickers = await MakeJsonRequestAsync<JToken>("/markets/tickers");
			string marketSymbol;
			List<KeyValuePair<string, ExchangeTicker>> tickerList =
					new List<KeyValuePair<string, ExchangeTicker>>();
			foreach (JToken ticker in tickers)
			{
				marketSymbol = ticker["symbol"].ToStringInvariant();
				ExchangeTicker tickerObj = await this.ParseTickerAsync(
						ticker,
						marketSymbol,
						"askRate",
						"bidRate",
						"lastTradeRate",
						"volume",
						"quoteVolume",
						"updatedAt",
						TimestampType.Iso8601UTC
				);
				tickerList.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, tickerObj));
			}
			return tickerList;
		}

		#endregion

		#region OrderBooks
		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 25
		)
		{
			// Bittrex API allowed values are [1, 25, 500], default is 25.
			if (maxCount > 100)
			{
				maxCount = 500;
			}
			else if (maxCount > 25 && maxCount <= 100) // ExchangeSharp default.
			{
				maxCount = 25;
			}
			else if (maxCount > 1 && maxCount <= 25)
			{
				maxCount = 25;
			}
			else
			{
				maxCount = 1;
			}

			JToken token = await MakeJsonRequestAsync<JToken>(
					"/markets/" + marketSymbol + "/orderbook" + "?depth=" + maxCount
			);
			return token.ParseOrderBookFromJTokenDictionaries("ask", "bid", "rate", "quantity");
		}

		/// <summary>Gets the deposit history for a symbol</summary>
		/// <param name="currency">The symbol to check. May be null.</param>
		/// <returns>Collection of ExchangeTransactions</returns>
		protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(
				string currencyNotNeeded = null
		)
		{
			var transactions = new List<ExchangeTransaction>();
			string url = "/deposits/closed";
			JToken result = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);
			foreach (JToken token in result)
			{
				var deposit = new ExchangeTransaction
				{
					Amount = token["quantity"].ConvertInvariant<decimal>(),
					Address = token["cryptoAddress"].ToStringInvariant(),
					Currency = token["currencySymbol"].ToStringInvariant(),
					PaymentId = token["id"].ToStringInvariant(),
					BlockchainTxId = token["txId"].ToStringInvariant(),
					Status = TransactionStatus.Complete, // As soon as it shows up in this list it is complete (verified manually)
				};

				DateTime.TryParse(token["updatedAt"].ToStringInvariant(), out DateTime timestamp);
				deposit.Timestamp = timestamp;

				transactions.Add(deposit);
			}

			return transactions;
		}
		#endregion

		#region Trades
		protected override async Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			throw new APIException(
					"Bittrex does not allow querying trades by dates. Consider using either GetRecentTradesAsync() or GetCandlesAsync() w/ a period of 1 min. See issue #508."
			);
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(
				string marketSymbol,
				int? limit = null
		)
		{
			List<ExchangeTrade> trades = new List<ExchangeTrade>();
			string baseUrl = "/markets/" + marketSymbol + "/trades";
			JToken array = await MakeJsonRequestAsync<JToken>(baseUrl);
			foreach (JToken token in array)
			{
				trades.Add(
						token.ParseTrade(
								"quantity",
								"rate",
								"takerSide",
								"executedAt",
								TimestampType.Iso8601UTC,
								"id"
						)
				);
			}
			return trades;
		}
		#endregion

		#region AmountMethods
		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);
			string url = "/balances";
			JToken array = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);
			foreach (JToken token in array)
			{
				decimal amount = token["total"].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					currencies.Add(token["currencySymbol"].ToStringInvariant(), amount);
				}
			}
			return currencies;
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);
			string url = "/balances";
			JToken array = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);
			foreach (JToken token in array)
			{
				decimal amount = token["available"].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					currencies.Add(token["currencySymbol"].ToStringInvariant(), amount);
				}
			}
			return currencies;
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances)
		{
			Dictionary<string, decimal> marginAmounts = new Dictionary<string, decimal>();

			string url = "/balances";
			JToken response = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);

			var result = response
					.Where(i => includeZeroBalances || i["available"].ConvertInvariant<decimal>() != 0)
					.ToDictionary(
							i => i["currencySymbol"].ToStringInvariant(),
							i => i["available"].ConvertInvariant<decimal>()
					);

			return result;
		}
		#endregion

		#region OrderMethods
		protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			decimal orderAmount = await ClampOrderQuantity(order.MarketSymbol, order.Amount);
			if (order.Price == null)
				throw new ArgumentNullException(nameof(order.Price));
			decimal orderPrice = await ClampOrderPrice(order.MarketSymbol, order.Price.Value);
			string url = "/orders";
			Dictionary<string, object> orderParams = await GetNoncePayloadAsync();
			orderParams.Add("marketSymbol", order.MarketSymbol);
			orderParams.Add("direction", order.IsBuy ? "BUY" : "SELL");
			orderParams.Add(
					"type",
					order.OrderType == ExchangeSharp.OrderType.Market ? "MARKET" : "LIMIT"
			);
			orderParams.Add("quantity", orderAmount);
			if (order.OrderType == ExchangeSharp.OrderType.Limit)
			{
				orderParams.Add("limit", orderPrice);
				if (order.IsPostOnly == true)
					orderParams.Add("timeInForce", "POST_ONLY_GOOD_TIL_CANCELLED"); // This option allows market makers to ensure that their orders are making it to the order book instead of matching with a pre-existing order. Note: If the order is not a maker order, you will return an error and the order will be cancelled
				else
					orderParams.Add("timeInForce", "GOOD_TIL_CANCELLED");
			}

			foreach (KeyValuePair<string, object> kv in order.ExtraParameters)
			{
				orderParams.Add(kv.Key, kv.Value);
			}

			JToken result = await MakeJsonRequestAsync<JToken>(url, null, orderParams, "POST");
			return new ExchangeOrderResult
			{
				Amount = orderAmount,
				AmountFilled = decimal.Parse(result["fillQuantity"].ToStringInvariant()),
				IsBuy = order.IsBuy,
				OrderDate = result["createdAt"].ToDateTimeInvariant(),
				OrderId = result["id"].ToStringInvariant(),
				Result = ExchangeAPIOrderResult.Open,
				MarketSymbol = result["marketSymbol"].ToStringInvariant(),
				Price = decimal.Parse(result["limit"].ToStringInvariant())
			};
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			if (isClientOrderId)
				throw new NotSupportedException(
						"Querying by client order ID is not implemented in ExchangeSharp. Please submit a PR if you are interested in this feature"
				);
			if (string.IsNullOrWhiteSpace(orderId))
			{
				return null;
			}

			string url = "/orders/" + orderId;
			JToken result = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);
			return ParseOrder(result);
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string marketSymbol = null
		)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			string url =
					"/orders/open"
					+ (
							string.IsNullOrWhiteSpace(marketSymbol)
									? string.Empty
									: "?marketSymbol=" + NormalizeMarketSymbol(marketSymbol)
					);
			JToken result = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);
			foreach (JToken token in result.Children())
			{
				orders.Add(ParseOrder(token));
			}

			return orders;
		}

		protected override async Task<
				IEnumerable<ExchangeOrderResult>
		> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			string url =
					"/orders/closed"
					+ (
							string.IsNullOrWhiteSpace(marketSymbol)
									? string.Empty
									: "?marketSymbol=" + NormalizeMarketSymbol(marketSymbol)
					);
			JToken result = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);
			foreach (JToken token in result.Children())
			{
				ExchangeOrderResult order = ParseOrder(token);
				if (afterDate == null || order.OrderDate >= afterDate.Value)
				{
					orders.Add(order);
				}
			}

			return orders;
		}

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			if (isClientOrderId)
				throw new NotSupportedException(
						"Cancelling by client order ID is not supported in ExchangeSharp. Please submit a PR if you are interested in this feature"
				);
			await MakeJsonRequestAsync<JToken>(
					"/orders/" + orderId,
					null,
					await GetNoncePayloadAsync(),
					"DELETE"
			);
		}

		#endregion

		#region Candles
		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string marketSymbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			if (limit != null)
			{
				throw new APIException("Limit parameter not supported in Bittrex");
			}

			// https://bittrex.com/Api/v2.0/pub/market/GetTicks?marketName=BTC-WAVES&tickInterval=day
			/* [{    "startsAt": "string (date-time)",    "open": "number (double)",    "high": "number (double)",    "low": "number (double)",    "close": "number (double)",    "volume": "number (double)",    "quoteVolume": "number (double)"}]			 */

			string periodString = PeriodSecondsToString(periodSeconds);
			List<MarketCandle> candles = new List<MarketCandle>();

			JToken result;

			result = await MakeJsonRequestAsync<JToken>(
					"/markets/" + marketSymbol + "/candles/" + periodString + "/recent"
			);

			if (result is JArray array)
			{
				foreach (JToken jsonCandle in array)
				{
					//NOTE: Bittrex uses the term "BaseVolume" when referring to the QuoteCurrencyVolume
					MarketCandle candle = this.ParseCandle(
							token: jsonCandle,
							marketSymbol: marketSymbol,
							periodSeconds: periodSeconds,
							openKey: "open",
							highKey: "high",
							lowKey: "low",
							closeKey: "close",
							timestampKey: "startsAt",
							timestampType: TimestampType.Iso8601UTC,
							baseVolumeKey: "volume",
							quoteVolumeKey: "quoteVolume"
					);
					if (startDate != null && endDate != null)
					{
						if (candle.Timestamp >= startDate && candle.Timestamp <= endDate)
							candles.Add(candle);
					}
					else
						candles.Add(candle);
				}
			}

			return candles;
		}
		#endregion

		#region Withdraw Methods
		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(
				ExchangeWithdrawalRequest withdrawalRequest
		)
		{
			/*
					"currencySymbol": "string",
					"quantity": "number (double)",
					"cryptoAddress": "string",
					"cryptoAddressTag": "string",
					"clientWithdrawalId": "string (uuid)"
			*/

			string url = "/withdrawals";
			var payload = await GetNoncePayloadAsync();
			payload.Add("currencySymbol", withdrawalRequest.Currency);
			payload.Add("quantity", withdrawalRequest.Amount);
			payload.Add("cryptoAddress", withdrawalRequest.Address);
			if (withdrawalRequest.AddressTag != null)
				payload.Add("cryptoAddressTag", withdrawalRequest.AddressTag);

			JToken result = await MakeJsonRequestAsync<JToken>(url, null, payload, "POST");

			/*
					{
						"id": "string (uuid)",
						"currencySymbol": "string",
						"quantity": "number (double)",
						"cryptoAddress": "string",
						"cryptoAddressTag": "string",
						"txCost": "number (double)",
						"txId": "string",
						"status": "string",
						"createdAt": "string (date-time)",
						"completedAt": "string (date-time)",
						"clientWithdrawalId": "string (uuid)",
						"accountId": "string (uuid)"
					}
			 */
			ExchangeWithdrawalResponse withdrawalResponse = new ExchangeWithdrawalResponse
			{
				Id = result["id"].ToStringInvariant(),
				Message = result["status"].ToStringInvariant(),
				Fee = result.Value<decimal?>("txCost")
			};

			return withdrawalResponse;
		}

		protected override async Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawHistoryAsync(
				string currency
		)
		{
			string url =
					$"/withdrawals/closed{(string.IsNullOrWhiteSpace(currency) ? string.Empty : $"?currencySymbol={currency}")}";
			JToken result = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);

			var transactions = result.Select(
					t =>
							new ExchangeTransaction
							{
								Amount = t["quantity"].ConvertInvariant<decimal>(),
								Address = t["cryptoAddress"].ToStringInvariant(),
								AddressTag = t["cryptoAddressTag"].ToStringInvariant(),
								TxFee = t["txCost"].ConvertInvariant<decimal>(),
								Currency = t["currencySymbol"].ToStringInvariant(),
								PaymentId = t["id"].ToStringInvariant(),
								BlockchainTxId = t["txId"].ToStringInvariant(),
								Timestamp = DateTime.Parse(t["createdAt"].ToStringInvariant()),
								Status = ToStatus(t["status"].ToStringInvariant())
							}
			);

			return transactions;
		}

		private TransactionStatus ToStatus(string status)
		{
			/* REQUESTED, AUTHORIZED, PENDING, COMPLETED, ERROR_INVALID_ADDRESS, CANCELLED */
			if (status == "CANCELLED")
				return TransactionStatus.Rejected;

			if (status == "ERROR_INVALID_ADDRESS")
				return TransactionStatus.Failure;

			if (status == "PENDING")
				return TransactionStatus.AwaitingApproval;

			if (status == "COMPLETED")
				return TransactionStatus.Complete;

			if (status == "REQUESTED")
				return TransactionStatus.Processing;

			return TransactionStatus.Unknown;
		}

		#endregion

		#region DepositAddress
		/// <summary>
		/// Gets the address to deposit to and applicable details.
		/// If one does not exist, the call will fail and return ADDRESS_GENERATING until one is available.
		/// </summary>
		/// <param name="currency">Currency to get address for.</param>
		/// <param name="forceRegenerate">(ignored) Bittrex does not support regenerating deposit addresses.</param>
		/// <returns>
		/// Deposit address details (including tag if applicable, such as with XRP)
		/// </returns>
		protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(
				string currency,
				bool forceRegenerate = false
		)
		{
			if (forceRegenerate)
			{
				throw new Exception("forceRegenerate does not support.");
			}

			string url = "/addresses/" + NormalizeMarketSymbol(currency);
			JToken result = await MakeJsonRequestAsync<JToken>(
					url,
					null,
					await GetNoncePayloadAsync()
			);

			/*
			 {
					"status": "string",
					"currencySymbol": "string",
					"cryptoAddress": "string",
					"cryptoAddressTag": "string"
			 }
			 */
			ExchangeDepositDetails depositDetails = new ExchangeDepositDetails
			{
				Currency = result["currencySymbol"].ToStringInvariant(),
				Address = result["cryptoAddress"].ToStringInvariant(),
				AddressTag = result["cryptoAddressTag"].ToStringInvariant(),
			};
			return depositDetails;
		}
		#endregion
	}

	public partial class ExchangeName
	{
		public const string Bittrex = "Bittrex";
	}
}
