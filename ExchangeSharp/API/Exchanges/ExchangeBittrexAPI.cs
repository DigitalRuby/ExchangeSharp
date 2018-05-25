/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

// if you can't use ASP.NET signalr nuget package, comment this out
#define HAS_SIGNALR

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json.Linq;

#if HAS_SIGNALR

using Microsoft.AspNet.SignalR.Client;

#endif

namespace ExchangeSharp
{
    public sealed class ExchangeBittrexAPI : ExchangeAPI
    {

#if HAS_SIGNALR

        public sealed class BittrexWebSocket : IDisposable
        {
            // https://bittrex.github.io/

            private sealed class BittrexSocketClientConnection : IDisposable
            {
                private BittrexWebSocket client;
                private Action<string> callback;
                private string functionName;

                /// <summary>
                /// Constructor
                /// </summary>
                /// <param name="client">Socket client</param>
                /// <param name="functionName">Function code, uS is supported</param>
                /// <param name="callback"></param>
                /// <param name="param"></param>
                /// <returns>Connection</returns>
                public async Task<IDisposable> OpenAsync(BittrexWebSocket client, string functionName, Action<string> callback, params object[] param)
                {
                    if (callback != null)
                    {
                        string functionFullName = null;
                        switch (functionName)
                        {
                            case "uS": functionFullName = "SubscribeToSummaryDeltas"; break;
                            default: throw new InvalidOperationException("Only 'uS' function is supported");
                        }
                        if (functionFullName != null)
                        {
                            client.AddListener(functionName, callback);
                            bool result = await client.hubProxy.Invoke<bool>(functionFullName, param).ConfigureAwait(false);
                            if (result)
                            {
                                this.client = client;
                                this.callback = callback;
                                this.functionName = functionName;
                                return this;
                            }
                            else
                            {
                                client.RemoveListener(functionName, callback);
                            }
                        }
                    }
                    throw new APIException("Unable to open web socket to Bittrex");
                }

                public void Dispose()
                {
                    try
                    {
                        client.RemoveListener(functionName, callback);
                    }
                    catch
                    {
                    }
                }
            }

            private HubConnection hubConnection;
            private IHubProxy hubProxy;
            private readonly Dictionary<string, List<Action<string>>> listeners = new Dictionary<string, List<Action<string>>>();
            private bool reconnecting;

            private void AddListener(string functionName, Action<string> callback)
            {
                Start();
                lock (listeners)
                {
                    if (!listeners.TryGetValue(functionName, out List<Action<string>> callbacks))
                    {
                        listeners[functionName] = callbacks = new List<Action<string>>();
                    }
                    if (!callbacks.Contains(callback))
                    {
                        callbacks.Add(callback);
                    }
                }
            }

            private void RemoveListener(string functionName, Action<string> callback)
            {
                lock (listeners)
                {
                    if (listeners.TryGetValue(functionName, out List<Action<string>> callbacks))
                    {
                        callbacks.Remove(callback);
                        if (callbacks.Count == 0)
                        {
                            listeners.Remove(functionName);
                        }
                    }
                    if (listeners.Count == 0)
                    {
                        Stop();
                    }
                }
            }

            private void HandleResponse(string functionName, string data)
            {
                data = Decode(data);
                lock (listeners)
                {
                    if (listeners.TryGetValue(functionName, out List<Action<string>> callbacks))
                    {
                        Parallel.ForEach(callbacks, (callback) =>
                        {
                            try
                            {
                                callback(data);
                            }
                            catch
                            {
                            }
                        });
                    }
                }
            }

