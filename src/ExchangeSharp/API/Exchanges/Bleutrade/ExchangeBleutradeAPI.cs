/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    // this exchange dropped v2 api, needs to be entirely re-coded
#if HAS_FIXED_BLEUTRADE_API

    public sealed partial class ExchangeBleutradeAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://bleutrade.com/api/v2";

        static ExchangeBleutradeAPI()
        {
            ExchangeGlobalCurrencyReplacements[typeof(ExchangeBleutradeAPI)] = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("BCC", "BCH")
            };
        }

        public ExchangeBleutradeAPI()
        {
            NonceStyle = NonceStyle.UnixMillisecondsString;
            MarketSymbolSeparator = "_";
        }

#region ProcessRequest

        protected override Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                request.AddHeader("apisign", CryptoUtility.SHA512Sign(request.RequestUri.ToString(), PrivateApiKey.ToUnsecureString()).ToLowerInvariant());
            }
            return base.ProcessRequestAsync(request, payload);
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query
                var query = (url.Query ?? string.Empty).Trim('?', '&');
                url.Query = "apikey=" + PublicApiKey.ToUnsecureString() + "&nonce=" + payload["nonce"].ToStringInvariant() + (query.Length != 0 ? "&" + query : string.Empty);
            }
            return url.Uri;
        }

#endregion

