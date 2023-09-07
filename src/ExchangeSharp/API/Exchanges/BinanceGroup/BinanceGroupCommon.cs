/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp.BinanceGroup
{
	public abstract class BinanceGroupCommon : ExchangeAPI
	{
		public virtual string BaseUrlApi => $"{BaseUrl}/api/v3";

		public string BaseUrlSApi => $"{BaseUrl}/sapi/v1";

		protected async Task<string> GetWebSocketStreamUrlForSymbolsAsync(
				string suffix,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			if (marketSymbols.Length > 400)
			{
				marketSymbols = marketSymbols.Take(400).ToArray();
				Logger.Warn("subscribing to the first 400 symbols"); // binance does not allow subscribing to more than 400 symbols at a time
			}

			StringBuilder streams = new StringBuilder("/stream?streams=");
			for (int i = 0; i < marketSymbols.Length; i++)
			{
				string marketSymbol = NormalizeMarketSymbol(marketSymbols[i]).ToLowerInvariant();
				streams.Append(marketSymbol);
				streams.Append(suffix);
				streams.Append('/');
			}
			streams.Length--; // remove last /

			return streams.ToString();
		}

		protected BinanceGroupCommon()
		{
			// give binance plenty of room to accept requests
			RequestWindow = TimeSpan.FromMilliseconds(60000); // 60000 is max value = max request time window of 60 seconds
			NonceStyle = NonceStyle.UnixMilliseconds;
			NonceOffset = TimeSpan.FromSeconds(15); // 15 seconds are deducted from current UTCTime as base of the request time window
			MarketSymbolSeparator = string.Empty;
			MarketSymbolIsUppercase = true;
			WebSocketOrderBookType = WebSocketOrderBookType.DeltasOnly;
			ExchangeGlobalCurrencyReplacements["BCC"] = "BCH";
			/* Binance rate limits are a combination of 3 things:
			 * - 1200 request weights per 1 minute
			 * - 100 orders per 10 seconds
			 * - 200,000 orders per 1 day
			 * - 5,000 raw requests per 5 min
			 * Since the most restrictive is the 100 orders per 10 seconds, and OCO orders count for 2, we can conservatively do a little less than 50 per 10 seconds
			 */
			RateLimit = new RateGate(40, TimeSpan.FromSeconds(10));
		}

		public override Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(
				string marketSymbol
		)
		{
			// All pairs in Binance end with BTC, ETH, BNB or USDT
			if (
					marketSymbol.EndsWith("BTC")
					|| marketSymbol.EndsWith("ETH")
					|| marketSymbol.EndsWith("BNB")
			)
			{
				string baseSymbol = marketSymbol.Substring(marketSymbol.Length - 3);
				return ExchangeMarketSymbolToGlobalMarketSymbolWithSeparatorAsync(
						(
								marketSymbol.Replace(baseSymbol, "")
								+ GlobalMarketSymbolSeparator
								+ baseSymbol
						),
						GlobalMarketSymbolSeparator
				);
			}
			if (marketSymbol.EndsWith("USDT"))
			{
				string baseSymbol = marketSymbol.Substring(marketSymbol.Length - 4);
				return ExchangeMarketSymbolToGlobalMarketSymbolWithSeparatorAsync(
						(
								marketSymbol.Replace(baseSymbol, "")
								+ GlobalMarketSymbolSeparator
								+ baseSymbol
						),
						GlobalMarketSymbolSeparator
				);
			}

			return ExchangeMarketSymbolToGlobalMarketSymbolWithSeparatorAsync(
					marketSymbol.Substring(0, marketSymbol.Length - 3)
							+ GlobalMarketSymbolSeparator
							+ (marketSymbol.Substring(marketSymbol.Length - 3, 3)),
					GlobalMarketSymbolSeparator
			);
		}

		/// <summary>
		/// Get the details of all trades
		/// </summary>
		/// <param name="marketSymbol">Symbol to get trades for or null for all</param>
		/// <returns>All trades for the specified symbol, or all if null symbol</returns>
		public async Task<IEnumerable<ExchangeOrderResult>> GetMyTradesAsync(
				string? marketSymbol = null,
				DateTime? afterDate = null
		)
		{
			await new SynchronizationContextRemover();
			return await OnGetMyTradesAsync(marketSymbol, afterDate);
		}

		protected override async Task<
				IReadOnlyDictionary<string, ExchangeCurrency>
		> OnGetCurrenciesAsync()
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			var result = await MakeJsonRequestAsync<List<Currency>>(
					"/capital/config/getall",
					BaseUrlSApi,
					payload
			);

			return result
					.Where(x => !string.IsNullOrWhiteSpace(x.AssetCode))
					.ToDictionary(
							x => x.AssetCode,
							x =>
									new ExchangeCurrency
									{
										Name = x.AssetCode,
										FullName = x.AssetName,
										DepositEnabled = x.EnableCharge ?? false,
										WithdrawalEnabled = x.EnableWithdraw.GetValueOrDefault(false),
										MinConfirmations = x.ConfirmTimes.GetValueOrDefault(0),
										MinWithdrawalSize = x.MinProductWithdraw.GetValueOrDefault(
													decimal.Zero
											),
										TxFee = x.FeeRate.GetValueOrDefault(decimal.Zero),
										CoinType = x.ParentCode
									}
					);
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{
			List<string> symbols = new List<string>();
			JToken? obj = await MakeJsonRequestAsync<JToken>("/ticker/price", BaseUrlApi);
			if (!(obj is null))
			{
				foreach (JToken token in obj)
				{
					symbols.Add(token["symbol"].ToStringInvariant());
				}
			}
			return symbols;
		}

		protected internal override async Task<
				IEnumerable<ExchangeMarket>
		> OnGetMarketSymbolsMetadataAsync()
		{
			/*
			 *         {
					"symbol": "ETHBTC",
					"status": "TRADING",
					"baseAsset": "ETH",
					"baseAssetPrecision": 8,
					"quoteAsset": "BTC",
					"quotePrecision": 8,
					"orderTypes": [
							"LIMIT",
							"MARKET",
							"STOP_LOSS",
							"STOP_LOSS_LIMIT",
							"TAKE_PROFIT",
							"TAKE_PROFIT_LIMIT",
							"LIMIT_MAKER"
					],
					"icebergAllowed": false,
					"filters": [
							{
									"filterType": "PRICE_FILTER",
									"minPrice": "0.00000100",
									"maxPrice": "100000.00000000",
									"tickSize": "0.00000100"
							},
							{
									"filterType": "LOT_SIZE",
									"minQty": "0.00100000",
									"maxQty": "100000.00000000",
									"stepSize": "0.00100000"
							},
							{
									"filterType": "MIN_NOTIONAL",
									"minNotional": "0.00100000"
							}
					]
	},
			 */

			var markets = new List<ExchangeMarket>();
			JToken obj = await MakeJsonRequestAsync<JToken>("/exchangeInfo", BaseUrlApi);
			JToken allSymbols = obj["symbols"];
			foreach (JToken marketSymbolToken in allSymbols)
			{
				var market = new ExchangeMarketBinance
				{
					// common ExchangeMarket properties
					MarketSymbol = marketSymbolToken["symbol"].ToStringUpperInvariant(),
					IsActive = ParseMarketStatus(
								marketSymbolToken["status"].ToStringUpperInvariant()
						),
					QuoteCurrency = marketSymbolToken["quoteAsset"].ToStringUpperInvariant(),
					BaseCurrency = marketSymbolToken["baseAsset"].ToStringUpperInvariant(),
					// Binance specific properties
					Status = (BinanceSymbolStatus)
								Enum.Parse(
										typeof(BinanceSymbolStatus),
										marketSymbolToken["status"].ToStringInvariant(),
										true
								),
					BaseAssetPrecision = marketSymbolToken[
								"baseAssetPrecision"
						].ConvertInvariant<int>(),
					QuotePrecision = marketSymbolToken["quotePrecision"].ConvertInvariant<int>(),
					IsIceBergAllowed = marketSymbolToken["icebergAllowed"].ConvertInvariant<bool>(),
				};

				// "LOT_SIZE"
				JToken filters = marketSymbolToken["filters"];
				JToken? lotSizeFilter = filters?.FirstOrDefault(
						x => string.Equals(x["filterType"].ToStringUpperInvariant(), "LOT_SIZE")
				);
				if (lotSizeFilter != null)
				{
					market.MaxTradeSize = lotSizeFilter["maxQty"].ConvertInvariant<decimal>();
					market.MinTradeSize = lotSizeFilter["minQty"].ConvertInvariant<decimal>();
					market.QuantityStepSize = lotSizeFilter["stepSize"].ConvertInvariant<decimal>();
				}

				// PRICE_FILTER
				JToken? priceFilter = filters?.FirstOrDefault(
						x => string.Equals(x["filterType"].ToStringUpperInvariant(), "PRICE_FILTER")
				);
				if (priceFilter != null)
				{
					market.MaxPrice = priceFilter["maxPrice"].ConvertInvariant<decimal>();
					market.MinPrice = priceFilter["minPrice"].ConvertInvariant<decimal>();
					market.PriceStepSize = priceFilter["tickSize"].ConvertInvariant<decimal>();
				}

				// MIN_NOTIONAL
				JToken? minNotionalFilter = filters?.FirstOrDefault(
						x => string.Equals(x["filterType"].ToStringUpperInvariant(), "MIN_NOTIONAL")
				);
				if (minNotionalFilter != null)
				{
					market.MinTradeSizeInQuoteCurrency = minNotionalFilter[
							"minNotional"
					].ConvertInvariant<decimal>();
				}

				// MAX_NUM_ORDERS
				JToken? maxOrdersFilter = filters?.FirstOrDefault(
						x => string.Equals(x["filterType"].ToStringUpperInvariant(), "MAX_NUM_ORDERS")
				);
				if (maxOrdersFilter != null)
				{
					market.MaxNumOrders = maxOrdersFilter[
							"maxNumberOrders"
					].ConvertInvariant<int>();
				}
				markets.Add(market);
			}

			return markets;
		}

		protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
		{
			JToken obj = await MakeJsonRequestAsync<JToken>(
					"/ticker/24hr?symbol=" + marketSymbol,
					BaseUrlApi
			);
			return await ParseTickerAsync(marketSymbol, obj);
		}

		protected override async Task<
				IEnumerable<KeyValuePair<string, ExchangeTicker>>
		> OnGetTickersAsync()
		{
			List<KeyValuePair<string, ExchangeTicker>> tickers =
					new List<KeyValuePair<string, ExchangeTicker>>();
			JToken obj = await MakeJsonRequestAsync<JToken>("/ticker/24hr", BaseUrlApi);
			foreach (JToken child in obj)
			{
				var marketSymbol = child["symbol"].ToStringInvariant();
				try
				{
					tickers.Add(
							new KeyValuePair<string, ExchangeTicker>(
									marketSymbol,
									await ParseTickerAsync(marketSymbol, child)
							)
					);
				}
				catch
				{
					// ignored
				}
			}
			return tickers;
		}

		protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(
				Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback,
				params string[] symbols
		)
		{
			string url = null;
			if (symbols == null || symbols.Length == 0)
			{
				url = "/stream?streams=!ticker@arr";
			}
			else
				url = await GetWebSocketStreamUrlForSymbolsAsync("@ticker", symbols);
			return await ConnectPublicWebSocketAsync(
					url,
					async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						List<KeyValuePair<string, ExchangeTicker>> tickerList =
											new List<KeyValuePair<string, ExchangeTicker>>();
						ExchangeTicker ticker;

						// if it is the tickers stream, data will be an array, else it is an direct item.
						var data = token["data"];
						if (data is JArray)
							foreach (JToken childToken in data)
							{
								ticker = await ParseTickerWebSocketAsync(childToken);
								tickerList.Add(
													new KeyValuePair<string, ExchangeTicker>(
															ticker.MarketSymbol,
															ticker
													)
											);
							}
						else
						{
							ticker = await ParseTickerWebSocketAsync(data);
							tickerList.Add(
												new KeyValuePair<string, ExchangeTicker>(ticker.MarketSymbol, ticker)
										);
						}

						if (tickerList.Count != 0)
						{
							callback(tickerList);
						}
					}
			);
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		)
		{
			/*
	{
		"e": "aggTrade",  // Event type
		"E": 123456789,   // Event time
		"s": "BNBBTC",    // Symbol
		"a": 12345,       // Aggregate trade ID
		"p": "0.001",     // Price
		"q": "100",       // Quantity
		"f": 100,         // First trade ID
		"l": 105,         // Last trade ID
		"T": 123456785,   // Trade time
		"m": true,        // Is the buyer the market maker?
		"M": true         // Ignore
	}
			*/

			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			string url = await GetWebSocketStreamUrlForSymbolsAsync("@aggTrade", marketSymbols);
			return await ConnectPublicWebSocketAsync(
					url,
					messageCallback: async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						string name = token["stream"].ToStringInvariant();
						token = token["data"];
						string marketSymbol = NormalizeMarketSymbol(
											name.Substring(0, name.IndexOf('@'))
									);

						// buy=0 -> m = true (The buyer is maker, while the seller is taker).
						// buy=1 -> m = false(The seller is maker, while the buyer is taker).
						await callback(
											new KeyValuePair<string, ExchangeTrade>(
													marketSymbol,
													token.ParseTradeBinance(
															amountKey: "q",
															priceKey: "p",
															typeKey: "m",
															timestampKey: "T", // use trade time (T) instead of event time (E)
															timestampType: TimestampType.UnixMilliseconds,
															idKey: "a",
															typeKeyIsBuyValue: "false"
													)
											)
									);
					}
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
			string combined = string.Join(
					"/",
					marketSymbols.Select(
							s => this.NormalizeMarketSymbol(s).ToLowerInvariant() + "@depth@100ms"
					)
			);
			return await ConnectPublicWebSocketAsync(
					$"/stream?streams={combined}",
					(_socket, msg) =>
					{
						string json = msg.ToStringFromUTF8();
						var update = JsonConvert.DeserializeObject<MultiDepthStream>(
											json,
											SerializerSettings
									);
						string marketSymbol = update.Data.MarketSymbol;
						ExchangeOrderBook book = new ExchangeOrderBook
						{
							SequenceId = update.Data.FinalUpdate,
							MarketSymbol = marketSymbol,
							LastUpdatedUtc = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
													update.Data.EventTime
											)
						};
						foreach (List<object> ask in update.Data.Asks)
						{
							var depth = new ExchangeOrderPrice
							{
								Price = ask[0].ConvertInvariant<decimal>(),
								Amount = ask[1].ConvertInvariant<decimal>()
							};
							book.Asks[depth.Price] = depth;
						}
						foreach (List<object> bid in update.Data.Bids)
						{
							var depth = new ExchangeOrderPrice
							{
								Price = bid[0].ConvertInvariant<decimal>(),
								Amount = bid[1].ConvertInvariant<decimal>()
							};
							book.Bids[depth.Price] = depth;
						}
						callback(book);
						return Task.CompletedTask;
					}
			);
		}

		protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(
				string marketSymbol,
				int maxCount = 100
		)
		{
			JToken obj = await MakeJsonRequestAsync<JToken>(
					$"/depth?symbol={marketSymbol}&limit={maxCount}",
					BaseUrlApi
			);
			return obj.ParseOrderBookFromJTokenArrays(sequence: "lastUpdateId");
		}

		protected override async Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			/* [ {
			"a": 26129,         // Aggregate tradeId
			"p": "0.01633102",  // Price
			"q": "4.70443515",  // Quantity
			"f": 27781,         // First tradeId
			"l": 27781,         // Last tradeId
			"T": 1498793709153, // Timestamp
			"m": true,          // Was the buyer the maker?
			"M": true           // Was the trade the best price match?
			} ] */

			//if(startDate == null && endDate == null) {
			//	await OnGetRecentTradesAsync(marketSymbol, limit);
			//}
			//else {
			//System.Windows.Forms.MessageBox.Show("Duplicate MessageId " + MessageId, "HMMMM RETURN?");


			ExchangeHistoricalTradeHelper state = new ExchangeHistoricalTradeHelper(this)
			{
				Callback = callback,
				EndDate = endDate,
				ParseFunction = (JToken token) =>
						token.ParseTrade(
								"q",
								"p",
								"m",
								"T",
								TimestampType.UnixMilliseconds,
								"a",
								"false"
						),
				StartDate = startDate,
				MarketSymbol = marketSymbol,
				TimestampFunction = (DateTime dt) =>
						(
								(long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(dt)
						).ToStringInvariant(),
				Url = "/aggTrades?symbol=[marketSymbol]&startTime={0}&endTime={1}",
			};
			await state.ProcessHistoricalTrades();
			//}
		}

		protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(
				string marketSymbol,
				int? limit = null
		)
		{
			//https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md
			var trades = new List<ExchangeTrade>();
			var maxRequestLimit = (limit == null || limit < 1 || limit > 1000) ? 1000 : (int)limit;

			JToken obj = await MakeJsonRequestAsync<JToken>(
					$"/aggTrades?symbol={marketSymbol}&limit={maxRequestLimit}",
					BaseUrlApi
			);
			if (obj.HasValues)
			{
				trades.AddRange(
						obj.Select(
								token =>
										token.ParseTrade(
												"q",
												"p",
												"m",
												"T",
												TimestampType.UnixMilliseconds,
												"a",
												"false"
										)
						)
				);
			}
			return trades.AsEnumerable().Reverse(); //Descending order (ie newest trades first)
		}

		public async Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				long startId,
				long? endId = null
		)
		{
			/* [ {
			"a": 26129,         // Aggregate tradeId
			"p": "0.01633102",  // Price
			"q": "4.70443515",  // Quantity
			"f": 27781,         // First tradeId
			"l": 27781,         // Last tradeId
			"T": 1498793709153, // Timestamp
			"m": true,          // Was the buyer the maker?
			"M": true           // Was the trade the best price match?
			} ] */

			// TODO : Refactor into a common layer once more Exchanges implement this pattern

			var fromId = startId;
			var maxRequestLimit = 1000;
			var trades = new List<ExchangeTrade>();
			var processedIds = new HashSet<long>();
			marketSymbol = NormalizeMarketSymbol(marketSymbol);

			do
			{
				if (fromId > endId)
					break;

				trades.Clear();
				var limit = Math.Min(endId - fromId ?? maxRequestLimit, maxRequestLimit);
				var obj = await MakeJsonRequestAsync<JToken>(
						$"/aggTrades?symbol={marketSymbol}&fromId={fromId}&limit={limit}"
				);

				foreach (var token in obj)
				{
					var trade = token.ParseTrade(
							"q",
							"p",
							"m",
							"T",
							TimestampType.UnixMilliseconds,
							"a",
							"false"
					);
					long tradeId = (long)trade.Id.ConvertInvariant<ulong>();
					if (tradeId < fromId)
						continue;
					if (tradeId > endId)
						continue;
					if (!processedIds.Add(tradeId))
						continue;

					trades.Add(trade);
					fromId = tradeId;
				}

				fromId++;
			} while (callback(trades) && trades.Count > 0);
		}

		public async Task OnGetHistoricalTradesAsync(
				Func<IEnumerable<ExchangeTrade>, bool> callback,
				string marketSymbol,
				int limit = 100
		)
		{
			/* [ {
			"a": 26129,         // Aggregate tradeId
			"p": "0.01633102",  // Price
			"q": "4.70443515",  // Quantity
			"f": 27781,         // First tradeId
			"l": 27781,         // Last tradeId
			"T": 1498793709153, // Timestamp
			"m": true,          // Was the buyer the maker?
			"M": true           // Was the trade the best price match?
			} ] */

			// TODO : Refactor into a common layer once more Exchanges implement this pattern
			// https://github.com/binance-exchange/binance-official-api-docs/blob/master/rest-api.md#compressedaggregate-trades-list
			if (limit > 1000)
				limit = 1000; //Binance max = 1000
			var maxRequestLimit = 1000;
			var trades = new List<ExchangeTrade>();
			var processedIds = new HashSet<long>();
			marketSymbol = NormalizeMarketSymbol(marketSymbol);

			do
			{
				//if(fromId > endId)
				//	break;

				trades.Clear();
				//var limit = Math.Min(endId - fromId ?? maxRequestLimit, maxRequestLimit);
				var obj = await MakeJsonRequestAsync<JToken>(
						$"/aggTrades?symbol={marketSymbol}&limit={limit}"
				);

				foreach (var token in obj)
				{
					var trade = token.ParseTrade(
							"q",
							"p",
							"m",
							"T",
							TimestampType.UnixMilliseconds,
							"a",
							"false"
					);
					//long tradeId = (long)trade.Id.ConvertInvariant<ulong>();
					//if(tradeId < fromId)
					//	continue;
					//if(tradeId > endId)
					//	continue;
					//if(!processedIds.Add(tradeId))
					//	continue;

					trades.Add(trade);
					//fromId = tradeId;
				}

				//fromId++;
			} while (callback(trades) && trades.Count > 0);
		}

		protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(
				string marketSymbol,
				int periodSeconds,
				DateTime? startDate = null,
				DateTime? endDate = null,
				int? limit = null
		)
		{
			/* [
			[
			1499040000000,      // Open time
			"0.01634790",       // Open
			"0.80000000",       // High
			"0.01575800",       // Low
			"0.01577100",       // Close
			"148976.11427815",  // Volume
			1499644799999,      // Close time
			"2434.19055334",    // Quote asset volume
			308,                // Number of trades
			"1756.87402397",    // Taker buy base asset volume
			"28.46694368",      // Taker buy quote asset volume
			"17928899.62484339" // Can be ignored
			]] */

			string url = "/klines?symbol=" + marketSymbol;
			if (startDate != null)
			{
				url +=
						"&startTime=" + (long)startDate.Value.UnixTimestampFromDateTimeMilliseconds();
				url +=
						"&endTime="
						+ (
								(
										endDate == null
												? long.MaxValue
												: (long)endDate.Value.UnixTimestampFromDateTimeMilliseconds()
								)
						).ToStringInvariant();
			}

			if (limit != null)
			{
				url += "&limit=" + (limit.Value.ToStringInvariant());
			}

			url += "&interval=" + PeriodSecondsToString(periodSeconds);
			JToken obj = await MakeJsonRequestAsync<JToken>(url, BaseUrlApi);

			return obj.Select(
							token =>
									this.ParseCandle(
											token,
											marketSymbol,
											periodSeconds,
											1,
											2,
											3,
											4,
											0,
											TimestampType.UnixMilliseconds,
											5,
											7
									)
					)
					.ToList();
		}

		protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
		{
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/account",
					BaseUrlApi,
					await GetNoncePayloadAsync()
			);
			Dictionary<string, decimal> balances = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);
			foreach (JToken balance in token["balances"])
			{
				decimal amount =
						balance["free"].ConvertInvariant<decimal>()
						+ balance["locked"].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					balances[balance["asset"].ToStringInvariant()] = amount;
				}
			}
			return balances;
		}

		protected override async Task<
				Dictionary<string, decimal>
		> OnGetAmountsAvailableToTradeAsync()
		{
			JToken token = await MakeJsonRequestAsync<JToken>(
					"/account",
					BaseUrlApi,
					await GetNoncePayloadAsync()
			);
			Dictionary<string, decimal> balances = new Dictionary<string, decimal>(
					StringComparer.OrdinalIgnoreCase
			);
			foreach (JToken balance in token["balances"])
			{
				decimal amount = balance["free"].ConvertInvariant<decimal>();
				if (amount > 0m)
				{
					balances[balance["asset"].ToStringInvariant()] = amount;
				}
			}
			return balances;
		}

		protected override async Task<ExchangeOrderResult?> OnPlaceOrderAsync(
				ExchangeOrderRequest order
		)
		{
			if (order.Price == null && order.OrderType != OrderType.Market)
				throw new ArgumentNullException(nameof(order.Price));

			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload["symbol"] = order.MarketSymbol;
			payload["newClientOrderId"] = order.ClientOrderId;
			payload["side"] = order.IsBuy ? "BUY" : "SELL";
			if (order.OrderType == OrderType.Stop)
				payload["type"] = "STOP_LOSS"; //if order type is stop loss/limit, then binance expect word 'STOP_LOSS' inestead of 'STOP'
			else if (order.IsPostOnly == true)
			{
				if (order.OrderType == OrderType.Limit)
					payload["type"] = "LIMIT_MAKER"; // LIMIT_MAKER are LIMIT orders that will be rejected if they would immediately match and trade as a taker.
				else
					throw new NotSupportedException(
							"PostOnly with non limit orders are not currently supported on Binance. Please submit a PR if you are interested in this feature"
					);
			}
			else
				payload["type"] = order.OrderType.ToStringUpperInvariant();

			// Binance has strict rules on which prices and quantities are allowed. They have to match the rules defined in the market definition.
			decimal outputQuantity = await ClampOrderQuantity(order.MarketSymbol, order.Amount);

			// Binance does not accept quantities with more than 20 decimal places.
			payload["quantity"] = Math.Round(outputQuantity, 20);
			payload["newOrderRespType"] = "FULL";

			if (order.OrderType != OrderType.Market)
			{
				decimal outputPrice = await ClampOrderPrice(order.MarketSymbol, order.Price.Value);
				payload["price"] = outputPrice;
				if (order.IsPostOnly != true)
					payload["timeInForce"] = "GTC";
			}
			order.ExtraParameters.CopyTo(payload);

			JToken? token = await MakeJsonRequestAsync<JToken>(
					"/order",
					BaseUrlApi,
					payload,
					"POST"
			);
			if (token is null)
			{
				return null;
			}
			return ParseOrder(token);
		}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(
				string orderId,
				string? marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			if (string.IsNullOrWhiteSpace(marketSymbol))
			{
				throw new ArgumentNullException(
						"Binance single order details request requires symbol"
				);
			}
			payload["symbol"] = marketSymbol!;

			if (isClientOrderId) // Either orderId or origClientOrderId must be sent.
				payload["origClientOrderId"] = orderId;
			else
				payload["orderId"] = orderId;

			JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrlApi, payload);
			ExchangeOrderResult result = ParseOrder(token);

			// Add up the fees from each trade in the order
			Dictionary<string, object> feesPayload = await GetNoncePayloadAsync();
			feesPayload["symbol"] = marketSymbol!;
			if (!isClientOrderId)
			{
				feesPayload["orderId"] = orderId;
			}
			JToken feesToken = await MakeJsonRequestAsync<JToken>(
					"/myTrades",
					BaseUrlApi,
					feesPayload
			);
			ParseFees(feesToken, result);

			return result;
		}

		/// <summary>Process the trades that executed as part of your order and sum the fees.</summary>
		/// <param name="feesToken">The trades executed for a specific currency pair.</param>
		/// <param name="result">The result object to append to.</param>
		private static void ParseFees(JToken feesToken, ExchangeOrderResult result)
		{
			var tradesInOrder = feesToken.Where(
					x => x["orderId"].ToStringInvariant() == result.OrderId
			);

			bool currencySet = false;
			foreach (var trade in tradesInOrder)
			{
				result.Fees ??= 0;
				result.Fees += trade["commission"].ConvertInvariant<decimal>();

				// TODO: Not sure how to handle commissions in different currencies, for example if you run out of BNB mid-trade
				if (!currencySet)
				{
					result.FeesCurrency = trade["commissionAsset"].ToStringInvariant();
					currencySet = true;
				}
			}
		}

		protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(
				string? marketSymbol = null
		)
		{
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			if (!string.IsNullOrWhiteSpace(marketSymbol))
			{
				payload["symbol"] = marketSymbol!;
			}
			JToken token = await MakeJsonRequestAsync<JToken>("/openOrders", BaseUrlApi, payload);
			foreach (JToken order in token)
			{
				orders.Add(ParseOrder(order));
			}

			return orders;
		}

		private async Task<IEnumerable<ExchangeOrderResult>> GetCompletedOrdersForAllSymbolsAsync(
				DateTime? afterDate
		)
		{
			// TODO: This is a HACK, Binance API needs to add a single API call to get all orders for all symbols, terrible...
			List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
			Exception? ex = null;
			string? failedSymbol = null;
			Parallel.ForEach(
					(await GetMarketSymbolsAsync()).Where(
							s => s.IndexOf("BTC", StringComparison.OrdinalIgnoreCase) >= 0
					),
					async (s) =>
					{
						try
						{
							foreach (
												ExchangeOrderResult order in (
														await GetCompletedOrderDetailsAsync(s, afterDate)
												)
										)
							{
								lock (orders)
								{
									orders.Add(order);
								}
							}
						}
						catch (Exception _ex)
						{
							failedSymbol = s;
							ex = _ex;
						}
					}
			);

			if (ex != null)
			{
				throw new APIException(
						"Failed to get completed order details for symbol " + failedSymbol,
						ex
				);
			}

			// sort timestamp desc
			orders.Sort(
					(o1, o2) =>
					{
						return o2.OrderDate.CompareTo(o1.OrderDate);
					}
			);
			return orders;
		}

		protected override async Task<
				IEnumerable<ExchangeOrderResult>
		> OnGetCompletedOrderDetailsAsync(string? marketSymbol = null, DateTime? afterDate = null)
		{
			//new way
			List<ExchangeOrderResult> trades = new List<ExchangeOrderResult>();
			if (string.IsNullOrWhiteSpace(marketSymbol))
			{
				trades.AddRange(await GetCompletedOrdersForAllSymbolsAsync(afterDate));
			}
			else
			{
				Dictionary<string, object> payload = await GetNoncePayloadAsync();
				payload["symbol"] = marketSymbol!;
				if (afterDate != null)
				{
					payload["startTime"] = afterDate.Value.UnixTimestampFromDateTimeMilliseconds();
				}
				JToken token = await MakeJsonRequestAsync<JToken>("/myTrades", BaseUrlApi, payload);
				foreach (JToken trade in token)
				{
					trades.Add(ParseTrade(trade, marketSymbol!));
				}
			}
			return trades;
		}

		private async Task<IEnumerable<ExchangeOrderResult>> OnGetMyTradesAsync(
				string? marketSymbol = null,
				DateTime? afterDate = null
		)
		{
			List<ExchangeOrderResult> trades = new List<ExchangeOrderResult>();
			if (string.IsNullOrWhiteSpace(marketSymbol))
			{
				trades.AddRange(await GetCompletedOrdersForAllSymbolsAsync(afterDate));
			}
			else
			{
				Dictionary<string, object> payload = await GetNoncePayloadAsync();
				payload["symbol"] = marketSymbol!;
				if (afterDate != null)
				{
					payload["timestamp"] = afterDate.Value.UnixTimestampFromDateTimeMilliseconds();
				}
				JToken token = await MakeJsonRequestAsync<JToken>("/myTrades", BaseUrlApi, payload);
				foreach (JToken trade in token)
				{
					trades.Add(ParseTrade(trade, marketSymbol!));
				}
			}
			return trades;
		}

		protected override async Task OnCancelOrderAsync(
				string orderId,
				string? marketSymbol = null,
				bool isClientOrderId = false
		)
		{
			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			if (string.IsNullOrWhiteSpace(marketSymbol))
			{
				throw new ArgumentNullException("Binance cancel order request requires symbol");
			}
			payload["symbol"] = marketSymbol!;
			if (isClientOrderId) // Either orderId or origClientOrderId must be sent.
				payload["origClientOrderId"] = orderId;
			else
				payload["orderId"] = orderId;
			var token = await MakeJsonRequestAsync<JToken>("/order", BaseUrlApi, payload, "DELETE");
			var cancelledOrder = ParseOrder(token);
			if (cancelledOrder.OrderId != orderId)
				throw new APIException(
						$"Cancelled {cancelledOrder.OrderId} when trying to cancel {orderId}"
				);
		}

		/// <summary>A withdrawal request. Fee is automatically subtracted from the amount.</summary>
		/// <param name="withdrawalRequest">The withdrawal request.</param>
		/// <returns>Withdrawal response from Binance</returns>
		protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(
				ExchangeWithdrawalRequest withdrawalRequest
		)
		{
			if (string.IsNullOrWhiteSpace(withdrawalRequest.Currency))
			{
				throw new ArgumentException("Symbol must be provided for Withdraw");
			}
			else if (string.IsNullOrWhiteSpace(withdrawalRequest.Address))
			{
				throw new ArgumentException("Address must be provided for Withdraw");
			}
			else if (withdrawalRequest.Amount <= 0)
			{
				throw new ArgumentException("Withdrawal amount must be positive and non-zero");
			}

			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload["coin"] = withdrawalRequest.Currency;
			payload["address"] = withdrawalRequest.Address;
			payload["amount"] = withdrawalRequest.Amount;

			if (!string.IsNullOrWhiteSpace(withdrawalRequest.Description))
			{
				payload["name"] = withdrawalRequest.Description;
			}

			if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
			{
				payload["addressTag"] = withdrawalRequest.AddressTag;
			}

			JToken response = await MakeJsonRequestAsync<JToken>(
					"/capital/withdraw/apply",
					BaseUrlSApi,
					payload,
					"POST"
			);
			ExchangeWithdrawalResponse withdrawalResponse = new ExchangeWithdrawalResponse
			{
				Id = response["id"].ToStringInvariant(),
				Message = response["msg"].ToStringInvariant(),
			};

			return withdrawalResponse;
		}

		private static bool ParseMarketStatus(string status)
		{
			bool isActive = false;
			if (!string.IsNullOrWhiteSpace(status))
			{
				switch (status)
				{
					case "TRADING":
						isActive = true;
						break;
						/*
								case "PRE_TRADING":
								case "POST_TRADING":
								case "END_OF_DAY":
								case "HALT":
								case "AUCTION_MATCH":
								case "BREAK": */
				}
			}

			return isActive;
		}

		private async Task<ExchangeTicker> ParseTickerAsync(string symbol, JToken token)
		{
			// {"priceChange":"-0.00192300","priceChangePercent":"-4.735","weightedAvgPrice":"0.03980955","prevClosePrice":"0.04056700","lastPrice":"0.03869000","lastQty":"0.69300000","bidPrice":"0.03858500","bidQty":"38.35000000","askPrice":"0.03869000","askQty":"31.90700000","openPrice":"0.04061300","highPrice":"0.04081900","lowPrice":"0.03842000","volume":"128015.84300000","quoteVolume":"5096.25362239","openTime":1512403353766,"closeTime":1512489753766,"firstId":4793094,"lastId":4921546,"count":128453}
			return await this.ParseTickerAsync(
					token,
					symbol,
					"askPrice",
					"bidPrice",
					"lastPrice",
					"volume",
					"quoteVolume",
					"closeTime",
					TimestampType.UnixMilliseconds
			);
		}

		private async Task<ExchangeTicker> ParseTickerWebSocketAsync(JToken token)
		{
			string marketSymbol = token["s"].ToStringInvariant();
			return await this.ParseTickerAsync(
					token,
					marketSymbol,
					"a",
					"b",
					"c",
					"v",
					"q",
					"E",
					TimestampType.UnixMilliseconds
			);
		}

		private static ExchangeOrderResult ParseOrder(JToken token)
		{
			/*
				"symbol": "IOTABTC",
				"orderId": 1,
				"clientOrderId": "12345",
				"transactTime": 1510629334993,
				"price": "1.00000000",
				"origQty": "1.00000000",
				"executedQty": "0.00000000",
				"status": "NEW",
				"timeInForce": "GTC",
				"type": "LIMIT",
				"side": "SELL",
				"fills": [
						{
							"price": "4000.00000000",
							"qty": "1.00000000",
							"commission": "4.00000000",
							"commissionAsset": "USDT"
						},
						{
							"price": "3999.00000000",
							"qty": "5.00000000",
							"commission": "19.99500000",
							"commissionAsset": "USDT"
						},
						{
							"price": "3998.00000000",
							"qty": "2.00000000",
							"commission": "7.99600000",
							"commissionAsset": "USDT"
						},
						{
							"price": "3997.00000000",
							"qty": "1.00000000",
							"commission": "3.99700000",
							"commissionAsset": "USDT"
						},
						{
							"price": "3995.00000000",
							"qty": "1.00000000",
							"commission": "3.99500000",
							"commissionAsset": "USDT"
						}
					]
			*/
			ExchangeOrderResult result = new ExchangeOrderResult
			{
				Amount = token["origQty"].ConvertInvariant<decimal>(),
				AmountFilled = token["executedQty"].ConvertInvariant<decimal>(),
				Price = token["price"].ConvertInvariant<decimal>(),
				IsBuy = token["side"].ToStringInvariant() == "BUY",
				OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
							token["time"].ConvertInvariant<long>(
									token["transactTime"].ConvertInvariant<long>()
							)
					),
				OrderId = token["orderId"].ToStringInvariant(),
				MarketSymbol = token["symbol"].ToStringInvariant(),
				ClientOrderId = token["clientOrderId"].ToStringInvariant()
			};

			result.ResultCode = token["status"].ToStringInvariant();
			result.Result = ParseExchangeAPIOrderResult(
					result.ResultCode,
					result.AmountFilled.Value
			);
			ParseAveragePriceAndFeesFromFills(result, token["fills"]);

			return result;
		}

		internal static ExchangeAPIOrderResult ParseExchangeAPIOrderResult(
				string status,
				decimal amountFilled
		)
		{
			switch (status)
			{
				case "NEW":
					return ExchangeAPIOrderResult.Open;
				case "PARTIALLY_FILLED":
					return ExchangeAPIOrderResult.FilledPartially;
				case "FILLED":
					return ExchangeAPIOrderResult.Filled;
				case "CANCELED":
					return amountFilled > 0
							? ExchangeAPIOrderResult.FilledPartiallyAndCancelled
							: ExchangeAPIOrderResult.Canceled;
				case "PENDING_CANCEL":
					return ExchangeAPIOrderResult.PendingCancel;
				case "EXPIRED":
					return ExchangeAPIOrderResult.Expired;
				case "REJECTED":
					return ExchangeAPIOrderResult.Rejected;
				default:
					throw new NotImplementedException($"Unexpected status type: {status}");
			}
		}

		private static ExchangeOrderResult ParseTrade(JToken token, string symbol)
		{
			/*
				[
					 {
							"id": 28457,
							"orderId": 100234,
							"price": "4.00000100",
							"qty": "12.00000000",
							"commission": "10.10000000",
							"commissionAsset": "BNB",
							"time": 1499865549590,
							"isBuyer": true,
							"isMaker": false,
							"isBestMatch": true
					 }
				]
			*/
			ExchangeOrderResult result = new ExchangeOrderResult
			{
				Result = ExchangeAPIOrderResult.Filled,
				Amount = token["qty"].ConvertInvariant<decimal>(),
				AmountFilled = token["qty"].ConvertInvariant<decimal>(),
				Price = token["price"].ConvertInvariant<decimal>(),
				AveragePrice = token["price"].ConvertInvariant<decimal>(),
				IsBuy = token["isBuyer"].ConvertInvariant<bool>() == true,
				// OrderDate - not provided here. ideally would be null but ExchangeOrderResult.OrderDate is not nullable
				CompletedDate = null, // order not necessarily fullly filled at this point
				TradeDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
							token["time"].ConvertInvariant<long>()
					),
				OrderId = token["orderId"].ToStringInvariant(),
				TradeId = token["id"].ToStringInvariant(),
				Fees = token["commission"].ConvertInvariant<decimal>(),
				FeesCurrency = token["commissionAsset"].ToStringInvariant(),
				MarketSymbol = symbol
			};

			return result;
		}

		private static void ParseAveragePriceAndFeesFromFills(
				ExchangeOrderResult result,
				JToken fillsToken
		)
		{
			decimal totalCost = 0;
			decimal totalQuantity = 0;

			bool currencySet = false;
			if (fillsToken is JArray)
			{
				foreach (var fill in fillsToken)
				{
					if (!currencySet)
					{
						result.FeesCurrency = fill["commissionAsset"].ToStringInvariant();
						currencySet = true;
					}

					result.Fees ??= 0;
					result.Fees += fill["commission"].ConvertInvariant<decimal>();

					decimal price = fill["price"].ConvertInvariant<decimal>();
					decimal quantity = fill["qty"].ConvertInvariant<decimal>();
					totalCost += price * quantity;
					totalQuantity += quantity;
				}
			}

			result.AveragePrice = (
					totalQuantity == 0 ? null : (decimal?)(totalCost / totalQuantity)
			);
		}

		protected override Task ProcessRequestAsync(
				IHttpWebRequest request,
				Dictionary<string, object>? payload
		)
		{
			if (
					CanMakeAuthenticatedRequest(payload)
					|| (payload == null && request.RequestUri.AbsoluteUri.Contains("userDataStream"))
			)
			{
				request.AddHeader("X-MBX-APIKEY", PublicApiKey!.ToUnsecureString());
			}
			return base.ProcessRequestAsync(request, payload);
		}

		protected override Uri ProcessRequestUrl(
				UriBuilder url,
				Dictionary<string, object>? payload,
				string? method
		)
		{
			if (CanMakeAuthenticatedRequest(payload))
			{
				// payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
				var query = (url.Query ?? string.Empty).Trim('?', '&');
				string newQuery =
						"timestamp="
						+ payload!["nonce"].ToStringInvariant()
						+ (query.Length != 0 ? "&" + query : string.Empty)
						+ (
								payload.Count > 1
										? "&" + CryptoUtility.GetFormForPayload(payload, false)
										: string.Empty
						);
				string signature = CryptoUtility.SHA256Sign(
						newQuery,
						CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey)
				);
				newQuery += "&signature=" + signature;
				url.Query = newQuery;
				return url.Uri;
			}
			return base.ProcessRequestUrl(url, payload, method);
		}

		/// <summary>
		/// Gets the address to deposit to and applicable details.
		/// </summary>
		/// <param name="currency">Currency to get address for</param>
		/// <param name="forceRegenerate">(ignored) Binance does not provide the ability to generate new addresses</param>
		/// <returns>
		/// Deposit address details (including tag if applicable, such as XRP)
		/// </returns>
		protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(
				string currency,
				bool forceRegenerate = false
		)
		{
			/*
			* TODO: Binance does not offer a "regenerate" option in the API, but a second IOTA deposit to the same address will not be credited
			* How does Binance handle GetDepositAddress for IOTA after it's been used once?
			* Need to test calling this API after depositing IOTA.
			*/

			Dictionary<string, object> payload = await GetNoncePayloadAsync();
			payload["coin"] = currency;

			JToken response = await MakeJsonRequestAsync<JToken>(
					"/capital/deposit/address",
					BaseUrlSApi,
					payload
			);
			ExchangeDepositDetails depositDetails = new ExchangeDepositDetails
			{
				Currency = response["coin"].ToStringInvariant(),
				Address = response["address"].ToStringInvariant(),
				AddressTag = response["tag"].ToStringInvariant()
			};

			return depositDetails;
		}

		/// <summary>Gets the deposit history for a symbol</summary>
		/// <param name="currency">The currency to check. Null for all symbols.</param>
		/// <returns>Collection of ExchangeCoinTransfers</returns>
		protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(
				string currency
		)
		{
			// TODO: API supports searching on status, startTime, endTime
			var payload = await GetNoncePayloadAsync();

			if (!string.IsNullOrWhiteSpace(currency))
			{
				payload["coin"] = currency;
			}

			var response = await MakeJsonRequestAsync<List<HistoryRecord>>(
					"/capital/deposit/hisrec",
					BaseUrlSApi,
					payload
			);
			var transactions = new List<ExchangeTransaction>();

			foreach (var item in response)
			{
				transactions.Add(
						new ExchangeTransaction
						{
							Timestamp = item.InsertTime.UnixTimeStampToDateTimeMilliseconds(),
							Amount = decimal.Parse(item.Amount),
							Currency = item.Coin.ToUpperInvariant(),
							Address = item.Address,
							AddressTag = item.AddressTag,
							BlockchainTxId = item.TxId,
							Status = item.Status switch
							{
								0 => TransactionStatus.Processing,
								1 => TransactionStatus.Complete,
								_ => TransactionStatus.Unknown
							}
						}
				);
			}

			return transactions;
		}

		protected override async Task<IWebSocket> OnUserDataWebSocketAsync(Action<object> callback)
		{
			var listenKey = await GetListenKeyAsync();
			return await ConnectPublicWebSocketAsync(
					$"/ws/{listenKey}",
					(_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						var eventType = token["e"].ToStringInvariant();
						switch (eventType)
						{
							case "executionReport": // systematically check to make sure we are dealing with expected cases here
								{
									var update = JsonConvert.DeserializeObject<ExecutionReport>(
														token.ToStringInvariant(),
														SerializerSettings
												);
									switch (update.CurrentExecutionType)
									{
										case "NEW ": // The order has been accepted into the engine.
											break;
										case "CANCELED": // The order has been canceled by the user.
											break;
										case "REPLACED": // (currently unused)
											throw new NotImplementedException(
																$"ExecutionType {update.CurrentExecutionType} is currently unused"
														);
											;
										case "REJECTED": // The order has been rejected and was not processed. (This is never pushed into the User Data Stream)
											throw new NotImplementedException(
																$"ExecutionType {update.CurrentExecutionType} is never pushed into the User Data Stream"
														);
											;
										case "TRADE": // Part of the order or all of the order's quantity has filled.
											break;
										case "EXPIRED": // The order was canceled according to the order type's rules (e.g. LIMIT FOK orders with no fill, LIMIT IOC or MARKET orders that partially fill) or by the exchange, (e.g. orders canceled during liquidation, orders canceled during maintenance)
											break;
										default:
											throw new NotImplementedException(
																$"Unexpected ExecutionType {update.CurrentExecutionType}"
														);
									}
									callback(update.ExchangeOrderResult);
									break;
								}
							case "outboundAccountInfo":
								throw new NotImplementedException(
													"has been removed (per binance 2021-01-01)"
											);
							case "outboundAccountPosition":
								{
									var update = JsonConvert.DeserializeObject<OutboundAccount>(
														token.ToStringInvariant(),
														SerializerSettings
												);
									callback(
														new ExchangeBalances()
														{
															EventTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
																		update.EventTime
																),
															BalancesUpdateType = BalancesUpdateType.Total,
															Balances = update.BalancesAsTotalDictionary
														}
												);
									callback(
														new ExchangeBalances()
														{
															EventTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
																		update.EventTime
																),
															BalancesUpdateType = BalancesUpdateType.AvailableToTrade,
															Balances = update.BalancesAsAvailableToTradeDictionary
														}
												);
									break;
								}
							case "listStatus":
								{ // fired as part of OCO order update
									// var update = JsonConvert.DeserializeObject<ListStatus>(token.ToStringInvariant());
									// no need to parse or call callback(), since OCO updates also send "executionReport"
									break;
								}
							case "balanceUpdate":
								{
									var update = JsonConvert.DeserializeObject<BalanceUpdate>(
														token.ToStringInvariant(),
														SerializerSettings
												);
									callback(
														new ExchangeBalances()
														{
															EventTime = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(
																		update.EventTime
																),
															BalancesUpdateType = BalancesUpdateType.Delta,
															Balances = new Dictionary<string, decimal>()
															{
																				{ update.Asset, update.BalanceDelta }
															}
														}
												);
									break;
								}
							default:
								throw new NotImplementedException($"Unexpected event type {eventType}");
						}
						return Task.CompletedTask;
					}
			);
		}

		protected override async Task<IWebSocket> OnGetCandlesWebSocketAsync(
				Func<MarketCandle, Task> callbackAsync,
				int periodSeconds,
				params string[] marketSymbols
		)
		{
			if (!marketSymbols.Any())
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}

			var period = PeriodSecondsToString(periodSeconds);
			var payload = marketSymbols.Select(s => $"{s.ToLowerInvariant()}@kline_{period}");
			var url = $"/stream?streams={string.Join("/", payload)}";
			if (url.Length > 2048)
			{
				throw new InvalidOperationException(
						$"URL length over the limit of 2048 characters. Consider splitting instruments in multiple connections."
				);
			}

			return await ConnectPublicWebSocketAsync(
					url,
					async (_socket, msg) =>
					{
						JToken token = JToken.Parse(msg.ToStringFromUTF8());

						if (token?["data"]?["k"] != null && token["data"]["s"] != null)
						{
							var candle = this.ParseCandle(
												token["data"]["k"],
												token["data"]["s"].ToStringInvariant(),
												periodSeconds,
												"o",
												"h",
												"l",
												"c",
												"t",
												TimestampType.UnixMilliseconds,
												"v",
												"q"
										);

							await callbackAsync(candle);
						}
					}
			);
		}

		public async Task<string> GetListenKeyAsync()
		{
			JToken response = await MakeJsonRequestAsync<JToken>(
					"/userDataStream",
					BaseUrlApi,
					null,
					"POST"
			);
			var listenKey = response["listenKey"].ToStringInvariant();
			return listenKey;
		}
	}
}