            private void SocketClosed()
            {
                if (reconnecting || listeners.Count == 0)
                {
                    return;
                }
                reconnecting = true;
                try
                {
                    // if hubConnection is null, exception will throw out
                    while (hubConnection.State != ConnectionState.Connected)
                    {
                        Start();
                    }
                    Task.Delay(1000).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch
                {
                }
                reconnecting = false;
            }

            public BittrexWebSocket()
            {
                const string connectionUrl = "https://socket.bittrex.com";
                hubConnection = new HubConnection(connectionUrl);
                hubConnection.Closed += SocketClosed;
                hubProxy = hubConnection.CreateHubProxy("c2");
                hubProxy.On("uS", (string data) => HandleResponse("uS", data));
            }

            public void Start()
            {
                while (hubConnection.State != ConnectionState.Connected)
                {
                    hubConnection.Start().ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }

            public void Stop()
            {
                hubConnection.Stop(TimeSpan.FromSeconds(1.0));
            }

            ~BittrexWebSocket()
            {
                Dispose();
            }

            public void Dispose()
            {
                if (hubConnection == null)
                {
                    return;
                }
                listeners.Clear();

                // null out hub so we don't try to reconnect
                var tmp = hubConnection;
                hubConnection = null;
                tmp.Stop(TimeSpan.FromSeconds(1.0));
            }

            /// <summary>
            /// Subscribe to all market summaries
            /// </summary>
            /// <param name="callback">Callback</param>
            /// <returns>IDisposable or null if error</returns>
            public IDisposable SubscribeToSummaryDeltas(Action<string> callback)
            {
                BittrexSocketClientConnection client = new BittrexSocketClientConnection();
                return client.OpenAsync(this, "uS", callback).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            // The return of GetAuthContext is a challenge string. Call CreateSignature(apiSecret, challenge)
            // for the response to the challenge, and pass it to Authenticate().
            public async Task<string> GetAuthContext(string apiKey) => await hubProxy.Invoke<string>("GetAuthContext", apiKey);

            public async Task<bool> Authenticate(string apiKey, string signedChallenge) => await hubProxy.Invoke<bool>("Authenticate", apiKey, signedChallenge);

            // Decode converts Bittrex CoreHub2 socket wire protocol data into JSON.
            // Data goes from base64 encoded to gzip (byte[]) to minifed JSON.
            public static string Decode(string wireData)
            {
                // Step 1: Base64 decode the wire data into a gzip blob
                byte[] gzipData = Convert.FromBase64String(wireData);

                // Step 2: Decompress gzip blob into minified JSON
                using (var decompressedStream = new MemoryStream())
                using (var compressedStream = new MemoryStream(gzipData))
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(decompressedStream);
                    decompressedStream.Position = 0;

                    using (var streamReader = new StreamReader(decompressedStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }

            public static string CreateSignature(string apiSecret, string challenge)
            {
                // Get hash by using apiSecret as key, and challenge as data
                var hmacSha512 = new HMACSHA512(Encoding.ASCII.GetBytes(apiSecret));
                var hash = hmacSha512.ComputeHash(Encoding.ASCII.GetBytes(challenge));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private BittrexWebSocket webSocket;

#endif

        public override string BaseUrl { get; set; } = "https://bittrex.com/api/v1.1";
        public override string Name => ExchangeName.Bittrex;
        public string BaseUrl2 { get; set; } = "https://bittrex.com/api/v2.0";

        /// <summary>Coin types that both an address and a tag to make the deposit</summary>
        public HashSet<string> TwoFieldDepositCoinTypes { get; }

        /// <summary>Coin types that only require an address to make the deposit</summary>
        public HashSet<string> OneFieldDepositCoinTypes { get; }

        public ExchangeBittrexAPI()
        {
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
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).ToUpperInvariant();
        }

#if HAS_SIGNALR

        /// <summary>
        /// Gets the BittrexSocketClient for this API
        /// </summary>
        private BittrexWebSocket SocketClient
        {
            get
            {
                if (webSocket == null)
                {
                    lock (this)
                    {
                        if (webSocket == null)
                        {
                            webSocket = new BittrexWebSocket();
                        }
                    }
                }
                return webSocket;
            }
        }

#endif

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
            order.Result = amountFilled == amount ? ExchangeAPIOrderResult.Filled : (amountFilled == 0 ? ExchangeAPIOrderResult.Pending : ExchangeAPIOrderResult.FilledPartially);
            order.OrderDate = ConvertDateTimeInvariant(token["Opened"], ConvertDateTimeInvariant(token["TimeStamp"]));
            order.Symbol = token["Exchange"].ToStringInvariant();
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

        protected override void OnDispose()
        {

#if HAS_SIGNALR

            if (webSocket != null)
            {
                webSocket.Dispose();
                webSocket = null;
            }

#endif

        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
                var query = HttpUtility.ParseQueryString(url.Query);
                url.Query = "apikey=" + PublicApiKey.ToUnsecureString() + "&nonce=" + payload["nonce"].ToStringInvariant() + (query.Count == 0 ? string.Empty : "&" + query.ToString());
            }
            return url.Uri;
        }

        protected override void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                string url = request.RequestUri.ToString();
                string sign = CryptoUtility.SHA512Sign(url, PrivateApiKey.ToUnsecureString());
                request.Headers["apisign"] = sign;
            }
        }

        protected override async Task<IReadOnlyDictionary<string, ExchangeCurrency>> OnGetCurrenciesAsync()
        {
            var currencies = new Dictionary<string, ExchangeCurrency>(StringComparer.OrdinalIgnoreCase);

            JObject obj = await MakeJsonRequestAsync<JObject>("/public/getcurrencies");
            JToken result = CheckError(obj);
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    var coin = new ExchangeCurrency
                    {
                        BaseAddress = token["BaseAddress"].ToStringInvariant(),
                        CoinType = token["CoinType"].ToStringInvariant(),
                        FullName = token["CurrencyLong"].ToStringInvariant(),
                        IsEnabled = token["IsActive"].ConvertInvariant<bool>(),
                        MinConfirmations = token["MinConfirmation"].ConvertInvariant<int>(),
                        Name = token["Currency"].ToStringUpperInvariant(),
                        Notes = token["Notice"].ToStringInvariant(),
                        TxFee = token["TxFee"].ConvertInvariant<decimal>(),
                    };

                    currencies[coin.Name] = coin;
                }
            }

            return currencies;
        }

