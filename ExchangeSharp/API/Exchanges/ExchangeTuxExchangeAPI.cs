﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public sealed class ExchangeTuxExchangeAPI : ExchangeAPI
    {
        public override string Name => ExchangeName.TuxExchange;
        public override string BaseUrl { get; set; } = "https://tuxexchange.com";

        public ExchangeTuxExchangeAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMillisecondsString;
        }

        #region ProcessRequest 

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // ensure nonce is the last parameter
                string nonce = payload["nonce"].ToString();
                payload.Remove("nonce");

                var msg = GetFormForPayload(payload) + "&nonce=" + nonce;
                var sig = CryptoUtility.SHA512Sign(msg, CryptoUtility.SecureStringToBytes(PrivateApiKey)).ToLower();
                request.Headers.Add("Sign", sig);
                request.Headers.Add("Key", PublicApiKey.ToUnsecureString());

                using (Stream stream = request.GetRequestStream())
                {
                    byte[] content = Encoding.UTF8.GetBytes(msg);
                    stream.Write(content, 0, content.Length);
                    stream.Flush();
                    stream.Close();
                }
            }
        }

        #endregion

        #region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>>  OnGetCurrenciesAsync()
        {
            Dictionary<string, ExchangeCurrency> currencies = new Dictionary<string, ExchangeCurrency>();
            // {"LTC":{"id":"2","name":"Litecoin","website":"www.litecoin.org","withdrawfee":"0.001","minconfs":"6","makerfee":"0","takerfee":"0.3","disabled":"0"}, ...
            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=getcoins");
            foreach(JProperty prop in token)
            {
                currencies.Add(prop.Name.ToStringInvariant(), new ExchangeCurrency()
                {
                    Name = prop.Name,
                    FullName = prop.First["name"].ToStringInvariant(),
                    IsEnabled = prop.First["disabled"].ToStringInvariant().Equals("0"),
                    MinConfirmations = prop.First["minconfs"].ConvertInvariant<int>(),
                    TxFee = prop.First["withdrawfee"].ConvertInvariant<decimal>(),
                    Notes = prop.First["website"].ToStringInvariant(),
                });
            }
            return currencies;
        }

        /// <summary>
        /// Uses TuxExchange getticker method to return market names
        /// </summary>
        /// <returns></returns>
        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            // {"BTC_LTC":{"id":"2","last":"0.0068","lowestAsk":0,"highestBid":0,"percentChange":"6.249999999999989","quoteVolume":"0.5550265","isFrozen":0,"baseVolume":0,"high24hr":"0.0068","low24hr":"0.0064"}, ...
            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=getticker");
            return (from prop in token.Children<JProperty>() select prop.Name).ToList();
        }
         

        /// <summary>
        /// Uses TuxExchange getticker method to get as much Market info as provided
        /// </summary>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // {"BTC_LTC":{"id":"2","last":"0.0068","lowestAsk":0,"highestBid":0,"percentChange":"6.249999999999989","quoteVolume":"0.5550265","isFrozen":0,"baseVolume":0,"high24hr":"0.0068","low24hr":"0.0064"}, ...
            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=getticker");
            foreach(JProperty prop in token)
            {
                var split = prop.Name.Split('_');
                markets.Add(new ExchangeMarket()
                {
                    MarketName = prop.Name.ToStringInvariant(),
                    IsActive = prop.First["isFrozen"].ConvertInvariant<int>() == 0,
                    BaseCurrency = split[0],
                    MarketCurrency = split[1]
                });
            }
            return markets;
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            // {"BTC_LTC":{"id":"2","last":"0.0068","lowestAsk":0,"highestBid":0,"percentChange":"6.249999999999989","quoteVolume":"0.5550265","isFrozen":0,"baseVolume":0,"high24hr":"0.0068","low24hr":"0.0064"}, ...
            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=getticker");
            foreach (JProperty prop in token)
            {
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(prop.Name, new ExchangeTicker()
                {
                    Id = prop.First["id"].ToStringLowerInvariant(),
                    Ask = prop.First["lowestAsk"].ConvertInvariant<decimal>(),
                    Bid = prop.First["highestBid"].ConvertInvariant<decimal>(),
                    Last = prop.First["last"].ConvertInvariant<decimal>(),
                    Volume = new ExchangeVolume()
                    {
                        // not sure which is which here
                         QuantityAmount = prop.First["quoteVolume"].ConvertInvariant<decimal>(),
                         PriceAmount = prop.First["baseVolume"].ConvertInvariant<decimal>(),
                    }
                }));
            }
            return tickers;
        }

        /// <summary>
        /// Uses TuxExchange getticker method and filters by symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var tickers = await OnGetTickersAsync();
            return tickers.Where(t => t.Key.Equals(symbol)).Select(t => t.Value).FirstOrDefault();     
        }


        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            ExchangeOrderBook orders = new ExchangeOrderBook();
            var split = symbol.Split('_');
            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=getorders&coin=" + split[1] + "&market=" + split[0]);
            if (token != null && token.HasValues)
            {
                foreach (JArray order in token["asks"]) orders.Asks.Add(new ExchangeOrderPrice() { Price = order[0].ConvertInvariant<decimal>(), Amount = order[1].ConvertInvariant<decimal>() });
                foreach (JArray order in token["bids"]) orders.Bids.Add(new ExchangeOrderPrice() { Price = order[0].ConvertInvariant<decimal>(), Amount = order[1].ConvertInvariant<decimal>() });
            }
            return orders;
        }


        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();

            // TuxExchange requires a begin and end date for this call
            // we'll return an arbitrary last 10 trades from the last 24 hrs for the market symbol (volume could be low)
            long start = (long)DateTime.UtcNow.AddDays(-1).UnixTimestampFromDateTimeSeconds();
            long end = (long)DateTime.UtcNow.UnixTimestampFromDateTimeSeconds();

            // All TuxExchange Market Symbols begin with "BTC_" as a base-currency. They only support getting Trades for the Market Currency Symbol, so we split it for the call
            string cur = symbol.Split('_')[1];

            // [{"tradeid":"3375","date":"2016-08-26 18:53:38","type":"buy","rate":"0.00000041","amount":"420.00000000","total":"0.00017220"}, ... ]
            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=gettradehistory&coin=" + cur + "&start=" + start + "&end=" + end);
            foreach(JToken trade in token)
            {
                trades.Add(new ExchangeTrade()
                {
                     Timestamp = trade["date"].ConvertInvariant<DateTime>(),
                     Id = trade["tradeid"].ConvertInvariant<long>(),
                     IsBuy = trade["type"].ToStringInvariant().Equals("buy"),
                     Amount = trade["amount"].ConvertInvariant<decimal>(),
                     Price = trade["rate"].ConvertInvariant<decimal>() 
                });
            }
            if (trades.Count > 0) return trades.OrderByDescending(t => t.Timestamp).Take(10);
            else return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? sinceDateTime = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();

            long start = sinceDateTime == null ? (long)DateTime.UtcNow.AddDays(-1).UnixTimestampFromDateTimeSeconds() : new DateTimeOffset((DateTime)sinceDateTime).ToUnixTimeSeconds();
            long end = (long)DateTime.UtcNow.UnixTimestampFromDateTimeSeconds();
            string cur = symbol.Split('_')[1];


            JToken token = await MakeJsonRequestAsync<JToken>("/api?method=gettradehistory&coin=" + cur + "&start=" + start + "&end=" + end);
            foreach (JToken trade in token)
            {
                trades.Add(new ExchangeTrade()
                {
                    Timestamp = trade["date"].ConvertInvariant<DateTime>(),
                    Id = trade["tradeid"].ConvertInvariant<long>(),
                    IsBuy = trade["type"].ToStringInvariant().Equals("buy"),
                    Amount = trade["amount"].ConvertInvariant<decimal>(),
                    Price = trade["rate"].ConvertInvariant<decimal>()
                });
            }
            callback?.Invoke(trades);
        }

        /// <summary>
        /// TuxExchange doesn't support GetCandles. It is possible to get all trades since startdate (filter by enddate if needed) and then aggregate into MarketCandles by periodSeconds 
        /// TODO: Aggregate TuxExchange Trades into Candles
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private APIs

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();

            var payload = GetNoncePayload();
            payload.Add("method", "getmybalances");

            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            if (token != null && token.HasValues)
            {
                foreach (JProperty amount in token)
                {
                    decimal balance = amount.First["balance"].ConvertInvariant<decimal>() + amount.First["inorders"].ConvertInvariant<decimal>();
                    if (balance > 0m) amounts.Add(amount.Name, balance);
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = GetNoncePayload();
            payload.Add("method", "getmybalances");

            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            if (token != null && token.HasValues)
            {
                foreach (JProperty amount in token)
                {
                    decimal balance = amount.First["balance"].ConvertInvariant<decimal>();
                    if (balance > 0m) amounts.Add(amount.Name, balance);
                }
            }
            return amounts;
        }

        /// <summary>
        /// TODO: Exchange API Documentation is missing the return values of this call
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="afterDate"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            var payload = GetNoncePayload();
            payload.Add("method", "getmytradehistory");

            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            // AAAAAAGGGGHHHHHHH!!!!!!!!!
            // I don't trade on TuxExchange because their volumes have been too low, so I don't know what this returns
            // The documentation is missing the details of this call, and I can't find any interface online that implements it!
            // If someone, anyone, knows what this call returns, it can be completed...
            throw new NotImplementedException("API Interface incomplete");
        }

        /// <summary>
        /// TODO: Exchange API Documentation is missing the return values of this call
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();

            var payload = GetNoncePayload();
            payload.Add("method", "getmyopenorders");
            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");

            // see OnGetCompletedOrderDetailsAsync
            throw new NotImplementedException("API Interface Incomplete");
        }

        protected override Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId)
        {
            // see OnGetCompletedOrderDetailsAsync
            throw new NotImplementedException("API Interface Incomplete");
        }


        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var split = order.Symbol.Split('_');
            var payload = GetNoncePayload();
            payload.Add("method", order.IsBuy ? "buy" : "sell");
            payload.Add("market", split[0]);
            payload.Add("coin", split[1]);
            payload.Add("amount", order.Amount);
            payload.Add("price", order.Price);
            // ( [success] => 1 )   - only data returned is success or fail on buy
            // ( [success] => 1 [error] => Array ( [0] =>61 [1] => Sell order placed. )) - an array is also returned on sell, but no documentation as to what the values are
            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            var rc = new ExchangeOrderResult();
            if (token["success"].ConvertInvariant<int>() == 1) rc.Result = ExchangeAPIOrderResult.Filled; // I guess...
            else rc.Result = ExchangeAPIOrderResult.Error;
            return rc;
        }

        // This should have a return value for success
        protected override async Task OnCancelOrderAsync(string orderId)
        {
            var payload = GetNoncePayload();
            payload.Add("method", "cancelorder");
            payload.Add("id", orderId);
            // TODO: a 'market' payload with split symbol *may* be required, in which case we'll have to do a lookup, but we can't. Again, the documentation is incomplete
            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            // nothing is returned on this call
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            List<ExchangeTransaction> deposits = new List<ExchangeTransaction>();
            var payload = GetNoncePayload();
            payload.Add("method", "getmydeposithistory");

            // {"365": { "coin": "DOGE","amount": "3","address": "D7ssLc8M4L3bKaku232GBeqxshbbD43hFM","date": "2016-02-11 21:23:44","txid": "ae4d47bc130ac8e2e1960ee3c3545963a380f6ef268d384f8fc3d6a2220c92fb" }
            // I'll assume the first Property '365' is the number of days returned - ignored in any case...  Seriously, they need to clean up their docs
            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            foreach(JToken deposit in token.First)
            {
                if (deposit["symbol"].ToStringInvariant().Equals(symbol)) deposits.Add(new ExchangeTransaction()
                {
                    Symbol = symbol,
                    TimestampUTC = deposit["date"].ConvertInvariant<DateTime>(),
                    Address = deposit["coin"].ToStringInvariant(),
                    BlockchainTxId = deposit["txid"].ToStringInvariant(),
                    Amount = deposit["amount"].ConvertInvariant<decimal>(),
                    Status = TransactionStatus.Complete    // I guess...
                });
            }
            return deposits;
        }


        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            var payload = GetNoncePayload();
            payload.Add("method", "getmyaddresses");

            // "addresses": { "BTC": "14iuWRBwB35HYG98vBxmVJoJZG73BZy4bZ", "LTC": "LXLWHFLpPbcKx69diMVEXVLAzSMXsyrQH2", "DOGE": "DGon17FjjTTVXaHeotm1gvw6ewUZ49WeZr",  }
            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            if (token != null && token.HasValues && token["addresses"][symbol] != null)
            {
                return new ExchangeDepositDetails()
                {
                    Symbol = symbol,
                    Address = token["addresses"][symbol].ToStringInvariant()
                };
            }
            return null;
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            ExchangeWithdrawalResponse response = new ExchangeWithdrawalResponse { Success = false };

            var payload = GetNoncePayload();
            payload.Add("method", "withdraw");
            payload.Add("coin", withdrawalRequest.Symbol);
            payload.Add("amount", withdrawalRequest.Amount);
            payload.Add("address", withdrawalRequest.Address);
            // ( [success] => 1 [error] => Array ([0] => Withdraw requested. ))
            JToken token = await MakeJsonRequestAsync<JToken>("/api", null, payload, "POST");
            if (token["success"].ConvertInvariant<int>() == 1) response.Success = true;
            return response;
        }

        #endregion
    }
}
