/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using ExchangeSharp.Coinbase;
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
	public sealed class ExchangeCoinbaseAPI : ExchangeAPI
  {
  	private const string ADVFILL = "advanced_trade_fill";
  	private const string CURRENCY = "currency";
  	private const string PRODUCTID = "product_id";
  	private const string PRODUCTS = "products";
  	private const string PRICEBOOK = "pricebook";
  	private const string PRICEBOOKS = "pricebooks";
  	private const string ASKS = "asks";
  	private const string BIDS = "bids";
  	private const string PRICE = "price";
  	private const string AMOUNT = "amount";
  	private const string VALUE = "value";
  	private const string SIZE = "size";
  	private const string CURSOR = "cursor";


  	public override string BaseUrl { get; set; } = "https://api.coinbase.com/api/v3/brokerage";
  	private readonly string BaseURLV2 = "https://api.coinbase.com/v2";	// For Wallet Support
  	public override string BaseUrlWebSocket { get; set; } = "wss://advanced-trade-ws.coinbase.com";
	
  	private enum PaginationType { None, V2, V3, V3Cursor}
  	private PaginationType pagination = PaginationType.None;
  	private string cursorNext;								

  	private Dictionary<string, string> Accounts = null;		// Cached Account IDs

  	private ExchangeCoinbaseAPI()
  	{
  		MarketSymbolIsReversed = false;
  		RequestContentType = "application/json";
  		NonceStyle = NonceStyle.None;
  		WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;
  		RateLimit = new RateGate(10, TimeSpan.FromSeconds(1));
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
				cursorNext = null;
  			JToken token = JsonConvert.DeserializeObject<JToken>((string)response);
				if (token == null) return;
				switch(pagination)
				{	
					case PaginationType.V2: cursorNext = token["pagination"]?["next_starting_after"]?.ToStringInvariant(); break;
					case PaginationType.V3: cursorNext = token["has_next"].ToStringInvariant().Equals("True") ? token[CURSOR]?.ToStringInvariant() : null; break;
					case PaginationType.V3Cursor: cursorNext = token[CURSOR]?.ToStringInvariant(); break;		// Only used for V3 Fills - go figure.
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

  	protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
  	{
  		if (CanMakeAuthenticatedRequest(payload))
  		{
  			string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToStringInvariant();	// If you're skittish about the local clock, you may retrieve the timestamp from the Coinbase Site
  			string body = CryptoUtility.GetJsonForPayload(payload);

  			// V2 wants PathAndQuery, V3 wants LocalPath for the sig 
  			string path = request.RequestUri.AbsoluteUri.StartsWith(BaseURLV2) ? request.RequestUri.PathAndQuery : request.RequestUri.LocalPath;
  			string signature = CryptoUtility.SHA256Sign(timestamp + request.Method.ToUpperInvariant() + path + body, PrivateApiKey.ToUnsecureString());		

  			request.AddHeader("CB-ACCESS-KEY", PublicApiKey.ToUnsecureString());
  			request.AddHeader("CB-ACCESS-SIGN", signature);
  			request.AddHeader("CB-ACCESS-TIMESTAMP", timestamp);
  			if (request.Method == "POST") await CryptoUtility.WriteToRequestAsync(request, body);
  		}
  	}

  	#endregion

  	#region GeneralProductEndpoints

  	protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
  	{
  		var markets = new List<ExchangeMarket>();
  		JToken products = await MakeJsonRequestAsync<JToken>("/products");
  		foreach (JToken product in products[PRODUCTS])
  		{
  			markets.Add(new ExchangeMarket()
  			{
  				MarketSymbol = product[PRODUCTID].ToStringUpperInvariant(),
  				BaseCurrency = product["base_currency_id"].ToStringUpperInvariant(),
  				QuoteCurrency = product["quote_currency_id"].ToStringUpperInvariant(),
  				IsActive = string.Equals(product["status"].ToStringInvariant(), "online", StringComparison.OrdinalIgnoreCase),
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
  		var Timestamp = CryptoUtility.ParseTimestamp(books["time"], TimestampType.Iso8601UTC);
  		foreach (JToken book in books[PRICEBOOKS])
  		{
  			var split = book[PRODUCTID].ToString().Split(GlobalMarketSymbolSeparator);
  			// This endpoint does not provide a last or open for the ExchangeTicker. We might get this from the sockets, but this call is extremely fast?
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
			// Again, me might also get this from the sockets, but this seems preferable for now.
  		JToken ticker = await MakeJsonRequestAsync<JToken>("/best_bid_ask?product_ids=" + marketSymbol.ToUpperInvariant());
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
  		JToken token = await MakeJsonRequestAsync<JToken>("/product_book?product_id=" + marketSymbol.ToUpperInvariant() + "&limit=" + maxCount);
  		ExchangeOrderBook orderBook = new ExchangeOrderBook();
  		foreach(JToken bid in token[PRICEBOOK][BIDS]) orderBook.Bids.Add(bid[PRICE].ConvertInvariant<decimal>(), new ExchangeOrderPrice(){ Price = bid[PRICE].ConvertInvariant<decimal>(), Amount = bid[SIZE].ConvertInvariant<decimal>() });
  		foreach(JToken ask in token[PRICEBOOK][ASKS]) orderBook.Asks.Add(ask[PRICE].ConvertInvariant<decimal>(), new ExchangeOrderPrice(){ Price = ask[PRICE].ConvertInvariant<decimal>(), Amount = ask[SIZE].ConvertInvariant<decimal>() });
  		return orderBook;
  	}

  	protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = 100)
  	{
  		// Limit is required but maxed at 100 with no pagination available. Check Sockets?
  		limit = (limit == null || limit < 1 || limit > 100) ? 100 : (int)limit;
  		JToken trades = await MakeJsonRequestAsync<JToken>("/products/" + marketSymbol.ToUpperInvariant() + "/ticker?limit=" + limit);
  		List<ExchangeTrade> tradeList = new List<ExchangeTrade>();
  		foreach (JToken trade in trades["trades"]) tradeList.Add(trade.ParseTrade(SIZE, PRICE, "side", "time", TimestampType.Iso8601UTC, "trade_id"));
  		return tradeList;
  	}

  	protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
  	{
  		// There is no Historical Trades endpoint. The best we can do is get the last 100 trades and filter.
  		// Check for this data on the sockets?
  		var trades = await OnGetRecentTradesAsync(marketSymbol.ToUpperInvariant());

  		if (startDate != null) trades = trades.Where(t => t.Timestamp >= startDate);
  		if (endDate != null) trades = trades.Where(t => t.Timestamp <= endDate);;
  		if (limit != null) trades = trades.Take((int)limit);

  		callback(trades);
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

  		// Returned Candle count is restricted to 300 - and they don't paginate this call
  		// We're going to keep retrieving candles 300 at a time until we get our date range for the granularity
  		if (startDate == null) startDate = CryptoUtility.UtcNow.AddMinutes(-(periodSeconds * 300));
  		if (startDate >= endDate) throw new APIException("Invalid Date Range");
  		DateTime RangeStart = (DateTime)startDate, RangeEnd = (DateTime)endDate;
  		if ((RangeEnd - RangeStart).TotalSeconds / periodSeconds > 300) RangeStart = RangeEnd.AddSeconds(-(periodSeconds * 300));

  		List<MarketCandle> candles = new List<MarketCandle>();
  		while (true) 
  		{ 
  			JToken token = await MakeJsonRequestAsync<JToken>(string.Format("/products/{0}/candles?start={1}&end={2}&granularity={3}", marketSymbol.ToUpperInvariant(), ((DateTimeOffset)RangeStart).ToUnixTimeSeconds(), ((DateTimeOffset)RangeEnd).ToUnixTimeSeconds(), granularity));
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

  		// We can chose between maker and taker fee, but currently ExchangeSharp only supports 1 fee rate per symbol.
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
  			JToken accountWalletAddress = await this.MakeJsonRequestAsync<JToken>($"/accounts/{Accounts[symbol]}/addresses", BaseURLV2);
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

  	/// <summary>
  	/// WARNING: Only Advanced Trade Open Orders are supported. 
  	/// </summary>
  	/// <param name="marketSymbol"></param>
  	/// <returns></returns>
  	protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
  	{
  		List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
  		// Max return count is 1000 with no pagination available
  		JToken array = await MakeJsonRequestAsync<JToken>("/orders/historical/batch?order_status=OPEN" + marketSymbol == null || marketSymbol == string.Empty ? string.Empty : "&product_id=" + marketSymbol );
  		foreach (JToken order in array) if (order["type"].ToStringInvariant().Equals(ADVFILL)) orders.Add(ParseOrder(order));
  		return orders;
  	}

  	/// <summary>
  	/// WARNING: Only Advanced Trade Completed Orders are supported. 
  	/// </summary>
  	/// <param name="marketSymbol"></param>
  	/// <param name="afterDate"></param>
  	/// <returns></returns>
  	protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
  	{
  		// Legacy Orders may be retrieved using V2 (not implemented here - see GetTx in code below)
  		List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
  		pagination = PaginationType.V3Cursor;
  		string startURL = "/orders/historical/fills";
		
  		if (!string.IsNullOrEmpty(marketSymbol)) startURL += "?product_id=" + marketSymbol.ToStringUpperInvariant();
  		if (afterDate != null) startURL += marketSymbol == null ? "?" : "&" + "start_sequence_timestamp=" + ((DateTimeOffset)afterDate).ToUnixTimeSeconds();
  		JToken token = await MakeJsonRequestAsync<JToken>(startURL);
  		startURL +=  marketSymbol == null && afterDate == null ? "?" : "&" + "cursor=";
  		while(true)
  		{ 
  			foreach (JToken fill in token["fills"])
  			{ 
  				orders.Add(new ExchangeOrderResult()
  				{
  					MarketSymbol = fill[PRODUCTID].ToStringInvariant(),
  					TradeId = fill["trade_id"].ToStringInvariant(),
  					OrderId = fill["order_id"].ToStringInvariant(),
  					OrderDate = fill["trade_time"].ToDateTimeInvariant(),
  					IsBuy = fill["side"].ToStringInvariant() == "buy",
  					Amount = fill[SIZE].ConvertInvariant<decimal>(),
  					AmountFilled = fill[SIZE].ConvertInvariant<decimal>(),
  					Price = fill[PRICE].ConvertInvariant<decimal>(),
  					Fees = fill["commission"].ConvertInvariant<decimal>(),
  					AveragePrice = fill[PRICE].ConvertInvariant<decimal>()
  				});
  			}
  			if (string.IsNullOrEmpty(cursorNext)) break;
  			token = await MakeJsonRequestAsync<JToken>(startURL + cursorNext);
  		} 
  		pagination = PaginationType.None;
  		return orders;
  	}

	protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
	{
  		JToken obj = await MakeJsonRequestAsync<JToken>("/orders/historical/" + orderId);
  		return ParseOrder(obj);
  	}

	protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null, bool isClientOrderId = false)
	{
  		Dictionary<string, object> payload = new Dictionary<string, object>() {{ "order_ids", new [] { orderId } }	};
  		await MakeJsonRequestAsync<JArray>("/orders/batch_cancel", payload: payload, requestMethod: "POST");
  	}

/// <summary>
	/// This supports two Entries in the Order ExtraParameters:
	/// "post_only" : true/false (defaults to false if does not exist)
	/// "gtd_timestamp : datetime (determines GTD order type if exists, otherwise GTC
	/// </summary>
	/// <param name="order"></param>
	/// <returns></returns>
	protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
	{
		Dictionary<string, object> configuration = new Dictionary<string, object>();
		switch (order.OrderType)
		{
			case OrderType.Limit:
				if (order.ExtraParameters.ContainsKey("gtd_timestamp"))
				{
					configuration.Add("limit_limit_gtd", new Dictionary<string, object>()
					{
						{"base_size", order.Amount.ToStringInvariant() },
						{"limit_price", order.Price.ToStringInvariant() },
						{"end_time", ((DateTimeOffset)order.ExtraParameters["gtd_timestamp"].ToDateTimeInvariant()).ToUnixTimeSeconds().ToString() },		// This is a bit convoluted? Is this the right format?
						{"post_only", order.ExtraParameters.TryGetValueOrDefault( "post_only", "false") }
					});
				}
				else
				{ 
					configuration.Add("limit_limit_gtc", new Dictionary<string, object>()
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
					configuration.Add("stop_limit_stop_limit_gtc", new Dictionary<string, object>()
					{
						{"base_size", order.Amount.ToStringInvariant() },
						{"limit_price", order.Price.ToStringInvariant() },
						{"stop_price", order.StopPrice.ToStringInvariant() },
						{"post_only", order.ExtraParameters.TryGetValueOrDefault( "post_only", "false") }
						//{"stop_direction", "UNKNOWN_STOP_DIRECTION" }    // set stop direction?
					});
				}
				else
				{
					configuration.Add("stop_limit_stop_limit_gtd", new Dictionary<string, object>()
					{
						{"base_size", order.Amount.ToStringInvariant() },
						{"limit_price", order.Price.ToStringInvariant() },
						{"stop_price", order.StopPrice.ToStringInvariant() },
						{"end_time", ((DateTimeOffset)order.ExtraParameters["gtd_timestamp"].ToDateTimeInvariant()).ToUnixTimeSeconds().ToString() },		// This is a bit convoluted? Is this the right format?
						{"post_only", order.ExtraParameters.TryGetValueOrDefault( "post_only", "false") }
						//{"stop_direction", "UNKNOWN_STOP_DIRECTION" }    // set stop direction?
					});
				}
				break;
			case OrderType.Market:
				configuration.Add("market_market_ioc", new Dictionary<string, object>()
				{
					{"base_size", order.Amount.ToStringInvariant() }
				});
				break;
		}

		Dictionary<string, object> payload = new Dictionary<string, object>	{	{ "order_configuration", configuration}	};
		string side = order.IsBuy ? "buy" : "sell";
		JToken result = await MakeJsonRequestAsync<JToken>($"/orders?product_id={order.MarketSymbol.ToUpperInvariant()}&side={side}", payload: payload, requestMethod: "POST");

		// We don't have the proper return type for the POST - will probably require a separate parsing function and return Success/Fail
		return ParseOrder(result);
	}
  	protected override Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
  	{
  		return base.OnWithdrawAsync(withdrawalRequest);
  	}


  	#endregion

  	#region SocketEndpoints

  	protected override Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 100, params string[] marketSymbols)
  	{
			return base.OnGetDeltaOrderBookWebSocketAsync(callback);
  	}

  	protected override Task<IWebSocket> OnGetTickersWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
  	{
			return base.OnGetTickersWebSocketAsync(callback, marketSymbols);
  	}

		protected override Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
		{
			return base.OnGetTradesWebSocketAsync(callback, marketSymbols);
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
  			if (cursorNext == null) break;
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
  		JToken tokens = await MakeJsonRequestAsync<JToken>($"accounts/{Accounts[currency]}/transactions", BaseURLV2);
  		while(true)
  		{ 
  			foreach (JToken token in tokens)
  			{
  				// A "send" to Coinbase is when someone "sent" you coin - or a receive to the rest of the world
  				// Likewise, a "receive" is when someone "received" coin from you. In other words, it's back-asswards.
  				if (!Withdrawals && token["type"].ToStringInvariant().Equals("send")) transfers.Add(ParseTransaction(token));
  				else if (Withdrawals && token["type"].ToStringInvariant().Equals("receive")) transfers.Add(ParseTransaction(token));

  				// Legacy Order and other Coinbase Tx Types can be parsed using this V2 code block
  				//var tmp = ParseOrder(token);
  			}
  			if (string.IsNullOrEmpty(cursorNext)) break;
  			tokens = await MakeJsonRequestAsync<JToken>($"accounts/{Accounts[currency]}/transactions?starting_after={cursorNext}", BaseURLV2);
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
  			Status = token["status"].ToStringInvariant() == "completed" ? TransactionStatus.Complete : TransactionStatus.Unknown,
  			Notes = token["description"].ToStringInvariant()
  			// Address 
  			// AddressTag 
  			// TxFee 
  		};
  	}


  	/// <summary>
  	/// Parse both Advanced Trade and Legacy Transactions
  	/// </summary>
  	/// <param name="result"></param>
  	/// <returns></returns>
  	private ExchangeOrderResult ParseOrder(JToken result)
  	{
  		decimal amount = 0, amountFilled = 0, price = 0, fees = 0;
  		string marketSymbol = string.Empty;
  		bool isBuy = true;

  		//Debug.WriteLine(result["type"].ToStringInvariant());
  		switch(result["type"].ToStringInvariant())
  		{
  			case ADVFILL:
  				// Buys/Sells have reversed amounts?


  				break;
  			case "send":
  			case "receive":
  				return new ExchangeOrderResult {OrderId = result["id"].ToStringInvariant(), Message = result["type"].ToStringInvariant(), };
  			case "buy":
  			case "sell":
  			case "trade":
  			case "request":
  			case "transfer":

  			case "exchange_deposit":
  			case "fiat_deposit":
  			case "fiat_withdrawal":
  			case "pro_withdrawal":
  			case "vault_withdrawal":
  			default:
  				return new ExchangeOrderResult {OrderId = result["id"].ToStringInvariant(), Message = result["type"].ToStringInvariant(), };
  		}

  		amount = result[AMOUNT][AMOUNT].ConvertInvariant<decimal>(amountFilled);
  		amountFilled = amount;

  		price = result[ADVFILL]["fill_price"].ConvertInvariant<decimal>();
  		fees = result[ADVFILL]["commission"].ConvertInvariant<decimal>();
  		marketSymbol = result[ADVFILL][PRODUCTID].ToStringInvariant(result["id"].ToStringInvariant());
  		isBuy = (result[ADVFILL]["order_side"].ToStringInvariant() == "buy");

  		ExchangeOrderResult order = new ExchangeOrderResult()
  		{
  			IsBuy = isBuy,
  			Amount = amount,
  			AmountFilled = amountFilled,
  			Price = price,
  			Fees = fees,
  			FeesCurrency = result["native_amount"]["currency"].ToStringInvariant(),
  			OrderDate = result["created_at"].ToDateTimeInvariant(),
  			CompletedDate = result["updated_at"].ToDateTimeInvariant(),
  			MarketSymbol = marketSymbol,
  			OrderId = result["id"].ToStringInvariant(),
  			Message = result["type"].ToStringInvariant()
  		};

  		switch (result["status"].ToStringInvariant())
  		{
  			case "completed":
  				order.Result = ExchangeAPIOrderResult.Filled;
  				break;
  			case "waiting_for_clearing":
  			case "waiting_for_signature":
  			case "pending":
  				order.Result = ExchangeAPIOrderResult.PendingOpen;
  				break;
  			case "expired":
  			case "canceled":
  				order.Result = ExchangeAPIOrderResult.Canceled;
  				break;
  			default:
  				order.Result = ExchangeAPIOrderResult.Unknown;
  				break;
  		}
  		return order;
  	}

  	#endregion

  }

  public partial class ExchangeName { public const string Coinbase = "Coinbase"; }
}
