/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Runtime.InteropServices;

namespace ExchangeSharp
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public sealed partial class ExchangeBitfinexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.bitfinex.com/v2";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.bitfinex.com/ws";

        public Dictionary<string, string> DepositMethodLookup { get; }

        public string BaseUrlV1 { get; set; } = "https://api.bitfinex.com/v1";

        public ExchangeBitfinexAPI()
        {
            NonceStyle = NonceStyle.UnixMillisecondsString;
            RateLimit = new RateGate(1, TimeSpan.FromSeconds(6.0));

            // List is from "Withdrawal Types" section https://docs.bitfinex.com/v1/reference#rest-auth-withdrawal
            DepositMethodLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AVT"] = "aventus",
                ["BCH"] = "bcash",
                ["BTC"] = "bitcoin",
                ["BTG"] = "bgold",
                ["DASH"] = "dash", // TODO: Bitfinex returns "DSH" as the symbol name in the API but on the site it is "DASH". How to normalize?
                ["EDO"] = "eidoo",
                ["ETC"] = "ethereumc",
                ["ETH"] = "ethereum",
                ["GNT"] = "golem",
                ["LTC"] = "litecoin",
                ["MIOTA"] = "iota",
                ["OMG"] = "omisego",
                ["SAN"] = "santiment",
                ["SNT"] = "status",
                ["XMR"] = "monero",
                ["XRP"] = "ripple",
                ["YYW"] = "yoyow",
                ["ZEC"] = "zcash",
            };

            MarketSymbolSeparator = string.Empty;
            MarketSymbolIsUppercase = false;
        }

        public string NormalizeMarketSymbolV1(string marketSymbol)
        {
            return (marketSymbol ?? string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        public async Task<IEnumerable<ExchangeOrderResult>> GetOrderDetailsInternalV2(string url, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["limit"] = 250;
            payload["start"] = CryptoUtility.UtcNow.Subtract(TimeSpan.FromDays(365.0)).UnixTimestampFromDateTimeMilliseconds();
            payload["end"] = CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds();
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, payload);
            Dictionary<string, List<JToken>> trades = new Dictionary<string, List<JToken>>(StringComparer.OrdinalIgnoreCase);
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    if (marketSymbol == null || token[1].ToStringInvariant() == "t" + marketSymbol.ToUpperInvariant())
                    {
                        string lookup = token[1].ToStringInvariant().Substring(1).ToLowerInvariant();
                        if (!trades.TryGetValue(lookup, out List<JToken> tradeList))
                        {
                            tradeList = trades[lookup] = new List<JToken>();
                        }
                        tradeList.Add(token);
                    }
                }
            }
            return ParseOrderV2(trades);
        }

        public override string PeriodSecondsToString(int seconds)
        {
            return base.PeriodSecondsToString(seconds).Replace("d", "D"); // WTF Bitfinex, capital D???
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            var markets = new List<ExchangeMarket>();
            JToken allPairs = await MakeJsonRequestAsync<JToken>("/symbols_details", BaseUrlV1);
            Match m;
            foreach (JToken pair in allPairs)
            {
                var market = new ExchangeMarket
                {
                    IsActive = true,
                    MarketSymbol = pair["pair"].ToStringInvariant(),
                    MinTradeSize = pair["minimum_order_size"].ConvertInvariant<decimal>(),
                    MaxTradeSize = pair["maximum_order_size"].ConvertInvariant<decimal>(),
                    MarginEnabled = pair["margin"].ConvertInvariant<bool>(false)
                };
                var pairPropertyVal = pair["pair"].ToStringUpperInvariant();
                m = Regex.Match(pairPropertyVal, "^(BTC|USD|ETH|GBP|JPY|EUR|EOS)");
                if (m.Success)
                {
                    market.BaseCurrency = m.Value;
                    market.QuoteCurrency = pairPropertyVal.Substring(m.Length);
                }
                else
                {
                    m = Regex.Match(pairPropertyVal, "(BTC|USD|ETH|GBP|JPY|EUR|EOS)$");
                    if (m.Success)
                    {
                        market.BaseCurrency = pairPropertyVal.Substring(0, m.Index);
                        market.QuoteCurrency = m.Value;
                    }
                    else
                    {
                        // TODO: Figure out a nicer way to handle newly added pairs
                        market.BaseCurrency = pairPropertyVal.Substring(0, 3);
                        market.QuoteCurrency = pairPropertyVal.Substring(3);
                    }
                }
                int pricePrecision = pair["price_precision"].ConvertInvariant<int>();
                market.PriceStepSize = (decimal)Math.Pow(0.1, pricePrecision);
                markets.Add(market);
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken ticker = await MakeJsonRequestAsync<JToken>("/ticker/t" + marketSymbol);
            return this.ParseTicker(ticker, marketSymbol, 2, 0, 6, 7);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            IReadOnlyDictionary<string, ExchangeMarket> marketsBySymbol = (await GetMarketSymbolsMetadataAsync()).ToDictionary(market => market.MarketSymbol, market => market);
            if (marketsBySymbol != null && marketsBySymbol.Count != 0)
            {
                StringBuilder symbolString = new StringBuilder();
                foreach (var marketSymbol in marketsBySymbol.Keys)
                {
                    symbolString.Append('t');
                    symbolString.Append(marketSymbol.ToUpperInvariant());
                    symbolString.Append(',');
                }
                symbolString.Length--;
                JToken token = await MakeJsonRequestAsync<JToken>("/tickers?symbols=" + symbolString);
                DateTime now = CryptoUtility.UtcNow;
                foreach (JArray array in token)
                {
                    #region Return Values
                    //[
                    //  SYMBOL,
                    //  BID,                float	Price of last highest bid
                    //  BID_SIZE,           float	Sum of the 25 highest bid sizes
                    //  ASK,                float	Price of last lowest ask
                    //  ASK_SIZE,           float	Sum of the 25 lowest ask sizes
                    //  DAILY_CHANGE,       float	Amount that the last price has changed since yesterday
                    //  DAILY_CHANGE_PERC,  float	Amount that the price has changed expressed in percentage terms
                    //  LAST_PRICE,         float	Price of the last trade
                    //  VOLUME,             float	Daily volume
                    //  HIGH,               float	Daily high
                    //  LOW                 float	Daily low
                    //]
                    #endregion
                    var marketSymbol = array[0].ToStringInvariant().Substring(1);
                    var market = marketsBySymbol[marketSymbol.ToLowerInvariant()];
                    tickers.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, new ExchangeTicker
                    {
                        MarketSymbol = marketSymbol,
                        Ask = array[3].ConvertInvariant<decimal>(),
                        Bid = array[1].ConvertInvariant<decimal>(),
                        Last = array[7].ConvertInvariant<decimal>(),
                        Volume = new ExchangeVolume
                        {
                            QuoteCurrencyVolume = array[8].ConvertInvariant<decimal>() * array[7].ConvertInvariant<decimal>(),
                            QuoteCurrency = market.QuoteCurrency,
                            BaseCurrencyVolume = array[8].ConvertInvariant<decimal>(),
                            BaseCurrency = market.BaseCurrency,
                            Timestamp = now
                        }
                    }));
                }
            }
            return tickers;
        }

        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback, params string[] marketSymbols)
        {
            Dictionary<int, string> channelIdToSymbol = new Dictionary<int, string>();
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token is JArray array)
                {
                    if (array.Count > 10)
                    {
                        List<KeyValuePair<string, ExchangeTicker>> tickerList = new List<KeyValuePair<string, ExchangeTicker>>();
                        if (channelIdToSymbol.TryGetValue(array[0].ConvertInvariant<int>(), out string symbol))
                        {
                            ExchangeTicker ticker = ParseTickerWebSocket(symbol, array);
                            if (ticker != null)
                            {
                                callback(new KeyValuePair<string, ExchangeTicker>[] { new KeyValuePair<string, ExchangeTicker>(symbol, ticker) });
                            }
                        }
                    }
                }
                else if (token["event"].ToStringInvariant() == "subscribed" && token["channel"].ToStringInvariant() == "ticker")
                {
                    // {"event":"subscribed","channel":"ticker","chanId":1,"pair":"BTCUSD"}
                    int channelId = token["chanId"].ConvertInvariant<int>();
                    channelIdToSymbol[channelId] = token["pair"].ToStringInvariant();
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                marketSymbols = marketSymbols == null || marketSymbols.Length == 0 ? (await GetMarketSymbolsAsync()).ToArray() : marketSymbols;
                foreach (var marketSymbol in marketSymbols)
                {
                    await _socket.SendMessageAsync(new { @event = "subscribe", channel = "ticker", pair = marketSymbol });
                }
            });
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
        {
            Dictionary<int, string> channelIdToSymbol = new Dictionary<int, string>();
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
			}
            return ConnectWebSocket("/2", (_socket, msg) => //use websocket V2 (beta, but millisecond timestamp)
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token is JArray array)
                {
                    if (token[1].ToStringInvariant() == "hb")
                    {
                        // heartbeat
                    }
                    else if (token.Last.Last.HasValues == false)
                    {
                        //[29654, "tu", [270343572, 1532012917722, -0.003, 7465.636738]] "te"=temp/intention to execute "tu"=confirmed and ID is definitive
                        //chan id, -- , [ID       , timestamp    , amount, price      ]]
                        if (channelIdToSymbol.TryGetValue(array[0].ConvertInvariant<int>(), out string symbol))
                        {
                            if (token[1].ToStringInvariant() == "tu")
                            {
                                ExchangeTrade trade = ParseTradeWebSocket(token.Last);
                                if (trade != null)
                                {
                                    callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
                                }
                            }
                        }
                    }
                    else
                    {
						//parse snapshot here if needed
						if (channelIdToSymbol.TryGetValue(array[0].ConvertInvariant<int>(), out string symbol))
						{
							if (array[1] is JArray subarray)
							{
								for (int i = 0; i < subarray.Count - 1; i++)
								{
									ExchangeTrade trade = ParseTradeWebSocket(subarray[i]);
									if (trade != null)
									{
										trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
										if (i == subarray.Count - 1)
										{
											trade.Flags |= ExchangeTradeFlags.IsLastFromSnapshot;
										}
										callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
									}
								}
							}
						}
					}
				}
                else if (token["event"].ToStringInvariant() == "subscribed" && token["channel"].ToStringInvariant() == "trades")
                {
                    //{"event": "subscribed","channel": "trades","chanId": 29654,"symbol": "tBTCUSD","pair": "BTCUSD"}
                    int channelId = token["chanId"].ConvertInvariant<int>();
                    channelIdToSymbol[channelId] = token["pair"].ToStringInvariant();
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
				foreach (var marketSymbol in marketSymbols)
                {
                    await _socket.SendMessageAsync(new { @event = "subscribe", channel = "trades", symbol = marketSymbol });
                }
            });
        }

        private ExchangeTrade ParseTradeWebSocket(JToken token)
        {
            decimal amount = token[2].ConvertInvariant<decimal>();
            return new ExchangeTrade
            {
                Id = token[0].ConvertInvariant<int>(),
                Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(token[1].ConvertInvariant<long>()),
                Amount = Math.Abs(amount),
                IsBuy = amount > 0,
                Price = token[3].ConvertInvariant<decimal>()
            };
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            ExchangeOrderBook orders = new ExchangeOrderBook();
            decimal[][] books = await MakeJsonRequestAsync<decimal[][]>("/book/t" + marketSymbol +
	        "/P0?limit_bids=" + maxCount.ToStringInvariant() + "limit_asks=" + maxCount.ToStringInvariant());
            foreach (decimal[] book in books)
            {
                if (book[2] > 0m)
                {
                    orders.Bids[book[0]] = new ExchangeOrderPrice { Amount = book[2], Price = book[0] };
                }
                else
                {
                    orders.Asks[book[0]] = new ExchangeOrderPrice { Amount = -book[2], Price = book[0] };
                }
            }
            return orders;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            const int maxCount = 100;
            string baseUrl = "/trades/t" + marketSymbol + "/hist?sort=" + (startDate == null ? "-1" : "1") + "&limit=" + maxCount;
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            decimal[][] tradeChunk;
            while (true)
            {
                url = baseUrl;
                if (startDate != null)
                {
                    url += "&start=" + (long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(startDate.Value);
                }
                tradeChunk = await MakeJsonRequestAsync<decimal[][]>(url);
                if (tradeChunk == null || tradeChunk.Length == 0)
                {
                    break;
                }
                if (startDate != null)
                {
                    startDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((double)tradeChunk[tradeChunk.Length - 1][1]);
                }
                foreach (decimal[] tradeChunkPiece in tradeChunk)
                {
                    trades.Add(new ExchangeTrade { Amount = Math.Abs(tradeChunkPiece[2]), IsBuy = tradeChunkPiece[2] > 0m, Price = tradeChunkPiece[3], Timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds((double)tradeChunkPiece[1]), Id = (long)tradeChunkPiece[0] });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                if (!callback(trades))
                {
                    break;
                }
                trades.Clear();
                if (tradeChunk.Length < maxCount || startDate == null)
                {
                    break;
                }
                await Task.Delay(5000);
            }
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            // https://api.bitfinex.com/v2/candles/trade:1d:btcusd/hist?start=ms_start&end=ms_end
            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = PeriodSecondsToString(periodSeconds);
            string url = "/candles/trade:" + periodString + ":t" + marketSymbol + "/hist?sort=1";
            if (startDate != null || endDate != null)
            {
                endDate = endDate ?? CryptoUtility.UtcNow;
                startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));
                url += "&start=" + ((long)startDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
                url += "&end=" + ((long)endDate.Value.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
            }
            if (limit != null)
            {
                url += "&limit=" + (limit.Value.ToStringInvariant());
            }
            JToken token = await MakeJsonRequestAsync<JToken>(url);

            /* MTS, OPEN, CLOSE, HIGH, LOW, VOL */
            foreach (JToken candle in token)
            {
                candles.Add(this.ParseCandle(candle, marketSymbol, periodSeconds, 1, 3, 4, 2, 0, TimestampType.UnixMilliseconds, 5));
            }

            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            return await OnGetAmountsAsync("exchange");
        }
        
        public async Task<Dictionary<string, decimal>> OnGetAmountsAsync(string type)
        {
            Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray obj = await MakeJsonRequestAsync<JArray>("/balances", BaseUrlV1, await GetNoncePayloadAsync());
            foreach (JToken token in obj)
            {
                if (token["type"].ToStringInvariant() == type)
                {
                    decimal amount = token["amount"].ConvertInvariant<decimal>();
                    if (amount > 0m)
                    {
                        lookup[token["currency"].ToStringInvariant()] = amount;
                    }
                }
            }
            return lookup;
        }
        
        protected override async Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync(
            bool includeZeroBalances = false)
        {
            return await OnGetAmountsAsync("trading");
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            JArray obj = await MakeJsonRequestAsync<JArray>("/balances", BaseUrlV1, await GetNoncePayloadAsync());
            foreach (JToken token in obj)
            {
                if (token["type"].ToStringInvariant() == "exchange")
                {
                    decimal amount = token["available"].ConvertInvariant<decimal>();
                    if (amount > 0m)
                    {
                        lookup[token["currency"].ToStringInvariant()] = amount;
                    }
                }
            }
            return lookup;
        }
        
        

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            string marketSymbol = NormalizeMarketSymbolV1(order.MarketSymbol);
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["symbol"] = marketSymbol;
            payload["amount"] = (await ClampOrderQuantity(marketSymbol, order.Amount)).ToStringInvariant();
            payload["side"] = (order.IsBuy ? "buy" : "sell");

            if (order.IsMargin)
            {
                payload["type"] = order.OrderType == OrderType.Market ? "market" : "limit";
            }
            else
            {
                payload["type"] = order.OrderType == OrderType.Market ? "exchange market" : "exchange limit";
            }

            if (order.OrderType != OrderType.Market)
            {
                payload["price"] = (await ClampOrderPrice(marketSymbol, order.Price)).ToStringInvariant();
            }
            else
            {
                payload["price"] = "1";
            }
            order.ExtraParameters.CopyTo(payload);
            JToken obj = await MakeJsonRequestAsync<JToken>("/order/new", BaseUrlV1, payload);
            return ParseOrder(obj);
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["order_id"] = orderId.ConvertInvariant<long>();
            JToken result = await MakeJsonRequestAsync<JToken>("/order/status", BaseUrlV1, payload);
            return ParseOrder(result);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            return await GetOrderDetailsInternalAsync("/orders", marketSymbol);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            if (string.IsNullOrWhiteSpace(marketSymbol))
            {
                // HACK: Bitfinex does not provide a way to get all historical order details beyond a few days in one call, so we have to
                //  get the historical details one by one for each symbol.
                var symbols = (await GetMarketSymbolsAsync()).Where(s => s.IndexOf("usd", StringComparison.OrdinalIgnoreCase) < 0 && s.IndexOf("btc", StringComparison.OrdinalIgnoreCase) >= 0);
                return await GetOrderDetailsInternalV1(symbols, afterDate);
            }

            // retrieve orders for the one symbol
            return await GetOrderDetailsInternalV1(new string[] { marketSymbol }, afterDate);
        }

        protected override IWebSocket OnGetCompletedOrderDetailsWebSocket(Action<ExchangeOrderResult> callback)
        {
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token is JArray array && array.Count > 1 && array[2] is JArray && array[1].ToStringInvariant() == "os")
                {
                    foreach (JToken orderToken in array[2])
                    {
                        callback.Invoke(ParseOrderWebSocket(orderToken));
                    }
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                object nonce = await GenerateNonceAsync();
                string authPayload = "AUTH" + nonce;
                string signature = CryptoUtility.SHA384Sign(authPayload, PrivateApiKey.ToUnsecureString());
                Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "apiKey", PublicApiKey.ToUnsecureString() },
                    { "event", "auth" },
                    { "authPayload", authPayload },
                    { "authSig", signature }
                };
                string payloadJSON = CryptoUtility.GetJsonForPayload(payload);
                await _socket.SendMessageAsync(payloadJSON);
            });
        }
 
        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["order_id"] = orderId.ConvertInvariant<long>();
           var token= await MakeJsonRequestAsync<JToken>("/order/cancel", BaseUrlV1, payload);
        }

        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            if (currency.Length == 0)
            {
                throw new ArgumentNullException(nameof(currency));
            }

            // IOTA addresses should never be used more than once
            if (currency.Equals("MIOTA", StringComparison.OrdinalIgnoreCase))
            {
                forceRegenerate = true;
            }

            // symbol needs to be translated to full name of coin: bitcoin/litecoin/ethereum
            if (!DepositMethodLookup.TryGetValue(currency, out string fullName))
            {
                fullName = currency.ToLowerInvariant();
            }

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["method"] = fullName;
            payload["wallet_name"] = "exchange";
            payload["renew"] = forceRegenerate ? 1 : 0;

            JToken result = await MakeJsonRequestAsync<JToken>("/deposit/new", BaseUrlV1, payload, "POST");
            var details = new ExchangeDepositDetails
            {
                Currency = result["currency"].ToStringInvariant(),
            };
            if (result["address_pool"] != null)
            {
                details.Address = result["address_pool"].ToStringInvariant();
                details.AddressTag = result["address"].ToStringLowerInvariant();
            }
            else
            {
                details.Address = result["address"].ToStringInvariant();
            }

            return details;
        }

        /// <summary>Gets the deposit history for a symbol</summary>
        /// <param name="currency">The symbol to check. Must be specified.</param>
        /// <returns>Collection of ExchangeCoinTransfers</returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
        {
            if (currency.Length == 0)
            {
                throw new ArgumentNullException(nameof(currency));
            }

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["currency"] = currency;

            JToken result = await MakeJsonRequestAsync<JToken>("/history/movements", BaseUrlV1, payload, "POST");
            var transactions = new List<ExchangeTransaction>();
            foreach (JToken token in result)
            {
                if (!string.Equals(token["type"].ToStringUpperInvariant(), "DEPOSIT"))
                {
                    continue;
                }

                var transaction = new ExchangeTransaction
                {
                    PaymentId = token["id"].ToStringInvariant(),
                    BlockchainTxId = token["txid"].ToStringInvariant(),
                    Currency = token["currency"].ToStringUpperInvariant(),
                    Notes = token["description"].ToStringInvariant() + ", method: " + token["method"].ToStringInvariant(),
                    Amount = token["amount"].ConvertInvariant<decimal>(),
                    Address = token["address"].ToStringInvariant()
                };

                string status = token["status"].ToStringUpperInvariant();
                switch (status)
                {
                    case "COMPLETED":
                        transaction.Status = TransactionStatus.Complete;
                        break;
                    case "UNCONFIRMED":
                        transaction.Status = TransactionStatus.Processing;
                        break;
                    default:
                        transaction.Status = TransactionStatus.Unknown;
                        transaction.Notes += ", Unknown transaction status " + status;
                        break;
                }

                double unixTimestamp = token["timestamp"].ConvertInvariant<double>();
                transaction.Timestamp = unixTimestamp.UnixTimeStampToDateTimeSeconds();
                transaction.TxFee = token["fee"].ConvertInvariant<decimal>();

                transactions.Add(transaction);
            }

            return transactions;
        }

        /// <summary>A withdrawal request.</summary>
        /// <param name="withdrawalRequest">The withdrawal request.
        /// NOTE: Network fee must be subtracted from amount or withdrawal will fail</param>
        /// <returns>The withdrawal response</returns>
        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            // symbol needs to be translated to full name of coin: bitcoin/litecoin/ethereum
            if (!DepositMethodLookup.TryGetValue(withdrawalRequest.Currency, out string fullName))
            {
                fullName = withdrawalRequest.Currency.ToLowerInvariant();
            }

            // Bitfinex adds the fee on top of what you request to withdrawal
            if (withdrawalRequest.TakeFeeFromAmount)
            {
                Dictionary<string, decimal> fees = await GetWithdrawalFeesAsync();
                if (fees.TryGetValue(withdrawalRequest.Currency, out decimal feeAmt))
                {
                    withdrawalRequest.Amount -= feeAmt;
                }
            }

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["withdraw_type"] = fullName;
            payload["walletselected"] = "exchange";
            payload["amount"] = withdrawalRequest.Amount.ToString(CultureInfo.InvariantCulture); // API throws if this is a number not a string
            payload["address"] = withdrawalRequest.Address;

            if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
            {
                payload["payment_id"] = withdrawalRequest.AddressTag;
            }

            if (!string.IsNullOrWhiteSpace(withdrawalRequest.Description))
            {
                payload["account_name"] = withdrawalRequest.Description;
            }

            JToken result = await MakeJsonRequestAsync<JToken>("/withdraw", BaseUrlV1, payload, "POST");

            var resp = new ExchangeWithdrawalResponse();
            if (!string.Equals(result[0]["status"].ToStringInvariant(), "success", StringComparison.OrdinalIgnoreCase))
            {
                resp.Success = false;
            }

            resp.Id = result[0]["withdrawal_id"].ToStringInvariant();
            resp.Message = result[0]["message"].ToStringInvariant();
            return resp;
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.Method = "POST";
                request.AddHeader("content-type", "application/json");
                request.AddHeader("accept", "application/json");
                if (request.RequestUri.AbsolutePath.StartsWith("/v2"))
                {
                    string nonce = payload["nonce"].ToStringInvariant();
                    payload.Remove("nonce");
                    string json = JsonConvert.SerializeObject(payload);
                    string toSign = "/api" + request.RequestUri.PathAndQuery + nonce + json;
                    string hexSha384 = CryptoUtility.SHA384Sign(toSign, PrivateApiKey.ToUnsecureString());
                    request.AddHeader("bfx-nonce", nonce);
                    request.AddHeader("bfx-apikey", PublicApiKey.ToUnsecureString());
                    request.AddHeader("bfx-signature", hexSha384);
                    await CryptoUtility.WriteToRequestAsync(request, json);
                }
                else
                {
                    // bitfinex v1 doesn't put the payload in the post body it puts it in as a http header, so no need to write to request stream
                    payload.Add("request", request.RequestUri.AbsolutePath);
                    string json = JsonConvert.SerializeObject(payload);
                    string json64 = System.Convert.ToBase64String(json.ToBytesUTF8());
                    string hexSha384 = CryptoUtility.SHA384Sign(json64, PrivateApiKey.ToUnsecureString());
                    request.AddHeader("X-BFX-PAYLOAD", json64);
                    request.AddHeader("X-BFX-SIGNATURE", hexSha384);
                    request.AddHeader("X-BFX-APIKEY", PublicApiKey.ToUnsecureString());
                }
            }
        }

        protected override Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            throw new NotSupportedException("Bitfinex does not provide data about its currencies via the API");
        }

        private async Task<IEnumerable<ExchangeOrderResult>> GetOrderDetailsInternalAsync(string url, string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            marketSymbol = NormalizeMarketSymbolV1(marketSymbol);
            JToken result = await MakeJsonRequestAsync<JToken>(url, BaseUrlV1, await GetNoncePayloadAsync());
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    if (marketSymbol == null || token["symbol"].ToStringInvariant() == marketSymbol)
                    {
                        orders.Add(ParseOrder(token));
                    }
                }
            }
            return orders;
        }

        private async Task<IEnumerable<ExchangeOrderResult>> GetOrderDetailsInternalV1(IEnumerable<string> marketSymbols, DateTime? afterDate)
        {
            Dictionary<string, ExchangeOrderResult> orders = new Dictionary<string, ExchangeOrderResult>(StringComparer.OrdinalIgnoreCase);
            foreach (string marketSymbol in marketSymbols)
            {
                string normalizedSymbol = NormalizeMarketSymbol(marketSymbol);
                Dictionary<string, object> payload = await GetNoncePayloadAsync();
                payload["symbol"] = normalizedSymbol;
                payload["limit_trades"] = 250;
                if (afterDate != null)
                {
                    payload["timestamp"] = afterDate.Value.UnixTimestampFromDateTimeSeconds().ToStringInvariant();
                    payload["until"] = CryptoUtility.UtcNow.UnixTimestampFromDateTimeSeconds().ToStringInvariant();
                }
                JToken token = await MakeJsonRequestAsync<JToken>("/mytrades", BaseUrlV1, payload);
                foreach (JToken trade in token)
                {
                    ExchangeOrderResult subOrder = ParseTrade(trade, normalizedSymbol);
                    lock (orders)
                    {
                        if (orders.TryGetValue(subOrder.OrderId, out ExchangeOrderResult baseOrder))
                        {
                            baseOrder.AppendOrderWithOrder(subOrder);
                        }
                        else
                        {
                            orders[subOrder.OrderId] = subOrder;
                        }
                    }
                }
            }
            return orders.Values.OrderByDescending(o => o.OrderDate);
        }

        private ExchangeOrderResult ParseOrder(JToken order)
        {
            decimal amount = order["original_amount"].ConvertInvariant<decimal>();
            decimal amountFilled = order["executed_amount"].ConvertInvariant<decimal>();
            decimal price = order["price"].ConvertInvariant<decimal>();
            return new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amountFilled,
                Price = price,
                AveragePrice = order["avg_execution_price"].ConvertInvariant<decimal>(order["price"].ConvertInvariant<decimal>()),
                Message = string.Empty,
                OrderId = order["id"].ToStringInvariant(),
                Result = (amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially)),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(order["timestamp"].ConvertInvariant<double>()),
                MarketSymbol = order["symbol"].ToStringInvariant(),
                IsBuy = order["side"].ToStringInvariant() == "buy"
            };
        }

        private ExchangeOrderResult ParseOrderWebSocket(JToken order)
        {
            /*
            [ 0, "os", [ [
                "<ORD_ID>",
                "<ORD_PAIR>",
                "<ORD_AMOUNT>",
                "<ORD_AMOUNT_ORIG>",
                "<ORD_TYPE>",
                "<ORD_STATUS>",
                "<ORD_PRICE>",
                "<ORD_PRICE_AVG>",
                "<ORD_CREATED_AT>",
                "<ORD_NOTIFY>",
                 "<ORD_HIDDEN>",
                "<ORD_OCO>"
            ] ] ];
            */

            decimal amount = order[2].ConvertInvariant<decimal>();
            return new ExchangeOrderResult
            {
                Amount = amount,
                AmountFilled = amount,
                Price = order[6].ConvertInvariant<decimal>(),
                AveragePrice = order[7].ConvertInvariant<decimal>(),
                IsBuy = (amount > 0m),
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(order[8].ConvertInvariant<long>()),
                OrderId = order[0].ToStringInvariant(),
                Result = ExchangeAPIOrderResult.Filled,
                MarketSymbol = order[1].ToStringInvariant()
            };
        }

        private IEnumerable<ExchangeOrderResult> ParseOrderV2(Dictionary<string, List<JToken>> trades)
        {

            /*
            [
            ID	integer	Trade database id
            PAIR	string	Pair (BTCUSD, …)
            MTS_CREATE	integer	Execution timestamp
            ORDER_ID	integer	Order id
            EXEC_AMOUNT	float	Positive means buy, negative means sell
            EXEC_PRICE	float	Execution price
            ORDER_TYPE	string	Order type
            ORDER_PRICE	float	Order price
            MAKER	int	1 if true, 0 if false
            FEE	float	Fee
            FEE_CURRENCY	string	Fee currency
            ],
            */

            foreach (var kv in trades)
            {
                ExchangeOrderResult order = new ExchangeOrderResult { Result = ExchangeAPIOrderResult.Filled };
                foreach (JToken trade in kv.Value)
                {
                    ExchangeOrderResult append = new ExchangeOrderResult { MarketSymbol = kv.Key, OrderId = trade[3].ToStringInvariant() };
                    append.Amount = append.AmountFilled = Math.Abs(trade[4].ConvertInvariant<decimal>());
                    append.Price = trade[7].ConvertInvariant<decimal>();
                    append.AveragePrice = trade[5].ConvertInvariant<decimal>();
                    append.IsBuy = trade[4].ConvertInvariant<decimal>() >= 0m;
                    append.OrderDate = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(trade[2].ConvertInvariant<long>());
                    append.OrderId = trade[3].ToStringInvariant();
                    order.AppendOrderWithOrder(append);
                }
                yield return order;
            }
        }

        private ExchangeOrderResult ParseTrade(JToken trade, string symbol)
        {
            /*
            [{
              "price":"246.94",
              "amount":"1.0",
              "timestamp":"1444141857.0",
              "exchange":"",
              "type":"Buy",
              "fee_currency":"USD",
              "fee_amount":"-0.49388",
              "tid":11970839,
              "order_id":446913929
            }]
            */
            return new ExchangeOrderResult
            {
                Amount = trade["amount"].ConvertInvariant<decimal>(),
                AmountFilled = trade["amount"].ConvertInvariant<decimal>(),
                AveragePrice = trade["price"].ConvertInvariant<decimal>(),
                IsBuy = trade["type"].ToStringUpperInvariant() == "BUY",
                OrderDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(trade["timestamp"].ConvertInvariant<double>()),
                OrderId = trade["order_id"].ToStringInvariant(),
                Result = ExchangeAPIOrderResult.Filled,
                MarketSymbol = symbol
            };
        }

        private ExchangeTicker ParseTickerWebSocket(string symbol, JToken token)
        {
            return this.ParseTicker(token, symbol, 3, 1, 7, 8);
        }

        /// <summary>Gets the withdrawal fees for various currencies.</summary>
        /// <returns>A dictionary of symbol-fee pairs</returns>
        private async Task<Dictionary<string, decimal>> GetWithdrawalFeesAsync()
        {
            JToken obj = await MakeJsonRequestAsync<JToken>("/account_fees", BaseUrlV1, await GetNoncePayloadAsync());
            var fees = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var jToken in obj["withdraw"])
            {
                var prop = (JProperty)jToken;
                fees[prop.Name] = prop.Value.ConvertInvariant<decimal>();
            }

            return fees;
        }
    }

    public partial class ExchangeName { public const string Bitfinex = "Bitfinex"; }
}