        /// <summary>
        /// Get exchange symbols including available metadata such as min trade size and whether the market is active
        /// </summary>
        /// <returns>Collection of ExchangeMarkets</returns>
        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            var markets = new List<ExchangeMarket>();
            JObject obj = await MakeJsonRequestAsync<JObject>("/public/getmarkets");
            JToken result = CheckError(obj);

            // StepSize is 8 decimal places for both price and amount on everything at Bittrex
            const decimal StepSize = 0.00000001m;
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    var market = new ExchangeMarket
                    {
                        BaseCurrency = token["BaseCurrency"].ToStringUpperInvariant(),
                        IsActive = token["IsActive"].ConvertInvariant<bool>(),
                        MarketCurrency = token["MarketCurrency"].ToStringUpperInvariant(),
                        MarketName = token["MarketName"].ToStringUpperInvariant(),
                        MinTradeSize = token["MinTradeSize"].ConvertInvariant<decimal>(),
                        MinPrice = StepSize,
                        PriceStepSize = StepSize,
                        QuantityStepSize = StepSize
                    };

                    markets.Add(market);
                }
            }

            return markets;
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            return (await GetSymbolsMetadataAsync()).Select(x => x.MarketName);
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            JObject obj = await MakeJsonRequestAsync<JObject>("/public/getmarketsummary?market=" + NormalizeSymbol(symbol));
            JToken result = CheckError(obj);
            JToken ticker = result[0];
            if (ticker != null)
            {
                return new ExchangeTicker
                {
                    Ask = ticker["Ask"].ConvertInvariant<decimal>(),
                    Bid = ticker["Bid"].ConvertInvariant<decimal>(),
                    Last = ticker["Last"].ConvertInvariant<decimal>(),
                    Volume = new ExchangeVolume
                    {
                        BaseVolume = ticker["Volume"].ConvertInvariant<decimal>(),
                        BaseSymbol = symbol,
                        ConvertedVolume = ticker["BaseVolume"].ConvertInvariant<decimal>(),
                        ConvertedSymbol = symbol,
                        Timestamp = ConvertDateTimeInvariant(ticker["TimeStamp"])
                    }
                };
            }
            return null;
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            JObject obj = await MakeJsonRequestAsync<Newtonsoft.Json.Linq.JObject>("public/getmarketsummaries");
            JToken tickers = CheckError(obj);
            string symbol;
            List<KeyValuePair<string, ExchangeTicker>> tickerList = new List<KeyValuePair<string, ExchangeTicker>>();
            foreach (JToken ticker in tickers)
            {
                symbol = ticker["MarketName"].ToStringInvariant();
                ExchangeTicker tickerObj = new ExchangeTicker
                {
                    Ask = ticker["Ask"].ConvertInvariant<decimal>(),
                    Bid = ticker["Bid"].ConvertInvariant<decimal>(),
                    Last = ticker["Last"].ConvertInvariant<decimal>(),
                    Volume = new ExchangeVolume
                    {
                        BaseVolume = ticker["BaseVolume"].ConvertInvariant<decimal>(),
                        BaseSymbol = symbol,
                        ConvertedVolume = ticker["Volume"].ConvertInvariant<decimal>(),
                        ConvertedSymbol = symbol,
                        Timestamp = ConvertDateTimeInvariant(ticker["TimeStamp"])
                    }
                };
                tickerList.Add(new KeyValuePair<string, ExchangeTicker>(symbol, tickerObj));
            }
            return tickerList;
        }

