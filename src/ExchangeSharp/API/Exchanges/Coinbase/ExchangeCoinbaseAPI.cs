/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExchangeSharp
{
  /// <summary>
  /// Warning: This API now uses Coinbase Advanced Trade V2/V3.
  /// If you are using legacy API keys from previous Coinbase versions they must be upgraded to Advanced Trade on the Coinbase site.
  /// These keys must be set before using the Coinbase API (sorry).
  /// </summary>
  public sealed partial class ExchangeCoinbaseAPI : ExchangeAPI
  {
  	public override string BaseUrl { get; set; } = "https://api.coinbase.com/api/v3/brokerage";
  	private readonly string BaseUrlV2 = "https://api.coinbase.com/v2";	// For Wallet Support
  	public override string BaseUrlWebSocket { get; set; } = "wss://advanced-trade-ws.coinbase.com";
	
  	private enum PaginationType { None, V2, V3}
  	private PaginationType pagination = PaginationType.None;
  	private string cursorNext;								

  	private Dictionary<string, string> Accounts = null;		// Cached Account IDs

  	private ExchangeCoinbaseAPI()
  	{
  		MarketSymbolIsUppercase = true;
  		MarketSymbolIsReversed = false;
  		RequestContentType = "application/json";
  		NonceStyle = NonceStyle.None;
  		WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
  		RateLimit = new RateGate(30, TimeSpan.FromSeconds(1));
  		base.RequestMaker.RequestStateChanged = ProcessResponse;
  	}

  	/// <summary>
  	/// This is used to capture Pagination instead of overriding the ProcessResponse
  	/// because the Pagination info is no longer in the Headers and ProcessResponse does not return the required Content
  	/// </summary>
  	/// <param name="maker"></param>
  	/// <param name="state"></param>
  	/// <param name="Response"></param>
  	private void ProcessResponse(IAPIRequestMaker maker, RequestMakerState state, object response)
   	{
   		// We can bypass serialization if we already know the last call isn't paginated
   		if (state == RequestMakerState.Finished && pagination != PaginationType.None)
   		{
   			JToken token = JsonConvert.DeserializeObject<JToken>((string)response);
 				if (token == null) return;
 				switch(pagination)
 				{	
 					case PaginationType.V2: cursorNext = token["pagination"]?["next_starting_after"]?.ToStringInvariant(); break;
 					case PaginationType.V3: cursorNext = token[CURSOR]?.ToStringInvariant(); break;
 				}
 			}
  	}

		#region BaseOverrides

  	/// <summary>
  	/// Overridden because we no longer need a nonce in the payload and passphrase is no longer used
  	/// </summary>
  	/// <param name="payload"></param>
  	/// <returns></returns>
		protected override bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
		{	
			return (PrivateApiKey != null && PublicApiKey != null);
		}

		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{
  		if (CanMakeAuthenticatedRequest(payload))
			{
  			string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToStringInvariant();	// If you're skittish about the local clock, you may retrieve the timestamp from the Coinbase Site
  			string body = CryptoUtility.GetJsonForPayload(payload);

  			// V2 wants PathAndQuery, V3 wants LocalPath for the sig (I guess they wanted to shave a nano-second or two - silly)
  			string path = request.RequestUri.AbsoluteUri.StartsWith(BaseUrlV2) ? request.RequestUri.PathAndQuery : request.RequestUri.LocalPath;
  			string signature = CryptoUtility.SHA256Sign(timestamp + request.Method.ToUpperInvariant() + path + body, PrivateApiKey.ToUnsecureString());		

  			request.AddHeader("CB-ACCESS-KEY", PublicApiKey.ToUnsecureString());
  			request.AddHeader("CB-ACCESS-SIGN", signature);
  			request.AddHeader("CB-ACCESS-TIMESTAMP", timestamp);
  			if (request.Method == "POST") await CryptoUtility.WriteToRequestAsync(request, body);
  		}
		}

		/// <summary>
		/// Sometimes the Fiat pairs are reported backwards, but Coinbase requires the fiat to be last of the pair
		/// Only three Fiat Currencies are supported
		/// </summary>
		/// <param name="marketSymbol"></param>
		/// <returns></returns>
		public override Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
		{
  		if (marketSymbol.StartsWith("USD-") || marketSymbol.StartsWith("EUR-") || marketSymbol.StartsWith("GRP-"))
  		{
  			var split = marketSymbol.Split(GlobalMarketSymbolSeparator);
  			return Task.FromResult(split[1] + GlobalMarketSymbolSeparator + split[0]);
  		}
  		else return Task.FromResult(marketSymbol);
		}

  #endregion

		#region GeneralProductEndpoints

  	protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
  	{
  		var markets = new List<ExchangeMarket>();
  		JToken products = await MakeJsonRequestAsync<JToken>("/products");
  		foreach (JToken product in products[PRODUCTS])
  		{
  			markets.Add(new ExchangeMarket
  			{
  				MarketSymbol = product[PRODUCTID].ToStringUpperInvariant(),
  				BaseCurrency = product["base_currency_id"].ToStringUpperInvariant(),
  				QuoteCurrency = product["quote_currency_id"].ToStringUpperInvariant(),
  				IsActive = string.Equals(product[STATUS].ToStringInvariant(), "online", StringComparison.OrdinalIgnoreCase),
  				MinTradeSize = product["base_min_size"].ConvertInvariant<decimal>(),
  				MaxTradeSize = product["base_max_size"].ConvertInvariant<decimal>(),
  				PriceStepSize = product["quote_increment"].ConvertInvariant<decimal>()
  			});
  		}
  		return markets.OrderBy(market => market.MarketSymbol);   // Ordered for Convenience
  	}

  	protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
  	{
  		return (await GetMarketSymbolsMetadataAsync()).Select(market => market.MarketSymbol);	
  	}

  	protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
  	{
  		var currencies = new Dictionary<string, ExchangeCurrency>();

  		// We don't have a currencies endpoint, but we can derive the currencies by splitting the products (includes fiat - filter if you wish)
  		JToken products = await MakeJsonRequestAsync<JToken>("/products");
  		foreach (JToken product in products[PRODUCTS])
  		{
  			var split = product[PRODUCTID].ToString().Split(GlobalMarketSymbolSeparator);
  			if (!currencies.ContainsKey(split[0]))
  			{
  				var currency = new ExchangeCurrency
  				{
  					Name = split[0],
  					FullName = product["base_name"].ToStringInvariant(),
  					DepositEnabled = true,
  					WithdrawalEnabled = true
  				};
  				currencies[currency.Name] = currency;
  			}
  			if (!currencies.ContainsKey(split[1]))
  			{
  				var currency = new ExchangeCurrency
  				{
  					Name = split[1],
  					FullName = product["quote_name"].ToStringInvariant(),
  					DepositEnabled = true,
  					WithdrawalEnabled = true
  				};
  				currencies[currency.Name] = currency;
  			}
  		}
  		return currencies;		
  	}

  	protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
  	{
  		var tickers = new List<KeyValuePair<string, ExchangeTicker>>();
  		JToken books = await MakeJsonRequestAsync<JToken>("/best_bid_ask");
  		var Timestamp = CryptoUtility.ParseTimestamp(books[TIME], TimestampType.Iso8601UTC);
  		foreach (JToken book in books[PRICEBOOKS])
  		{
  			var split = book[PRODUCTID].ToString().Split(GlobalMarketSymbolSeparator);
  			// This endpoint does not provide a last or open for the ExchangeTicker 
  			tickers.Add(new KeyValuePair<string, ExchangeTicker>(book[PRODUCTID].ToString(), new ExchangeTicker()
  			{
  				MarketSymbol = book[PRODUCTID].ToString(),
  				Ask = book[ASKS][0][PRICE].ConvertInvariant<decimal>(),
  				Bid = book[BIDS][0][PRICE].ConvertInvariant<decimal>(),
			    Volume = new ExchangeVolume()
  				{
						BaseCurrency = split[0],
	  				BaseCurrencyVolume = book[BIDS][0][SIZE].ConvertInvariant<decimal>(),
						QuoteCurrency = split[1],
  			    QuoteCurrencyVolume = book[ASKS][0][SIZE].ConvertInvariant<decimal>(),
  					Timestamp = Timestamp
  				}
  			}));
  		}
  		return tickers;
  	}

  	protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
  	{
  		JToken ticker = await MakeJsonRequestAsync<JToken>("/best_bid_ask?product_ids=" + marketSymbol);
  		JToken book = ticker[PRICEBOOKS][0];
  		var split = book[PRODUCTID].ToString().Split(GlobalMarketSymbolSeparator);
  		return new ExchangeTicker()
  		{
  			MarketSymbol = book[PRODUCTID].ToString(),
  			Ask = book[ASKS][0][PRICE].ConvertInvariant<decimal>(),
  			Bid = book[BIDS][0][PRICE].ConvertInvariant<decimal>(),
 		    Volume = new ExchangeVolume()
  			{
   				BaseCurrency = split[0],
  				BaseCurrencyVolume = book[BIDS][0][SIZE].ConvertInvariant<decimal>(),
  				QuoteCurrency = split[1],
 			    QuoteCurrencyVolume = book[ASKS][0][SIZE].ConvertInvariant<decimal>(),
  				Timestamp = DateTime.UtcNow
  			}
  		};	
  	}

  	protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 50)
  	{
  		JToken token = await MakeJsonRequestAsync<JToken>("/product_book?product_id=" + marketSymbol + "&limit=" + maxCount);
  		ExchangeOrderBook orderBook = new ExchangeOrderBook();
  		foreach(JToken bid in token[PRICEBOOK][BIDS]) orderBook.Bids.Add(bid[PRICE].ConvertInvariant<decimal>(), new ExchangeOrderPrice(){ Price = bid[PRICE].ConvertInvariant<decimal>(), Amount = bid[SIZE].ConvertInvariant<decimal>() });
  		foreach(JToken ask in token[PRICEBOOK][ASKS]) orderBook.Asks.Add(ask[PRICE].ConvertInvariant<decimal>(), new ExchangeOrderPrice(){ Price = ask[PRICE].ConvertInvariant<decimal>(), Amount = ask[SIZE].ConvertInvariant<decimal>() });
  		return orderBook;
  	}

  	protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = 100)
  	{
  		// Limit is required but maxed at 100 with no pagination available
  		limit = (limit == null || limit < 1 || limit > 100) ? 100 : (int)limit;
  		JToken trades = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol + "/ticker?limit=" + limit);
  		List<ExchangeTrade> tradeList = new List<ExchangeTrade>();
  		foreach (JToken trade in trades[TRADES]) tradeList.Add(trade.ParseTrade(SIZE, PRICE, SIDE, TIME, TimestampType.Iso8601UTC, TRADEID));
  		return tradeList;
  	}

  	protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
  	{
  		if (endDate == null) endDate = CryptoUtility.UtcNow;

  		string granularity = "UNKNOWN_GRANULARITY";
  		if (periodSeconds <= 60) { granularity = "ONE_MINUTE"; periodSeconds = 60; }
  		else if (periodSeconds <= 300) { granularity = "FIVE_MINUTE"; periodSeconds = 300; }
  		else if (periodSeconds <= 900) { granularity = "FIFTEEN_MINUTE"; periodSeconds = 900; }
  		else if (periodSeconds <= 1800) { granularity = "THIRTY_MINUTE"; periodSeconds = 1800; }
  		else if (periodSeconds <= 3600) { granularity = "ONE_HOUR"; periodSeconds = 3600; }
  		else if (periodSeconds <= 21600) { granularity = "SIX_HOUR"; periodSeconds = 21600; }
  		else { granularity = "ONE_DAY"; periodSeconds = 86400; }

  		// Returned Candle count is restricted to 300 and they don't paginate this call
  		// We're going to keep retrieving candles 300 at a time until we get our date range for the granularity
  		if (startDate == null) startDate = CryptoUtility.UtcNow.AddMinutes(-(periodSeconds * 300));
  		if (startDate >= endDate) throw new APIException("Invalid Date Range");
  		DateTime RangeStart = (DateTime)startDate, RangeEnd = (DateTime)endDate;
  		if ((RangeEnd - RangeStart).TotalSeconds / periodSeconds > 300) RangeStart = RangeEnd.AddSeconds(-(periodSeconds * 300));

  		List<MarketCandle> candles = new List<MarketCandle>();
  		while (true) 
  		{ 
  			JToken token = await MakeJsonRequestAsync<JToken>(string.Format("/products/{0}/candles?start={1}&end={2}&granularity={3}", marketSymbol, ((DateTimeOffset)RangeStart).ToUnixTimeSeconds(), ((DateTimeOffset)RangeEnd).ToUnixTimeSeconds(), granularity));
  			foreach (JToken candle in token["candles"])	candles.Add(this.ParseCandle(candle, marketSymbol, periodSeconds, "open", "high", "low", "close", "start", TimestampType.UnixSeconds, "volume"));
  			if (RangeStart > startDate)
  			{
  				// For simplicity, we'll go back 300 each iteration and sort/filter date range before return
  				RangeStart = RangeStart.AddSeconds(-(periodSeconds * 300));
  				RangeEnd = RangeEnd.AddSeconds(-(periodSeconds * 300));
  			}
  			else break;
  		} 
  		return candles.Where(c => c.Timestamp >= startDate).OrderBy(c => c.Timestamp);
  	}


  	protected override async Task<Dictionary<string, decimal>> OnGetFeesAsync()
  	{
  		var symbols = await OnGetMarketSymbolsAsync();
  		JToken token = await this.MakeJsonRequestAsync<JToken>("/transaction_summary");
  		Dictionary<string, decimal> fees = new Dictionary<string, decimal>();

  		// We can chose between maker and taker fee, but currently ExchangeSharp only supports 1 fee rate per market symbol.
  		// Here, we choose taker fee, which is usually higher
  		decimal makerRate = token["fee_tier"]["taker_fee_rate"].Value<decimal>(); //percentage between 0 and 1

  		return symbols.Select(symbol => new KeyValuePair<string, decimal>(symbol, makerRate)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
  	}


  	#endregion

 		#region AccountSpecificEndpoints

  	// WARNING: Currently V3 doesn't support Coinbase Wallet APIs, so we are reverting to V2 for this call. 
  	protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
  	{
  		if (Accounts  == null) await GetAmounts(true);		// Populate Accounts Cache
  		if (Accounts.ContainsKey(symbol))
  		{
  			JToken accountWalletAddress = await this.MakeJsonRequestAsync<JToken>($"/accounts/{Accounts[symbol]}/addresses", BaseUrlV2);
  			return new ExchangeDepositDetails { Address = accountWalletAddress[0]["address"].ToStringInvariant(), Currency = symbol };		// We only support a single Wallet/Address (Coinbase is the only Exchange that has multiple)
  		}
  		throw new APIException($"Address not found for {symbol}");
  	}

  	protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
  	{
  		return await GetAmounts(false);
  	}

  	protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
  	{
  		return await GetAmounts(true);
  	}

  	// WARNING: Currently V3 doesn't support Coinbase Wallet APIs, so we are reverting to V2 for this call. 
  	protected override async Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawHistoryAsync(string currency)
  	{
  		return await GetTx(true, currency);
  	}

  	// WARNING: Currently V3 doesn't support Coinbase Wallet APIs, so we are reverting to V2 for this call. 
  	protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
  	{
  		return await GetTx(false, currency);
  	}


  	// Warning: Max Open orders returned is 1000, which shouldn't be a problem. If it is (yikes), this can be replaced with the WebSocket User Channel.
  	protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
  	{
  		List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
  		pagination = PaginationType.V3;
  		string uri = string.IsNullOrEmpty(marketSymbol) ? "/orders/historical/batch?order_status=OPEN" : $"/orders/historical/batch?product_id={marketSymbol}&order_status=OPEN";   // Parameter order is critical
  		JToken token = await MakeJsonRequestAsync<JToken>(uri);
  		while(true)
  		{ 
  			foreach (JToken order in token[ORDERS]) if (order[TYPE].ToStringInvariant().Equals(ADVFILL)) orders.Add(ParseOrder(order));
  			if (string.IsNullOrEmpty(cursorNext)) break;
  			token = await MakeJsonRequestAsync<JToken>(uri + "&cursor=" + cursorNext);
  		}
  		pagination = PaginationType.None;
  		return orders;
  	}

  	protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
  	{
  		List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
  		pagination = PaginationType.V3;
  		string uri = string.IsNullOrEmpty(marketSymbol) ? "/orders/historical/batch?order_status=FILLED" : $"/orders/historical/batch?product_id={marketSymbol}&order_status=OPEN";   // Parameter order is critical
  		JToken token = await MakeJsonRequestAsync<JToken>(uri);
  		while(true)
  		{ 
  			foreach (JToken order in token[ORDERS]) orders.Add(ParseOrder(order));
  			if (string.IsNullOrEmpty(cursorNext)) break;
  			token = await MakeJsonRequestAsync<JToken>(uri + "&cursor=" + cursorNext);
  		}
  		pagination = PaginationType.None;
  		return orders;
  	}

		protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
  		JToken obj = await MakeJsonRequestAsync<JToken>("/orders/historical/" + orderId);
  		return ParseOrder(obj["order"]);
 		}

 		/// <summary>
 		/// This supports two Entries in the Order ExtraParameters:
 		/// "post_only" : bool (defaults to false if does not exist)
 		/// "gtd_timestamp : datetime (determines GTD order type if exists, otherwise GTC
 		/// </summary>
 		/// <param name="order"></param>
 		/// <returns></returns>
	 	protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
  	{
  		Dictionary<string, object> payload = new Dictionary<string, object>();

  		// According to the V3 Docs, a Unique Client OrderId is required. Currently this doesn't seem to be enforced by the API, but...
  		// If not set by the client give them one instead of throwing an exception. Uncomment below if you would rather not.
  		//if (string.IsNullOrEmpty(order.ClientOrderId)) throw new ApplicationException("Client Order Id is required");
  		if (string.IsNullOrEmpty(order.ClientOrderId)) { order.ClientOrderId =  Guid.NewGuid().ToString(); }

  		payload["client_order_id"] = order.ClientOrderId;
  		payload["product_id"] = order.MarketSymbol;
  		payload["side"] = order.IsBuy ? BUY : "SELL";

  		Dictionary<string, object> orderConfig = new Dictionary<string, object>();
  		switch (order.OrderType)
  		{
  			case OrderType.Limit:
  				if (order.ExtraParameters.ContainsKey("gtd_timestamp"))
  				{
  					orderConfig.Add("limit_limit_gtd", new Dictionary<string, object>()
  					{
  						{"base_size", order.Amount.ToStringInvariant() },
  						{"limit_price", order.Price.ToStringInvariant() },
  						{"end_time", order.ExtraParameters["gtd_timestamp"] },	
  						{"post_only", order.ExtraParameters.TryGetValueOrDefault( "post_only", false) }
  					});
  				}
  				else
  				{ 
  					orderConfig.Add("limit_limit_gtc", new Dictionary<string, object>()
  					{
  						{"base_size", order.Amount.ToStringInvariant() },
  						{"limit_price", order.Price.ToStringInvariant() },
  						{"post_only", order.ExtraParameters.TryGetValueOrDefault( "post_only", "false") }
  					});
  				}
  				break;
  			case OrderType.Stop:
  				if (order.ExtraParameters.ContainsKey("gtd_timestamp"))
  				{
  					orderConfig.Add("stop_limit_stop_limit_gtd", new Dictionary<string, object>()
  					{
  						{"base_size", order.Amount.ToStringInvariant() },
  						{"limit_price", order.Price.ToStringInvariant() },
  						{"stop_price", order.StopPrice.ToStringInvariant() },
  						{"end_time", order.ExtraParameters["gtd_timestamp"] },
  					});
  				}
  				else
  				{
  					orderConfig.Add("stop_limit_stop_limit_gtc", new Dictionary<string, object>()
  					{
  						{"base_size", order.Amount.ToStringInvariant() },
  						{"limit_price", order.Price.ToStringInvariant() },
  						{"stop_price", order.StopPrice.ToStringInvariant() },
  					});
  				}
  				break;
  			case OrderType.Market:
  				if (order.IsBuy) orderConfig.Add("market_market_ioc", new Dictionary<string, object>() { { "quote_size", order.Amount.ToStringInvariant() }});
  				else orderConfig.Add("market_market_ioc", new Dictionary<string, object>() { { "base_size", order.Amount.ToStringInvariant() }});
  				break;
  		}

  		payload.Add("order_configuration", orderConfig);

  		try
  		{
  			JToken result = await MakeJsonRequestAsync<JToken>($"/orders", payload: payload, requestMethod: "POST"  );
  			// The Post doesn't return with any status, just a new OrderId. To get the Order Details we have to reQuery.
  			return await OnGetOrderDetailsAsync(result[ORDERID].ToStringInvariant());
  		}
  		catch (Exception ex) // All fails come back with an exception. 
  		{
  			var token = JToken.Parse(ex.Message);
  			return new ExchangeOrderResult(){ Result = ExchangeAPIOrderResult.Rejected, ClientOrderId = order.ClientOrderId, ResultCode = token["error_response"]["error"].ToStringInvariant() };
  		}
  	}

		protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
		{
  			Dictionary<string, object> payload = new Dictionary<string, object>() {{ "order_ids", new [] { orderId } }	};
	  		await MakeJsonRequestAsync<JArray>("/orders/batch_cancel", payload: payload, requestMethod: "POST");
 		}

  	protected override Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
  	{
  		return base.OnWithdrawAsync(withdrawalRequest);
  	}

  	#endregion

 		#region SocketEndpoints

  	protected override Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 100, params string[] marketSymbols)
  	{
  		return ConnectWebSocketAsync(BaseUrlWebSocket, (_socket, msg) =>
  		{
  			JToken tokens = JToken.Parse(msg.ToStringFromUTF8());
  			if (tokens[EVENTS][0][TYPE] == null || tokens[EVENTS][0]["updates"] == null ) return Task.CompletedTask;

  			string type = tokens[EVENTS][0][TYPE].ToStringInvariant();
  			if (type.Equals("update") || type.Equals("snapshot"))
  			{
  				var book = new ExchangeOrderBook(){ MarketSymbol = tokens[EVENTS][0][PRODUCTID].ToStringInvariant(), LastUpdatedUtc = DateTime.UtcNow, SequenceId =  tokens["sequence_num"].ConvertInvariant<long>() };
  				int askCount = 0, bidCount = 0;
  				foreach(var token in tokens[EVENTS][0]["updates"])
  				{
  					if (token[SIDE].ToStringInvariant().Equals("bid"))
  					{
  						if (bidCount++ < maxCount)
  						{
  							decimal price = token[PRICELEVEL].ConvertInvariant<decimal>();
  							book.Bids.Add( price, new ExchangeOrderPrice(){ Price = price, Amount=token["new_quantity"].ConvertInvariant<decimal>()} );
  						}
  					}
  					else if (token[SIDE].ToStringInvariant().Equals("offer"))  // One would think this would be 'ask' but no...
  					{
  						if (askCount++ < maxCount)
  						{
  							decimal price = token[PRICELEVEL].ConvertInvariant<decimal>();
  							book.Asks.Add( price, new ExchangeOrderPrice(){ Price = price, Amount=token["new_quantity"].ConvertInvariant<decimal>()} );
  						}
  					}
  					if (askCount >= maxCount && bidCount >=maxCount) break;
  				}
  				callback?.Invoke(book);
  			} 
  			return Task.CompletedTask;				
  		}, async (_socket) =>
  		{
  			string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToStringInvariant();
  			string signature = CryptoUtility.SHA256Sign(timestamp + LEVEL2 + string.Join(",", marketSymbols), PrivateApiKey.ToUnsecureString());
  			var subscribeRequest = new
  			{
  				type = SUBSCRIBE,
  				product_ids = marketSymbols,
  				channel = LEVEL2,
  				api_key = PublicApiKey.ToUnsecureString(),
  				timestamp,
  				signature
  			};
  			await _socket.SendMessageAsync(subscribeRequest);
  		});
  	}

  	protected override async Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
  	{
  		return await ConnectWebSocketAsync(BaseUrlWebSocket, async (_socket, msg) =>
  		{
  			JToken tokens = JToken.Parse(msg.ToStringFromUTF8());

  			var timestamp = tokens["timestamp"].ConvertInvariant<DateTime>();
  			List<KeyValuePair<string, ExchangeTicker>> ticks = new List<KeyValuePair<string, ExchangeTicker>>();
  			foreach(var token in tokens[EVENTS]?[0]?["tickers"])
  			{
  				string product = token[PRODUCTID].ToStringInvariant();
  				var split = product.Split(GlobalMarketSymbolSeparator);

  				ticks.Add(new KeyValuePair<string, ExchangeTicker>(product, new ExchangeTicker()
  				{
  					// We don't have Bid or Ask info on this feed
 					  ApiResponse = token,
 			 	    Last = token[PRICE].ConvertInvariant<decimal>(),
  					Volume = new ExchangeVolume()
  					{
  						BaseCurrency = split[0],
  						QuoteCurrency = split[1],
  						BaseCurrencyVolume = token["volume_24_h"].ConvertInvariant<decimal>(),
  						Timestamp = timestamp			  
  					}
  				} ));
  			}
  			callback?.Invoke(ticks);
  		}, async (_socket) =>
  		{
  			string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToStringInvariant();
  			string signature = CryptoUtility.SHA256Sign(timestamp + TICKER + string.Join(",", marketSymbols), PrivateApiKey.ToUnsecureString());
  			var subscribeRequest = new
  			{
  				type = SUBSCRIBE,
  				product_ids = marketSymbols,
  				channel = TICKER,
  				api_key = PublicApiKey.ToUnsecureString(),
  				timestamp,
  				signature
  			};
  			await _socket.SendMessageAsync(subscribeRequest);
  		});
  	}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
  		if (marketSymbols == null || marketSymbols.Length == 0) marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
  		return await ConnectWebSocketAsync(BaseUrlWebSocket, async (_socket, msg) =>
  		{
  			JToken tokens = JToken.Parse(msg.ToStringFromUTF8());
  			if (tokens[EVENTS][0][TRADES] == null) return; // This is most likely a subscription confirmation (they don't document this)
  			foreach(var token in tokens[EVENTS]?[0]?[TRADES])
  			{
  				if (token[TRADEID] == null) continue;
  				callback?.Invoke(new KeyValuePair<string, ExchangeTrade>(token[PRODUCTID].ToStringInvariant(), new ExchangeTrade()
  				{
  					Amount = token[SIZE].ConvertInvariant<decimal>(),
  					Price = token[PRICE].ConvertInvariant<decimal>(),
  					IsBuy = token[SIDE].ToStringInvariant().Equals(BUY),
  					Id = token[TRADEID].ToStringInvariant(),
  					Timestamp = token[TIME].ConvertInvariant<DateTime>()
  				}));
  			}
  		}, async (_socket) =>
  		{
  			string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToStringInvariant();
  			string signature = CryptoUtility.SHA256Sign(timestamp + MARKETTRADES + string.Join(",", marketSymbols), PrivateApiKey.ToUnsecureString());
  			var subscribeRequest = new
  			{
  				type = SUBSCRIBE,
  				product_ids = marketSymbols,
  				channel = MARKETTRADES,
  				api_key = PublicApiKey.ToUnsecureString(),
  				timestamp,
  				signature
  			};
  			await _socket.SendMessageAsync(subscribeRequest);
  		});
  	}

 	#endregion

 		#region PrivateFunctions

  	private async Task<Dictionary<string, decimal>> GetAmounts(bool AvailableOnly)
  	{
  		Accounts ??= new Dictionary<string, string>();	// This function is the only place where Accounts cache is populated

  		Dictionary<string, decimal> amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
  		pagination = PaginationType.V3;
  		JToken token = await MakeJsonRequestAsync<JToken>("/accounts");
  		while(true)
  		{
  			foreach (JToken account in token["accounts"])
  			{
  				Accounts[account[CURRENCY].ToString()] = account["uuid"].ToString();		// populate Accounts cache as we go
  				decimal amount = AvailableOnly ? account["available_balance"][VALUE].ConvertInvariant<decimal>() : account["available_balance"][VALUE].ConvertInvariant<decimal>() + account["hold"][VALUE].ConvertInvariant<decimal>();
  				if (amount > 0.0m) amounts[account[CURRENCY].ToStringInvariant()] = amount;
  			}
  			if (string.IsNullOrEmpty(cursorNext)) break;
  			token = await MakeJsonRequestAsync<JToken>("/accounts?starting_after=" + cursorNext);
  		} 
  		pagination = PaginationType.None;
  		return amounts;
  	}

  	/// <summary>
  	/// Warning: This call uses V2 Transactions
  	/// </summary>
  	/// <param name="Withdrawals"></param>
  	/// <param name="currency"></param>
  	/// <returns></returns>
  	private async Task<List<ExchangeTransaction>> GetTx(bool Withdrawals, string currency)
  	{
  		if (Accounts  == null) await GetAmounts(true);		
  		pagination = PaginationType.V2;
  		List<ExchangeTransaction> transfers = new List<ExchangeTransaction>();
  		JToken tokens = await MakeJsonRequestAsync<JToken>($"accounts/{Accounts[currency]}/transactions", BaseUrlV2);
  		while(true)
  		{ 
  			foreach (JToken token in tokens)
  			{
  				// A "send" to Coinbase is when someone "sent" you coin - or a receive to the rest of the world
  				// Likewise, a "receive" is when someone "received" coin from you. In other words, it's back-asswards.
  				if (!Withdrawals && token[TYPE].ToStringInvariant().Equals("send")) transfers.Add(ParseTransaction(token));
  				else if (Withdrawals && token[TYPE].ToStringInvariant().Equals("receive")) transfers.Add(ParseTransaction(token));
  			}
  			if (string.IsNullOrEmpty(cursorNext)) break;
  			tokens = await MakeJsonRequestAsync<JToken>($"accounts/{Accounts[currency]}/transactions?starting_after={cursorNext}", BaseUrlV2);
  		} 
  		pagination = PaginationType.None;
  		return transfers;
  	}

  	/// <summary>
  	/// Parse V2 Transaction of type of either "Send" or "Receive"
  	/// </summary>
  	/// <param name="token"></param>
  	/// <returns></returns>
  	private ExchangeTransaction ParseTransaction(JToken token)
  	{
  		// The Coin Address/TxFee isn't available but can be retrieved using the Network Hash/BlockChainId
  		return new ExchangeTransaction()
  		{ 
  			PaymentId = token["id"].ToStringInvariant(),				// Not sure how this is used elsewhere but here it is the Coinbase TransactionID
  			BlockchainTxId = token["network"]["hash"].ToStringInvariant(),
  			Currency = token[AMOUNT][CURRENCY].ToStringInvariant(),
  			Amount = token[AMOUNT][AMOUNT].ConvertInvariant<decimal>(),
  			Timestamp = token["created_at"].ToObject<DateTime>(),
  			Status = token[STATUS].ToStringInvariant() == "completed" ? TransactionStatus.Complete : TransactionStatus.Unknown,
  			Notes = token["description"].ToStringInvariant()
  			// Address 
  			// AddressTag 
  			// TxFee 
  		};
  	}

  	private ExchangeOrderResult ParseOrder(JToken result)
  	{
			return new ExchangeOrderResult
			{
				OrderId = result[ORDERID].ToStringInvariant(),
				ClientOrderId = result["client_order_id"].ToStringInvariant(),
				MarketSymbol = result[PRODUCTID].ToStringInvariant(),
				Fees = result["total_fees"].ConvertInvariant<decimal>(),
				OrderDate = result["created_time"].ToDateTimeInvariant(),
				CompletedDate = result["last_fill_time"].ToDateTimeInvariant(),
				AmountFilled = result["filled_size"].ConvertInvariant<decimal>(),
				AveragePrice = result["average_filled_price"].ConvertInvariant<decimal>(),
				IsBuy = result[SIDE].ToStringInvariant() == BUY,
				Result = result[STATUS].ToStringInvariant() switch
				{
					"FILLED" => ExchangeAPIOrderResult.Filled,
					"OPEN" => ExchangeAPIOrderResult.Open,
					"CANCELLED" => ExchangeAPIOrderResult.Canceled,
					"EXPIRED" => ExchangeAPIOrderResult.Expired,
					"FAILED" => ExchangeAPIOrderResult.Rejected,
					_ => ExchangeAPIOrderResult.Unknown,
				}
			};
  	}

  	#endregion

  }

  public partial class ExchangeName { public const string Coinbase = "Coinbase"; }
}
