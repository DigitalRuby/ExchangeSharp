/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Web;

    public sealed partial class ExchangeBittrexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://bittrex.com/api/v1.1";
        public string BaseUrl2 { get; set; } = "https://bittrex.com/api/v2.0";

        /// <summary>Coin types that both an address and a tag to make the deposit</summary>
        public HashSet<string> TwoFieldDepositCoinTypes { get; }

        /// <summary>Coin types that only require an address to make the deposit</summary>
        public HashSet<string> OneFieldDepositCoinTypes { get; }

        public ExchangeBittrexAPI()
        {
            // https://bittrex.github.io/api/v1-1#call-limits (Same counts for the V3 API)
            RateLimit = new RateGate(60, TimeSpan.FromSeconds(60));

            TwoFieldDepositCoinTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "BITSHAREX",
                "CRYPTO_NOTE_PAYMENTID",
                "LUMEN",
                "NEM",
                "NXT",
                "NXT_MS",
                "RIPPLE",
                "STEEM"
            };

            OneFieldDepositCoinTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ADA",
                "ANTSHARES",
                "BITCOIN",
                "BITCOIN_PERCENTAGE_FEE",
                "BITCOIN_STEALTH",
                "BITCOINEX",
                "BYTEBALL",
                "COUNTERPARTY",
                "ETH",
                "ETH_CONTRACT",
                "FACTOM",
                "LISK",
                "OMNI",
                "SIA",
                "WAVES",
                "WAVES_ASSET",
            };

            MarketSymbolIsReversed = true;
            WebSocketOrderBookType = WebSocketOrderBookType.DeltasOnly;
        }

        public override string PeriodSecondsToString(int seconds)
        {
            string periodString;
            switch (seconds)
            {
                case 60: periodString = "oneMin"; break;
                case 300: periodString = "fiveMin"; break;
                case 1800: periodString = "thirtyMin"; break;
                case 3600: periodString = "hour"; break;
                case 86400: periodString = "day"; break;
                case 259200: periodString = "threeDay"; break;
                case 604800: periodString = "week"; break;
                default:
                    if (seconds > 604800)
                    {
                        periodString = "month";
                    }
                    else
                    {
                        throw new ArgumentException($"{nameof(seconds)} must be one of 60 (min), 300 (fiveMin), 1800 (thirtyMin), 3600 (hour), 86400 (day), 259200 (threeDay), 604800 (week), 2419200 (month)");
                    }
                    break;
            }
            return periodString;
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            ExchangeOrderResult order = new ExchangeOrderResult();
            decimal amount = token["Quantity"].ConvertInvariant<decimal>();
            decimal remaining = token["QuantityRemaining"].ConvertInvariant<decimal>();
            decimal amountFilled = amount - remaining;
            order.Amount = amount;
            order.AmountFilled = amountFilled;
            order.AveragePrice = token["PricePerUnit"].ConvertInvariant<decimal>();
            order.Price = token["Limit"].ConvertInvariant<decimal>(order.AveragePrice);
            order.Message = string.Empty;
            order.OrderId = token["OrderUuid"].ToStringInvariant();
            if (token["CancelInitiated"].ConvertInvariant<bool>())
            {
                order.Result = ExchangeAPIOrderResult.Canceled;
            }
            else if (amountFilled >= amount)
            {
                order.Result = ExchangeAPIOrderResult.Filled;
            }
            else if (amountFilled == 0m)
            {
                order.Result = ExchangeAPIOrderResult.Pending;
            }
            else
            {
                order.Result = ExchangeAPIOrderResult.FilledPartially;
            }
            order.OrderDate = token["Opened"].ToDateTimeInvariant(token["TimeStamp"].ToDateTimeInvariant());
            order.MarketSymbol = token["Exchange"].ToStringInvariant();
            order.Fees = token["Commission"].ConvertInvariant<decimal>(); // This is always in the base pair (e.g. BTC, ETH, USDT)

            string exchangePair = token["Exchange"].ToStringInvariant();
            if (!string.IsNullOrWhiteSpace(exchangePair))
            {
                string[] pairs = exchangePair.Split('-');
                if (pairs.Length == 2)
                {
                    order.FeesCurrency = pairs[0];
                }
            }

            string type = token["OrderType"].ToStringInvariant();
            if (string.IsNullOrWhiteSpace(type))
            {
                type = token["Type"].ToStringInvariant();
            }
            order.IsBuy = type.IndexOf("BUY", StringComparison.OrdinalIgnoreCase) >= 0;
            return order;
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
                var query = (url.Query ?? string.Empty).Trim('?', '&');
                url.Query = "apikey=" + PublicApiKey.ToUnsecureString() + "&nonce=" + payload["nonce"].ToStringInvariant() + (query.Length != 0 ? "&" + query : string.Empty);
            }
            return url.Uri;
        }

        protected override Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string url = request.RequestUri.ToString();
                string sign = CryptoUtility.SHA512Sign(url, PrivateApiKey.ToUnsecureString());
                request.AddHeader("apisign", sign);
            }
            return base.ProcessRequestAsync(request, payload);
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            var currencies = new Dictionary<string, ExchangeCurrency>(StringComparer.OrdinalIgnoreCase);
            JToken array = await MakeJsonRequestAsync<JToken>("/public/getcurrencies");
            foreach (JToken token in array)
            {
                bool enabled = token["IsActive"].ConvertInvariant<bool>();
                var coin = new ExchangeCurrency
                {
                    BaseAddress = token["BaseAddress"].ToStringInvariant(),
                    CoinType = token["CoinType"].ToStringInvariant(),
                    FullName = token["CurrencyLong"].ToStringInvariant(),
                    DepositEnabled = enabled,
                    WithdrawalEnabled = enabled,
                    MinConfirmations = token["MinConfirmation"].ConvertInvariant<int>(),
                    Name = token["Currency"].ToStringUpperInvariant(),
                    Notes = token["Notice"].ToStringInvariant(),
                    TxFee = token["TxFee"].ConvertInvariant<decimal>(),
                };

                currencies[coin.Name] = coin;
            }

            return currencies;
        }

        /// <summary>
        /// Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            var markets = new List<ExchangeMarket>();
            JToken array = await MakeJsonRequestAsync<JToken>("/public/getmarkets");

            // StepSize is 8 decimal places for both price and amount on everything at Bittrex
            const decimal StepSize = 0.00000001m;
            foreach (JToken token in array)
            {
                var market = new ExchangeMarket
                {
                    //NOTE: Bittrex is weird in that they call the QuoteCurrency the "BaseCurrency" and the BaseCurrency the "MarketCurrency".
                    QuoteCurrency = token["BaseCurrency"].ToStringUpperInvariant(),
                    IsActive = token["IsActive"].ConvertInvariant<bool>(),
                    BaseCurrency = token["MarketCurrency"].ToStringUpperInvariant(),
                    //NOTE: They also reverse the order of the currencies in the MarketName
                    MarketSymbol = token["MarketName"].ToStringUpperInvariant(),
                    MinTradeSize = token["MinTradeSize"].ConvertInvariant<decimal>(),
                    MinPrice = StepSize,
                    PriceStepSize = StepSize,
                    QuantityStepSize = StepSize
                };

                markets.Add(market);
            }

            return markets;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            return (await GetMarketSymbolsMetadataAsync()).Select(x => x.MarketSymbol);
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            JToken ticker = await MakeJsonRequestAsync<JToken>("/public/getmarketsummary?market=" + marketSymbol);
            //NOTE: Bittrex uses the term "BaseVolume" when referring to the QuoteCurrencyVolume
            return await this.ParseTickerAsync(ticker[0], marketSymbol, "Ask", "Bid", "Last", "Volume", "BaseVolume", "Timestamp", TimestampType.Iso8601);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            JToken tickers = await MakeJsonRequestAsync<JToken>("public/getmarketsummaries");
            string marketSymbol;
            List<KeyValuePair<string, ExchangeTicker>> tickerList = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (JToken ticker in tickers)
            {
                marketSymbol = ticker["MarketName"].ToStringInvariant();
                //NOTE: Bittrex uses the term "BaseVolume" when referring to the QuoteCurrencyVolume
                ExchangeTicker tickerObj = await this.ParseTickerAsync(ticker, marketSymbol, "Ask", "Bid", "Last", "Volume", "BaseVolume", "Timestamp", TimestampType.Iso8601);
                tickerList.Add(new KeyValuePair<string, ExchangeTicker>(marketSymbol, tickerObj));
            }
            return tickerList;
        }

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {
            JToken token = await MakeJsonRequestAsync<JToken>("public/getorderbook?market=" + marketSymbol + "&type=both&limit_bids=" + maxCount + "&limit_asks=" + maxCount);
            return ExchangeAPIExtensions.ParseOrderBookFromJTokenDictionaries(token, "sell", "buy", "Rate", "Quantity", maxCount: maxCount);
        }

        /// <summary>Gets the deposit history for a symbol</summary>
        /// <param name="currency">The symbol to check. May be null.</param>
        /// <returns>Collection of ExchangeTransactions</returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string currency)
        {
            var transactions = new List<ExchangeTransaction>();
            string url = $"/account/getdeposithistory{(string.IsNullOrWhiteSpace(currency) ? string.Empty : $"?currency={currency}")}";
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            foreach (JToken token in result)
            {
                var deposit = new ExchangeTransaction
                {
                    Amount = token["Amount"].ConvertInvariant<decimal>(),
                    Address = token["CryptoAddress"].ToStringInvariant(),
                    Currency = token["Currency"].ToStringInvariant(),
                    PaymentId = token["Id"].ToStringInvariant(),
                    BlockchainTxId = token["TxId"].ToStringInvariant(),
                    Status = TransactionStatus.Complete // As soon as it shows up in this list it is complete (verified manually)
                };

                DateTime.TryParse(token["LastUpdated"].ToStringInvariant(), out DateTime timestamp);
                deposit.Timestamp = timestamp;

                transactions.Add(deposit);
            }

            return transactions;
        }

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol,
			DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
			throw new APIException(
				"Bittrex does not allow querying trades by dates. Consider using either GetRecentTradesAsync() or GetCandlesAsync() w/ a period of 1 min. See issue #508.");
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol, int? limit = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            string baseUrl = "/public/getmarkethistory?market=" + marketSymbol;
            JToken array = await MakeJsonRequestAsync<JToken>(baseUrl);
            foreach (JToken token in array)
            {
                trades.Add(token.ParseTrade("Quantity", "Price", "OrderType", "TimeStamp", TimestampType.Iso8601, "Id"));
            }
            return trades;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds,
			DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported in Bittrex");
            }

            // https://bittrex.com/Api/v2.0/pub/market/GetTicks?marketName=BTC-WAVES&tickInterval=day
            // "{"success":true,"message":"","result":[{"O":0.00011000,"H":0.00060000,"L":0.00011000,"C":0.00039500,"V":5904999.37958770,"T":"2016-06-20T00:00:00","BV":2212.16809610} ] }"
            string periodString = PeriodSecondsToString(periodSeconds);
            List<MarketCandle> candles = new List<MarketCandle>();
            endDate = endDate ?? CryptoUtility.UtcNow;
            startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));
            JToken result = await MakeJsonRequestAsync<JToken>("pub/market/GetTicks?marketName=" + marketSymbol + "&tickInterval=" + periodString, BaseUrl2);
            if (result is JArray array)
            {
                foreach (JToken jsonCandle in array)
                {
                    //NOTE: Bittrex uses the term "BaseVolume" when referring to the QuoteCurrencyVolume
                    MarketCandle candle = this.ParseCandle(token: jsonCandle, marketSymbol: marketSymbol, periodSeconds: periodSeconds,
						openKey: "O", highKey: "H", lowKey: "L", closeKey: "C", timestampKey: "T", timestampType: TimestampType.Iso8601,
						baseVolumeKey: "V", quoteVolumeKey: "BV");
                    if (candle.Timestamp >= startDate && candle.Timestamp <= endDate)
                    {
                        candles.Add(candle);
                    }
                }
            }

            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string url = "/account/getbalances";
            JToken array = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            foreach (JToken token in array)
            {
                decimal amount = token["Balance"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    currencies.Add(token["Currency"].ToStringInvariant(), amount);
                }
            }
            return currencies;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string url = "/account/getbalances";
            JToken array = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            foreach (JToken token in array)
            {
                decimal amount = token["Available"].ConvertInvariant<decimal>();
                if (amount > 0m)
                {
                    currencies.Add(token["Currency"].ToStringInvariant(), amount);
                }
            }
            return currencies;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == ExchangeSharp.OrderType.Market)
            {
                throw new NotSupportedException("Order type " + order.OrderType + " not supported");
            }

            decimal orderAmount = await ClampOrderQuantity(order.MarketSymbol, order.Amount);
            decimal orderPrice = await ClampOrderPrice(order.MarketSymbol, order.Price);
            string url = (order.IsBuy ? "/market/buylimit" : "/market/selllimit") + "?market=" + order.MarketSymbol + "&quantity=" +
                orderAmount.ToStringInvariant() + "&rate=" + orderPrice.ToStringInvariant();
            foreach (var kv in order.ExtraParameters)
            {
                url += "&" + kv.Key.UrlEncode() + "=" + kv.Value.ToStringInvariant().UrlEncode();
            }
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            string orderId = result["uuid"].ToStringInvariant();
            return new ExchangeOrderResult
            {
                Amount = orderAmount,
                IsBuy = order.IsBuy,
                OrderDate = CryptoUtility.UtcNow,
                OrderId = orderId,
                Result = ExchangeAPIOrderResult.Pending,
                MarketSymbol = order.MarketSymbol,
                Price = order.Price
            };
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            string url = "/account/getorder?uuid=" + orderId;
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            return ParseOrder(result);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            string url = "/market/getopenorders" + (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "?market=" + NormalizeMarketSymbol(marketSymbol));
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            foreach (JToken token in result.Children())
            {
                orders.Add(ParseOrder(token));
            }

            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string marketSymbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            string url = "/account/getorderhistory" + (string.IsNullOrWhiteSpace(marketSymbol) ? string.Empty : "?market=" + NormalizeMarketSymbol(marketSymbol));
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            foreach (JToken token in result.Children())
            {
                ExchangeOrderResult order = ParseOrder(token);

                // Bittrex v1.1 API call has no timestamp parameter, sigh...
                if (afterDate == null || order.OrderDate >= afterDate.Value)
                {
                    orders.Add(order);
                }
            }

            return orders;
        }

        protected override async Task<ExchangeWithdrawalResponse> OnWithdrawAsync(ExchangeWithdrawalRequest withdrawalRequest)
        {
            // Example: https://bittrex.com/api/v1.1/account/withdraw?apikey=API_KEY&currency=EAC&quantity=20.40&address=EAC_ADDRESS

            string url = $"/account/withdraw?currency={NormalizeMarketSymbol(withdrawalRequest.Currency)}&quantity={withdrawalRequest.Amount.ToStringInvariant()}&address={withdrawalRequest.Address}";
            if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
            {
                url += $"&paymentid={withdrawalRequest.AddressTag}";
            }

            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());
            ExchangeWithdrawalResponse withdrawalResponse = new ExchangeWithdrawalResponse
            {
                Id = result["uuid"].ToStringInvariant(),
                Message = result["msg"].ToStringInvariant()
            };

            return withdrawalResponse;
        }

        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetWithdrawHistoryAsync(string currency)
        {
            string url = $"/account/getwithdrawalhistory{(string.IsNullOrWhiteSpace(currency) ? string.Empty : $"?currency={currency}")}";
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());

            var transactions = result.Select(t => new ExchangeTransaction
            {
                Amount = t["Amount"].ConvertInvariant<decimal>(),
                Address = t["Address"].ToStringInvariant(),
                Currency = t["Currency"].ToStringInvariant(),
                PaymentId = t["PaymentUuid"].ToStringInvariant(),
                BlockchainTxId = t["TxId"].ToStringInvariant(),
                TxFee = t["TxCost"].ConvertInvariant<decimal>(),
                Timestamp = DateTime.Parse(t["Opened"].ToStringInvariant()),
                Status = ToStatus(t)
            });

            return transactions;
        }
        private TransactionStatus ToStatus(JToken withdraw)
        {
            if (withdraw["Canceled"].ConvertInvariant<bool>())
                return TransactionStatus.Rejected;

            if (withdraw["InvalidAddress"].ConvertInvariant<bool>())
                return TransactionStatus.Failure;

            if (withdraw["PendingPayment"].ConvertInvariant<bool>())
                return TransactionStatus.AwaitingApproval;

            if (withdraw["Authorized"].ConvertInvariant<bool>())
                return TransactionStatus.Complete;

            return TransactionStatus.Unknown;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            await MakeJsonRequestAsync<JToken>("/market/cancel?uuid=" + orderId, null, await GetNoncePayloadAsync());
        }

        /// <summary>
        /// Gets the address to deposit to and applicable details.
        /// If one does not exist, the call will fail and return ADDRESS_GENERATING until one is available.
        /// </summary>
        /// <param name="currency">Currency to get address for.</param>
        /// <param name="forceRegenerate">(ignored) Bittrex does not support regenerating deposit addresses.</param>
        /// <returns>
        /// Deposit address details (including tag if applicable, such as with XRP)
        /// </returns>
        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string currency, bool forceRegenerate = false)
        {
            IReadOnlyDictionary<string, ExchangeCurrency> updatedCurrencies = (await GetCurrenciesAsync());

            string url = "/account/getdepositaddress?currency=" + NormalizeMarketSymbol(currency);
            JToken result = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());

            // NOTE API 1.1 does not include the the static wallet address for currencies with tags such as XRP & NXT (API 2.0 does!)
            // We are getting the static addresses via the GetCurrencies() api.
            ExchangeDepositDetails depositDetails = new ExchangeDepositDetails
            {
                Currency = result["Currency"].ToStringInvariant(),
            };

            if (!updatedCurrencies.TryGetValue(depositDetails.Currency, out ExchangeCurrency coin))
            {
                Logger.Warn($"Unable to find {depositDetails.Currency} in existing list of coins.");
                return null;
            }

            if (TwoFieldDepositCoinTypes.Contains(coin.CoinType))
            {
                depositDetails.Address = coin.BaseAddress;
                depositDetails.AddressTag = result["Address"].ToStringInvariant();
            }
            else if (OneFieldDepositCoinTypes.Contains(coin.CoinType))
            {
                depositDetails.Address = result["Address"].ToStringInvariant();
            }
            else
            {
                Logger.Warn($"ExchangeBittrexAPI: Unknown coin type {coin.CoinType} must be registered as requiring one or two fields. Add coin type to One/TwoFieldDepositCoinTypes and make this call again.");
                return null;
            }

            return depositDetails;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetMarginAmountsAvailableToTradeAsync(bool includeZeroBalances)
        {
            Dictionary<string, decimal> marginAmounts = new Dictionary<string, decimal>();

            string url = "/account/getbalances";
            JToken response = await MakeJsonRequestAsync<JToken>(url, null, await GetNoncePayloadAsync());

            var result = response
                .Where(i => includeZeroBalances || i["Available"].ConvertInvariant<decimal>() != 0)
                .ToDictionary(i => i["Currency"].ToStringInvariant(), i => i["Available"].ConvertInvariant<decimal>());

            return result;
        }
    }

    public partial class ExchangeName { public const string Bittrex = "Bittrex"; }
}