#if HAS_SIGNALR

        public override IDisposable GetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            if (callback == null)
            {
                return null;
            }

            void innerCallback(string json)
            {
                /*
                {
                    Nonce : int,
                    Deltas : 
                    [
                        {
                            MarketName     : string,
                            High           : decimal,
                            Low            : decimal,
                            Volume         : decimal,
                            Last           : decimal,
                            BaseVolume     : decimal,
                            TimeStamp      : date,
                            Bid            : decimal,
                            Ask            : decimal,
                            OpenBuyOrders  : int,
                            OpenSellOrders : int,
                            PrevDay        : decimal,
                            Created        : date
                        }
                    ]
                }
                */

                // Convert Bittrex.Net tickers objects into ExchangeSharp ExchangeTickers
                var freshTickers = new Dictionary<string, ExchangeTicker>(StringComparer.OrdinalIgnoreCase);
                JToken token = JToken.Parse(json);
                token = token["D"];
                foreach (JToken ticker in token)
                {
                    string marketName = ticker["M"].ToStringInvariant();
                    decimal last = ticker["l"].ConvertInvariant<decimal>();
                    decimal ask = ticker["A"].ConvertInvariant<decimal>();
                    decimal bid = ticker["B"].ConvertInvariant<decimal>();
                    decimal volume = ticker["V"].ConvertInvariant<decimal>();
                    decimal baseVolume = ticker["m"].ConvertInvariant<decimal>();
                    DateTime timestamp = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(ticker["T"].ConvertInvariant<long>());
                    var t = new ExchangeTicker
                    {
                        Ask = ask,
                        Bid = bid,
                        Last = last,
                        Volume = new ExchangeVolume
                        {
                            ConvertedVolume = volume,
                            ConvertedSymbol = marketName,
                            BaseVolume = baseVolume,
                            BaseSymbol = marketName,
                            Timestamp = timestamp
                        }
                    };
                    freshTickers[marketName] = t;
                }
                callback(freshTickers);
            }
            var client = SocketClient;
            return client.SubscribeToSummaryDeltas(innerCallback);
        }

