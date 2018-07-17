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
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;

#if HAS_SIGNALR

using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Http;
using Microsoft.AspNet.SignalR.Client.Transports;
using Microsoft.AspNet.SignalR.Client.Infrastructure;

#endif

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public partial class ExchangeBittrexAPI
    {

#if HAS_SIGNALR

        public sealed class BittrexWebSocket
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
                public async Task OpenAsync(BittrexWebSocket client, string functionName, Action<string> callback, params object[] param)
                {
                    if (callback != null)
                    {
                        string functionFullName;
                        switch (functionName)
                        {
                            case "uS": functionFullName = "SubscribeToSummaryDeltas"; break;
                            case "uE": functionFullName = "SubscribeToExchangeDeltas"; break;
                            default: throw new InvalidOperationException("Only 'uS', 'uE' function is supported");
                        }

                        client.AddListener(functionName, callback);
                        bool result = await client.hubProxy.Invoke<bool>(functionFullName, param);
                        if (result)
                        {
                            this.client = client;
                            this.callback = callback;
                            this.functionName = functionName;
                            return;
                        }

                        client.RemoveListener(functionName, callback);
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

            private sealed class WebsocketCustomTransport : ClientTransportBase
            {
                private IConnection connection;
                private string connectionData;
                private WebSocketWrapper webSocket;

                public override bool SupportsKeepAlive => true;

                public WebsocketCustomTransport(IHttpClient client) : base(client, "webSockets")
                {
                }

                ~WebsocketCustomTransport()
                {
                    Dispose(false);
                }

                protected override void OnStart(IConnection con, string conData, CancellationToken disconToken)
                {
                    connection = con;
                    connectionData = conData;

                    var connectUrl = UrlBuilder.BuildConnect(connection, Name, connectionData);

                    if (webSocket != null)
                    {
                        DisposeWebSocket();
                    }

                    // SignalR uses https, websocket4net uses wss
                    connectUrl = connectUrl.Replace("http://", "ws://").Replace("https://", "wss://");

                    IDictionary<string, string> cookies = new Dictionary<string, string>();
                    if (connection.CookieContainer != null)
                    {
                        var container = connection.CookieContainer.GetCookies(new Uri(connection.Url));
                        foreach (Cookie cookie in container)
                        {
                            cookies.Add(cookie.Name, cookie.Value);
                        }
                    }

                    webSocket = new WebSocketWrapper(connectUrl, WebSocketOnMessageReceived);
                }

                protected override void OnStartFailed()
                {
                    Dispose();
                }

                public override async Task Send(IConnection con, string data, string conData)
                {
                    await webSocket.SendMessageAsync(data);
                }

                public override void LostConnection(IConnection con)
                {
                    // TODO: If we are going to stop the connection we need to restart it somewhere else
                    //connection.Stop();
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        if (webSocket != null)
                        {
                            DisposeWebSocket();
                        }
                    }

                    base.Dispose(disposing);
                }

                private void DisposeWebSocket()
                {
                    webSocket.Dispose();
                    webSocket = null;
                }

                private void WebSocketOnClosed()
                {
                    connection.Stop();
                }

                private void WebSocketOnError(Exception e)
                {
                    connection.OnError(e);
                }

                private void WebSocketOnMessageReceived(byte[] data, WebSocketWrapper _webSocket)
                {
                    string dataText = CryptoUtility.UTF8EncodingNoPrefix.GetString(data);
                    ProcessResponse(connection, dataText);
                }
            }

            private HubConnection hubConnection;
            private IHubProxy hubProxy;
            private readonly Dictionary<string, List<Action<string>>> listeners = new Dictionary<string, List<Action<string>>>();
            private bool reconnecting;

            private void AddListener(string functionName, Action<string> callback)
            {
                StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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
                        StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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
                hubProxy.On("uE", (string data) => HandleResponse("uE", data));
            }

            public async Task StartAsync()
            {
                DefaultHttpClient client = new DefaultHttpClient();
                var autoTransport = new AutoTransport(client, new IClientTransport[] { new WebsocketCustomTransport(client) });
                hubConnection.TransportConnectTimeout = hubConnection.DeadlockErrorTimeout = TimeSpan.FromSeconds(10.0);
                await hubConnection.Start(autoTransport);
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
                try
                {
                    tmp.Transport.Dispose();
                    tmp.Dispose();
                }
                catch
                {
                    // eat exceptions here, we don't care if it fails
                }
            }

            /// <summary>
            /// Subscribe to all market summaries
            /// </summary>
            /// <param name="callback">Callback</param>
            /// <returns>IDisposable to close the socket</returns>
            public IDisposable SubscribeToSummaryDeltas(Action<string> callback)
            {
                BittrexSocketClientConnection conn = new BittrexSocketClientConnection();
                conn.OpenAsync(this, "uS", callback).ConfigureAwait(false).GetAwaiter().GetResult();
                return conn;
            }

            /// <summary>
            /// Subscribe to order book updates
            /// </summary>
            /// <param name="callback">Callback</param>
            /// <param name="ticker">The ticker to subscribe to</param>
            /// <returns>IDisposable to close the socket</returns>
            public IDisposable SubscribeToExchangeDeltas(Action<string> callback, string ticker)
            {
                BittrexSocketClientConnection conn = new BittrexSocketClientConnection();
                conn.OpenAsync(this, "uE", callback, ticker).ConfigureAwait(false).GetAwaiter().GetResult();
                return conn;
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
                var hmacSha512 = new HMACSHA512(CryptoUtility.UTF8EncodingNoPrefix.GetBytes(apiSecret));
                var hash = hmacSha512.ComputeHash(CryptoUtility.UTF8EncodingNoPrefix.GetBytes(challenge));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private BittrexWebSocket webSocket;

        protected override IDisposable OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> callback)
        {
            if (callback == null)
            {
                return null;
            }

            void innerCallback(string json)
            {
                #region sample json
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
                #endregion

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

        protected override IDisposable OnGetOrderBookDeltasWebSocket(
            Action<ExchangeOrderBook> callback,
            int maxCount = 20,
            params string[] symbols)
        {
            if (callback == null || symbols == null || !symbols.Any())
            {
                return null;
            }

            void innerCallback(string json)
            {
                #region sample json
                /*
                    {
                        MarketName : string,
                        Nonce      : int,
                        Buys: 
                        [
                            {
                                Type     : int,
                                Rate     : decimal,
                                Quantity : decimal
                            }
                        ],
                        Sells: 
                        [
                            {
                                Type     : int,
                                Rate     : decimal,
                                Quantity : decimal
                            }
                        ],
                        Fills: 
                        [
                            {
                                FillId    : int,
                                OrderType : string,
                                Rate      : decimal,
                                Quantity  : decimal,
                                TimeStamp : date
                            }
                        ]
                    }
                */
                #endregion

                var ordersUpdates = JsonConvert.DeserializeObject<BittrexStreamUpdateExchangeState>(json);
                var book = new ExchangeOrderBook();
                foreach (BittrexStreamOrderBookUpdateEntry ask in ordersUpdates.Sells)
                {
                    var depth = new ExchangeOrderPrice { Price = ask.Rate, Amount = ask.Quantity };
                    book.Asks[depth.Price] = depth;
                }

                foreach (BittrexStreamOrderBookUpdateEntry bid in ordersUpdates.Buys)
                {
                    var depth = new ExchangeOrderPrice { Price = bid.Rate, Amount = bid.Quantity };
                    book.Bids[depth.Price] = depth;
                }

                book.Symbol = ordersUpdates.MarketName;
                book.SequenceId = ordersUpdates.Nonce;
                callback(book);
            }

            IDisposable client = null;
            foreach (var sym in symbols)
            {
                client = this.SocketClient.SubscribeToExchangeDeltas(innerCallback, sym);
            }

            return client;
        }

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
    }
}