#region Public APIs

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            var currencies = new Dictionary<string, ExchangeCurrency>(StringComparer.OrdinalIgnoreCase);
            //{ "success" : true,"message" : "", "result" : [{"Currency" : "BTC","CurrencyLong" : "Bitcoin","MinConfirmation" : 2,"TxFee" : 0.00080000,"IsActive" : true, "CoinType" : "BITCOIN","MaintenanceMode" : false}, ...
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getcurrencies", null, null);
            foreach (JToken token in result)
            {
                bool isMaintenanceMode = token["MaintenanceMode"].ConvertInvariant<bool>();
                var coin = new ExchangeCurrency
                {
                    CoinType = token["CoinType"].ToStringInvariant(),
                    FullName = token["CurrencyLong"].ToStringInvariant(),
                    DepositEnabled = !isMaintenanceMode,
                    WithdrawalEnabled = !isMaintenanceMode,
                    MinConfirmations = token["MinConfirmation"].ConvertInvariant<int>(),
                    Name = token["Currency"].ToStringUpperInvariant(),
                    Notes = token["Notice"].ToStringInvariant(),
                    TxFee = token["TxFee"].ConvertInvariant<decimal>(),
                };
                currencies[coin.Name] = coin;
            }
            return currencies;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            List<string> symbols = new List<string>();
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getmarkets", null, null);
            foreach (var market in result) symbols.Add(market["MarketName"].ToStringInvariant());
            return symbols;
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            // "result" : [{"MarketCurrency" : "DOGE","BaseCurrency" : "BTC","MarketCurrencyLong" : "Dogecoin","BaseCurrencyLong" : "Bitcoin", "MinTradeSize" : 0.10000000, "MarketName" : "DOGE_BTC", "IsActive" : true, }, ...
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getmarkets", null, null);
            foreach (JToken token in result)
            {
                markets.Add(new ExchangeMarket()
                {
                    //NOTE: Bleutrade is another weird one that calls the QuoteCurrency the "BaseCurrency" and the BaseCurrency the "MarketCurrency".
                    QuoteCurrency = token["BaseCurrency"].ToStringInvariant(),
                    BaseCurrency = token["MarketCurrency"].ToStringInvariant(),
                    MarketSymbol = token["MarketName"].ToStringInvariant(),
                    IsActive = token["IsActive"].ToStringInvariant().Equals("true"),
                    MinTradeSize = token["MinTradeSize"].ConvertInvariant<decimal>(),
                });
            }
            return markets;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getmarketsummary?market=" + marketSymbol);
            return this.ParseTicker(result, marketSymbol, "Ask", "Bid", "Last", "Volume", "BaseVolume", "Timestamp", TimestampType.Iso8601);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            // "result" : [{"MarketCurrency" : "Ethereum","BaseCurrency" : "Bitcoin","MarketName" : "ETH_BTC","PrevDay" : 0.00095000,"High" : 0.00105000,"Low" : 0.00086000, "Last" : 0.00101977, "Average" : 0.00103455, "Volume" : 2450.97496015, "BaseVolume" : 2.40781647,    "TimeStamp" : "2014-07-29 11:19:30", "Bid" : 0.00100000, "Ask" : 0.00101977, "IsActive" : true }, ... ]
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getmarketsummaries");
            foreach (JToken token in result)
            {
                var ticker = this.ParseTicker(token, token["MarketName"].ToStringInvariant(), "Ask", "Bid", "Last", "Volume", "BaseVolume", "Timestamp", TimestampType.Iso8601);
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(token["MarketName"].ToStringInvariant(), ticker));
            }
            return tickers;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = PeriodSecondsToString(periodSeconds);
            limit = limit ?? (limit > 2160 ? 2160 : limit);
            endDate = endDate ?? CryptoUtility.UtcNow.AddMinutes(1.0);
            startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));

            //market period(15m, 20m, 30m, 1h, 2h, 3h, 4h, 6h, 8h, 12h, 1d) count(default: 1000, max: 999999) lasthours(default: 24, max: 2160)
            //"result":[{"TimeStamp":"2014-07-31 10:15:00","Open":"0.00000048","High":"0.00000050","Low":"0.00000048","Close":"0.00000049","Volume":"594804.73036048","BaseVolume":"0.11510368" }, ...
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getcandles?market=" + marketSymbol + "&period=" + periodString + (limit == null ? string.Empty : "&lasthours=" + limit));
            foreach (JToken jsonCandle in result)
            {
                //NOTE: Bleutrade uses the term "BaseVolume" when referring to the QuoteCurrencyVolume
                MarketCandle candle = this.ParseCandle(jsonCandle, marketSymbol, periodSeconds, "Open", "High", "Low", "Close", "Timestamp", TimestampType.Iso8601, "Volume", "BaseVolume");
                if (candle.Timestamp >= startDate && candle.Timestamp <= endDate)
                {
                    candles.Add(candle);
                }
            }
            return candles;
        }


        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            //"result" : [{ "TimeStamp" : "2014-07-29 18:08:00","Quantity" : 654971.69417461,"Price" : 0.00000055,"Total" : 0.360234432,"OrderType" : "BUY"}, ...  ]
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getmarkethistory?market=" + marketSymbol);
            foreach (JToken token in result) trades.Add(ParseTrade(token));
            return trades;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            // TODO: Not directly supported so the best we can do is get their Max 200 and check the timestamp if necessary
            JToken result = await MakeJsonRequestAsync<JToken>("/public/getmarkethistory?market=" + marketSymbol + "&count=200");
            foreach (JToken token in result)
            {
                ExchangeTrade trade = ParseTrade(token);
                if (startDate == null || trade.Timestamp >= startDate)
                {
                    trades.Add(trade);
                }
            }
            if (trades.Count != 0)
            {
                callback(trades);
            }
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            //"result" : { "buy" : [{"Quantity" : 4.99400000,"Rate" : 3.00650900}, {"Quantity" : 50.00000000, "Rate" : 3.50000000 }  ] ...
            JToken token = await MakeJsonRequestAsync<JToken>("/public/getorderbook?market=" + marketSymbol + "&type=ALL&depth=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(token, "sell", "buy", "Rate", "Quantity", maxCount: maxCount);
        }

        #endregion

        #region Private APIs

                protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
                {
                    Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
                    // "result" : [{"Currency" : "DOGE","Balance" : 0.00000000,"Available" : 0.00000000,"Pending" : 0.00000000,"CryptoAddress" : "DBSwFELQiVrwxFtyHpVHbgVrNJXwb3hoXL", "IsActive" : true}, ...
                    JToken result = await MakeJsonRequestAsync<JToken>("/account/getbalances", null, await GetNoncePayloadAsync());
                    foreach (JToken token in result)
                    {
                        decimal amount = result["Balance"].ConvertInvariant<decimal>();
                        if (amount > 0) amounts[token["Currency"].ToStringInvariant()] = amount;
                    }
                    return amounts;
                }

                protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
                {
                    Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
                    // "result" : [{"Currency" : "DOGE","Balance" : 0.00000000,"Available" : 0.00000000,"Pending" : 0.00000000,"CryptoAddress" : "DBSwFELQiVrwxFtyHpVHbgVrNJXwb3hoXL", "IsActive" : true}, ...
                    JToken result = await MakeJsonRequestAsync<JToken>("/account/getbalances", null, await GetNoncePayloadAsync());
                    foreach (JToken token in result)
                    {
                        decimal amount = result["Available"].ConvertInvariant<decimal>();
                        if (amount > 0) amounts[token["Currency"].ToStringInvariant()] = amount;
                    }
                    return amounts;
                }

                protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
                {
                    // "result" : { "OrderId" : "65489","Exchange" : "LTC_BTC", "Type" : "BUY", "Quantity" : 20.00000000, "QuantityRemaining" : 5.00000000, "QuantityBaseTraded" : "0.16549400", "Price" : 0.01268311, "Status" : "OPEN", "Created" : "2014-08-03 13:55:20", "Comments" : "My optional comment, eg function id #123"  }
                    JToken result = await MakeJsonRequestAsync<JToken>("/account/getorder?orderid=" + orderId, null, await GetNoncePayloadAsync());
                    return ParseOrder(result);
                }

                protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
                {
                    List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
                    JToken result = await MakeJsonRequestAsync<JToken>("/account/getorders?market=" + (string.IsNullOrEmpty(marketSymbol) ? "ALL" : marketSymbol) + "&orderstatus=OK&ordertype=ALL", null, await GetNoncePayloadAsync());
                    foreach (JToken token in result)
                    {
                        ExchangeOrderResult order = ParseOrder(token);
                        if (afterDate != null) { if (order.OrderDate > afterDate) orders.Add(order); }
                        else orders.Add(order);
                    }
                    return orders;
                }

                protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
                {
                    List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
                    JToken result = await MakeJsonRequestAsync<JToken>("/market/getopenorders", null, await GetNoncePayloadAsync());
                    foreach (JToken token in result) orders.Add(ParseOrder(token));
                    return orders;
                }

                protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
                {
                    ExchangeOrderResult result = new ExchangeOrderResult() { Result = ExchangeAPIOrderResult.Error };
                    var payload = await GetNoncePayloadAsync();
                    order.ExtraParameters.CopyTo(payload);

                    // Only limit order is supported - no indication on how it is filled
                    JToken token = await MakeJsonRequestAsync<JToken>((order.IsBuy ? "/market/buylimit?" : "market/selllimit?") + "market=" + order.MarketSymbol +
                        "&rate=" + order.Price.ToStringInvariant() + "&quantity=" + order.RoundAmount().ToStringInvariant(), null, payload);
                    if (token.HasValues)
                    {
                        // Only the orderid is returned on success
                        result.OrderId = token["orderid"].ToStringInvariant();
                        result.Result = ExchangeAPIOrderResult.Filled;
                    }
                    return result;
                }

                protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
                {
                    await MakeJsonRequestAsync<JToken>("/market/cancel?orderid=" + orderId, null, await GetNoncePayloadAsync());
                }

                protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
                {
                    JToken token = await MakeJsonRequestAsync<JToken>("/account/getdepositaddress?" + "currency=" + NormalizeMarketSymbol(currency), BaseUrl, await GetNoncePayloadAsync());
                    if (token["Currency"].ToStringInvariant().Equals(currency) && token["Address"] != null)
                    {
                        // At this time, according to Bleutrade support, they don't support any currency requiring an Address Tag, but they will add this feature in the future
                        return new ExchangeDepositDetails()
                        {
                            Currency = token["Currency"].ToStringInvariant(),
                            Address = token["Address"].ToStringInvariant()
                        };
                    }
                    return null;
                }

                protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
                {
                    List<ExchangeTransaction> transactions = new List<ExchangeTransaction>();

                    // "result" : [{"Id" : "44933431","TimeStamp" : "2015-05-13 07:15:23","Coin" : "LTC","Amount" : -0.10000000,"Label" : "Withdraw: 0.99000000 to address Anotheraddress; fee 0.01000000","TransactionId" : "c396228895f8976e3810286c1537bddd4a45bb37d214c0e2b29496a4dee9a09b" }
                    JToken result = await MakeJsonRequestAsync<JToken>("/account/getdeposithistory", BaseUrl, await GetNoncePayloadAsync());
                    foreach (JToken token in result)
                    {
                        transactions.Add(new ExchangeTransaction()
                        {
                            PaymentId = token["Id"].ToStringInvariant(),
                            BlockchainTxId = token["TransactionId"].ToStringInvariant(),
                            Timestamp = token["TimeStamp"].ToDateTimeInvariant(),
                            Currency = token["Coin"].ToStringInvariant(),
                            Amount = token["Amount"].ConvertInvariant<decimal>(),
                            Notes = token["Label"].ToStringInvariant(),
                            TxFee = token["fee"].ConvertInvariant<decimal>(),
                            Status = TransactionStatus.Unknown
                        });
                    }
                    return transactions;
                }

                protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
                {
                    var payload = await GetNoncePayloadAsync();
                    payload["currency"] = withdrawalRequest.Currency;
                    payload["quantity"] = withdrawalRequest.Amount;
                    payload["address"] = withdrawalRequest.Address;
                    if (!string.IsNullOrEmpty(withdrawalRequest.AddressTag)) payload["comments"] = withdrawalRequest.AddressTag;

                    await MakeJsonRequestAsync<JToken>("/account/withdraw", BaseUrl, payload);

                    // Bleutrade doesn't return any info, just an empty string on success. The MakeJsonRequestAsync will throw an exception if there's an error
                    return new ExchangeWithdrawalResponse() { Success = true };
                }


        #endregion

        #region Private Functions

                private ExchangeTrade ParseTrade(JToken token)
                {
                    return token.ParseTrade("Quantity", "Price", "OrderType", "TimeStamp", TimestampType.Iso8601);
                }

                private ExchangeOrderResult ParseOrder(JToken token)
                {
                    var order = new ExchangeOrderResult()
                    {
                        OrderId = token["OrderId"].ToStringInvariant(),
                        IsBuy = token["Type"].ToStringInvariant().Equals("BUY"),
                        MarketSymbol = token["Exchange"].ToStringInvariant(),
                        Amount = token["Quantity"].ConvertInvariant<decimal>(),
                        OrderDate = token["Created"].ToDateTimeInvariant(),
                        AveragePrice = token["Price"].ConvertInvariant<decimal>(),
                        AmountFilled = token["QuantityBaseTraded"].ConvertInvariant<decimal>(),
                        Message = token["Comments"].ToStringInvariant(),
                    };

                    switch (token["status"].ToStringInvariant())
                    {
                        case "OPEN":
                            order.Result = ExchangeAPIOrderResult.Pending;
                            break;
                        case "OK":
                            if (order.Amount == order.AmountFilled) order.Result = ExchangeAPIOrderResult.Filled;
                            else order.Result = ExchangeAPIOrderResult.FilledPartially;
                            break;
                        case "CANCELED":
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

    public partial class ExchangeName { public const string Bleutrade = "Bleutrade"; }

#endif

}