#endif

        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string symbol, int maxCount = 100)
        {
            symbol = NormalizeSymbol(symbol);
            JObject obj = await MakeJsonRequestAsync<Newtonsoft.Json.Linq.JObject>("public/getorderbook?market=" + symbol + "&type=both&limit_bids=" + maxCount + "&limit_asks=" + maxCount);
            JToken book = CheckError(obj);
            ExchangeOrderBook orders = new ExchangeOrderBook();
            JToken bids = book["buy"];
            foreach (JToken token in bids)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["Quantity"].ConvertInvariant<decimal>(), Price = token["Rate"].ConvertInvariant<decimal>() };
                orders.Bids.Add(order);
            }
            JToken asks = book["sell"];
            foreach (JToken token in asks)
            {
                ExchangeOrderPrice order = new ExchangeOrderPrice { Amount = token["Quantity"].ConvertInvariant<decimal>(), Price = token["Rate"].ConvertInvariant<decimal>() };
                orders.Asks.Add(order);
            }
            return orders;
        }

        /// <summary>Gets the deposit history for a symbol</summary>
        /// <param name="symbol">The symbol to check. May be null.</param>
        /// <returns>Collection of ExchangeTransactions</returns>
        protected override async Task<IEnumerable<ExchangeTransaction>> OnGetDepositHistoryAsync(string symbol)
        {
            var transactions = new List<ExchangeTransaction>();
            symbol = NormalizeSymbol(symbol);

            string url = $"/account/getdeposithistory{(string.IsNullOrWhiteSpace(symbol) ? string.Empty : $"?currency={symbol}")}";
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            JToken result = obj["result"];

            foreach (var token in result)
            {
                var deposit = new ExchangeTransaction
                {
                    Amount = token["Amount"].ConvertInvariant<decimal>(),
                    Address = token["CryptoAddress"].ToStringInvariant(),
                    Symbol = token["Currency"].ToStringInvariant(),
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

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string symbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            // TODO: sinceDateTime is ignored
            // https://bittrex.com/Api/v2.0/pub/market/GetTicks?marketName=BTC-WAVES&tickInterval=oneMin&_=1499127220008
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/pub/market/GetTicks?marketName=" + symbol + "&tickInterval=oneMin";
            string url;
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            while (true)
            {
                url = baseUrl;
                if (startDate != null)
                {
                    url += "&_=" + DateTime.UtcNow.Ticks;
                }
                JObject obj = await MakeJsonRequestAsync<JObject>(url, BaseUrl2);
                JToken result = CheckError(obj);
                JArray array = result as JArray;
                if (array == null || array.Count == 0)
                {
                    break;
                }
                if (startDate != null)
                {
                    startDate = ConvertDateTimeInvariant(array.Last["T"]);
                }
                foreach (JToken trade in array)
                {
                    // {"O":0.00106302,"H":0.00106302,"L":0.00106302,"C":0.00106302,"V":80.58638589,"T":"2017-08-18T17:48:00","BV":0.08566493}
                    trades.Add(new ExchangeTrade
                    {
                        Amount = trade["V"].ConvertInvariant<decimal>(),
                        Price = trade["C"].ConvertInvariant<decimal>(),
                        Timestamp = ConvertDateTimeInvariant(trade["T"]),
                        Id = -1,
                        IsBuy = true
                    });
                }
                trades.Sort((t1, t2) => t1.Timestamp.CompareTo(t2.Timestamp));
                if (!callback(trades))
                {
                    break;
                }
                trades.Clear();
                if (startDate == null)
                {
                    break;
                }
                Task.Delay(1000).Wait();
            }
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string symbol)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            symbol = NormalizeSymbol(symbol);
            string baseUrl = "/public/getmarkethistory?market=" + symbol;
            JObject obj = await MakeJsonRequestAsync<JObject>(baseUrl);
            JToken result = CheckError(obj);
            if (result is JArray array && array.Count != 0)
            {
                foreach (JToken token in array)
                {
                    trades.Add(new ExchangeTrade
                    {
                        Amount = token["Quantity"].ConvertInvariant<decimal>(),
                        IsBuy = token["OrderType"].ToStringUpperInvariant() == "BUY",
                        Price = token["Price"].ConvertInvariant<decimal>(),
                        Timestamp = ConvertDateTimeInvariant(token["TimeStamp"]),
                        Id = token["Id"].ConvertInvariant<long>()
                    });
                }
            }

            return trades;
        }

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string symbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            if (limit != null)
            {
                throw new APIException("Limit parameter not supported");
            }

            // https://bittrex.com/Api/v2.0/pub/market/GetTicks?marketName=BTC-WAVES&tickInterval=day
            // "{"success":true,"message":"","result":[{"O":0.00011000,"H":0.00060000,"L":0.00011000,"C":0.00039500,"V":5904999.37958770,"T":"2016-06-20T00:00:00","BV":2212.16809610} ] }"
            string periodString;
            switch (periodSeconds)
            {
                case 60: periodString = "oneMin"; break;
                case 300: periodString = "fiveMin"; break;
                case 1800: periodString = "thirtyMin"; break;
                case 3600: periodString = "hour"; break;
                case 86400: periodString = "day"; break;
                case 259200: periodString = "threeDay"; break;
                case 604800: periodString = "week"; break;
                default:
                    if (periodSeconds > 604800)
                    {
                        periodString = "month";
                    }
                    else
                    {
                        throw new ArgumentException("Period seconds must be one of 60 (min), 300 (fiveMin), 1800 (thirtyMin), 3600 (hour), 86400 (day), 259200 (threeDay), 604800 (week), 2419200 (month)");
                    }
                    break;
            }
            List<MarketCandle> candles = new List<MarketCandle>();
            symbol = NormalizeSymbol(symbol);
            endDate = endDate ?? DateTime.UtcNow;
            startDate = startDate ?? endDate.Value.Subtract(TimeSpan.FromDays(1.0));
            JToken result = await MakeJsonRequestAsync<JToken>("pub/market/GetTicks?marketName=" + symbol + "&tickInterval=" + periodString, BaseUrl2);
            result = CheckError(result);
            if (result is JArray array)
            {
                foreach (JToken jsonCandle in array)
                {
                    MarketCandle candle = new MarketCandle
                    {
                        ClosePrice = jsonCandle["C"].ConvertInvariant<decimal>(),
                        ExchangeName = Name,
                        HighPrice = jsonCandle["H"].ConvertInvariant<decimal>(),
                        LowPrice = jsonCandle["L"].ConvertInvariant<decimal>(),
                        Name = symbol,
                        OpenPrice = jsonCandle["O"].ConvertInvariant<decimal>(),
                        PeriodSeconds = periodSeconds,
                        Timestamp = ConvertDateTimeInvariant(jsonCandle["T"]),
                        BaseVolume = jsonCandle["BV"].ConvertInvariant<double>(),
                        ConvertedVolume = jsonCandle["V"].ConvertInvariant<double>()
                    };
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
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    decimal amount = token["Balance"].ConvertInvariant<decimal>();
                    if (amount > 0m)
                    {
                        currencies.Add(token["Currency"].ToStringInvariant(), amount);
                    }
                }
            }
            return currencies;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string url = "/account/getbalances";
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            if (result is JArray array)
            {
                foreach (JToken token in array)
                {
                    decimal amount = token["Available"].ConvertInvariant<decimal>();
                    if (amount > 0m)
                    {
                        currencies.Add(token["Currency"].ToStringInvariant(), amount);
                    }
                }
            }
            return currencies;
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == OrderType.Market)
            {
                throw new NotSupportedException("Order type " + order.OrderType + " not supported");
            }

            string symbol = NormalizeSymbol(order.Symbol);

            decimal orderAmount = await ClampOrderQuantity(symbol, order.Amount);
            decimal orderPrice = await ClampOrderPrice(symbol, order.Price);

            string url = (order.IsBuy ? "/market/buylimit" : "/market/selllimit") + "?market=" + symbol + "&quantity=" +
                orderAmount.ToStringInvariant() + "&rate=" + orderPrice.ToStringInvariant();
            foreach (var kv in order.ExtraParameters)
            {
                url += "&" + WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value.ToStringInvariant());
            }
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            string orderId = result["uuid"].ToStringInvariant();
            return new ExchangeOrderResult { Amount = orderAmount, IsBuy = order.IsBuy, OrderDate = DateTime.UtcNow, OrderId = orderId, Result = ExchangeAPIOrderResult.Pending, Symbol = symbol };
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string symbol = null)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            string url = "/account/getorder?uuid=" + orderId;
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
            return ParseOrder(result);
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string symbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            string url = "/market/getopenorders" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "?market=" + NormalizeSymbol(symbol));
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            CheckError(obj);
            JToken result = obj["result"];
            foreach (JToken token in result.Children())
            {
                orders.Add(ParseOrder(token));
            }

            return orders;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetCompletedOrderDetailsAsync(string symbol = null, DateTime? afterDate = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            string url = "/account/getorderhistory" + (string.IsNullOrWhiteSpace(symbol) ? string.Empty : "?market=" + NormalizeSymbol(symbol));
            JObject obj = await MakeJsonRequestAsync<JObject>(url, null, GetNoncePayload());
            JToken result = CheckError(obj);
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

            string url = $"/account/withdraw?currency={NormalizeSymbol(withdrawalRequest.Symbol)}&quantity={withdrawalRequest.Amount.ToStringInvariant()}&address={withdrawalRequest.Address}";
            if (!string.IsNullOrWhiteSpace(withdrawalRequest.AddressTag))
            {
                url += $"&paymentid={withdrawalRequest.AddressTag}";
            }

            JToken response = await MakeJsonRequestAsync<JToken>(url, null, GetNoncePayload());
            JToken result = CheckError(response);

            ExchangeWithdrawalResponse withdrawalResponse = new ExchangeWithdrawalResponse
            {
                Id = result["uuid"].ToStringInvariant(),
                Message = result["msg"].ToStringInvariant()
            };

            return withdrawalResponse;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string symbol = null)
        {
            JObject obj = await MakeJsonRequestAsync<JObject>("/market/cancel?uuid=" + orderId, null, GetNoncePayload());
            CheckError(obj);
        }

        /// <summary>
        /// Gets the address to deposit to and applicable details.
        /// If one does not exist, the call will fail and return ADDRESS_GENERATING until one is available.
        /// </summary>
        /// <param name="symbol">Symbol to get address for.</param>
        /// <param name="forceRegenerate">(ignored) Bittrex does not support regenerating deposit addresses.</param>
        /// <returns>
        /// Deposit address details (including tag if applicable, such as with XRP)
        /// </returns>
        protected override async Task<ExchangeDepositDetails> OnGetDepositAddressAsync(string symbol, bool forceRegenerate = false)
        {
            IReadOnlyDictionary<string, ExchangeCurrency> updatedCurrencies = (await GetCurrenciesAsync());

            string url = "/account/getdepositaddress?currency=" + NormalizeSymbol(symbol);
            JToken response = await MakeJsonRequestAsync<JToken>(url, null, GetNoncePayload());
            JToken result = CheckError(response);

            // NOTE API 1.1 does not include the the static wallet address for currencies with tags such as XRP & NXT (API 2.0 does!)
            // We are getting the static addresses via the GetCurrencies() api.
            ExchangeDepositDetails depositDetails = new ExchangeDepositDetails
            {
                Symbol = result["Currency"].ToStringInvariant(),
            };

            if (!updatedCurrencies.TryGetValue(depositDetails.Symbol, out ExchangeCurrency coin))
            {
                Console.WriteLine($"Unable to find {depositDetails.Symbol} in existing list of coins.");
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
                Console.WriteLine($"ExchangeBittrexAPI: Unknown coin type {coin.CoinType} must be registered as requiring one or two fields. Add coin type to One/TwoFieldDepositCoinTypes and make this call again.");
                return null;
            }

            return depositDetails;
        }
    }
}
