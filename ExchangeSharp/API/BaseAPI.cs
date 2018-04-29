/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Type of nonce styles
    /// </summary>
    public enum NonceStyle
    {
        /// <summary>
        /// Ticks (int64)
        /// </summary>
        Ticks,

        /// <summary>
        /// Ticks (string)
        /// </summary>
        TicksString,

        /// <summary>
        /// Milliseconds (int64)
        /// </summary>
        UnixMilliseconds,

        /// <summary>
        /// Milliseconds (string)
        /// </summary>
        UnixMillisecondsString,

        /// <summary>
        /// Seconds (double)
        /// </summary>
        UnixSeconds,

        /// <summary>
        /// Seconds (string)
        /// </summary>
        UnixSecondsString
    }

    /// <summary>
    /// API base class functionality
    /// </summary>
    public abstract class BaseAPI : IAPIRequestHandler
    {
        /// <summary>
        /// User agent for requests
        /// </summary>
        public const string RequestUserAgent = "ExchangeSharp (https://github.com/jjxtra/ExchangeSharp)";

        private IAPIRequestMaker requestMaker;
        /// <summary>
        /// API request maker
        /// </summary>
        public IAPIRequestMaker RequestMaker
        {
            get { return requestMaker; }
            set { requestMaker = value ?? new APIRequestMaker(this); }

        }
        /// <summary>
        /// Base URL for the API
        /// </summary>
        public abstract string BaseUrl { get; set; }

        /// <summary>
        /// Base URL for the API for web sockets
        /// </summary>
        public virtual string BaseUrlWebSocket { get; set; }

        /// <summary>
        /// Gets the name of the API
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Public API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString PublicApiKey { get; set; }

        /// <summary>
        /// Private API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString PrivateApiKey { get; set; }

        /// <summary>
        /// Pass phrase API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// Most services do not require this, but GDAX is an example of one that does
        /// </summary>
        public System.Security.SecureString Passphrase { get; set; }

        /// <summary>
        /// Rate limiter - set this to a new limit if you are seeing your ip get blocked by the API
        /// </summary>
        public RateGate RateLimit { get; set; } = new RateGate(5, TimeSpan.FromSeconds(15.0d));

        /// <summary>
        /// Default request method
        /// </summary>
        public string RequestMethod { get; set; } = "GET";

        /// <summary>
        /// Content type for requests
        /// </summary>
        public string RequestContentType { get; set; } = "text/plain";

        /// <summary>
        /// Timeout for requests
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30.0);

        /// <summary>
        /// Request window - most services do not use this, but Binance API is an example of one that does
        /// </summary>
        public TimeSpan RequestWindow { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Type of nonce
        /// </summary>
        public NonceStyle NonceStyle { get; protected set; } = NonceStyle.Ticks;

        /// <summary>
        /// Offset for nonce calculation, some exchanges like Binance have a problem with requests being in the future, so you can offset the current DateTime with this
        /// </summary>
        public TimeSpan NonceOffset { get; set; }

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        public System.Net.Cache.RequestCachePolicy RequestCachePolicy { get; set; } = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

        private readonly Dictionary<string, KeyValuePair<DateTime, object>> cache = new Dictionary<string, KeyValuePair<DateTime, object>>(StringComparer.OrdinalIgnoreCase);

        private decimal lastNonce;

        /// <summary>
        /// Static constructor
        /// </summary>
        static BaseAPI()
        {

#pragma warning disable CS0618

            try
            {

#if HAS_WINDOWS_FORMS // NET47

                ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

#else

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;

#endif

            }
            catch
            {

            }

#pragma warning restore CS0618

        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BaseAPI()
        {
            requestMaker = new APIRequestMaker(this);
        }

        /// <summary>
        /// Generate a nonce
        /// </summary>
        /// <returns>Nonce</returns>
        public object GenerateNonce()
        {
            // exclusive lock, no two nonces must match
            lock (this)
            {
                object nonce;

                while (true)
                {
                    // some API (Binance) have a problem with requests being after server time, subtract of one second fixes it
                    DateTime now = DateTime.UtcNow - NonceOffset;
                    Task.Delay(1).Wait();

                    switch (NonceStyle)
                    {
                        case NonceStyle.Ticks:
                            nonce = now.Ticks;
                            break;

                        case NonceStyle.TicksString:
                            nonce = now.Ticks.ToStringInvariant();
                            break;

                        case NonceStyle.UnixMilliseconds:
                            nonce = (long)now.UnixTimestampFromDateTimeMilliseconds();
                            break;

                        case NonceStyle.UnixMillisecondsString:
                            nonce = ((long)now.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
                            break;

                        case NonceStyle.UnixSeconds:
                            nonce = now.UnixTimestampFromDateTimeSeconds();
                            break;

                        case NonceStyle.UnixSecondsString:
                            nonce = now.UnixTimestampFromDateTimeSeconds().ToStringInvariant();
                            break;

                        default:
                            throw new InvalidOperationException("Invalid nonce style: " + NonceStyle);
                    }

                    // check for duplicate nonce
                    decimal convertedNonce = nonce.ConvertInvariant<decimal>();
                    if (lastNonce != convertedNonce)
                    {
                        lastNonce = convertedNonce;
                        break;
                    }
                }

                return nonce;
            }
        }

        /// <summary>
        /// Load API keys from an encrypted file - keys will stay encrypted in memory
        /// </summary>
        /// <param name="encryptedFile">Encrypted file to load keys from</param>
        public void LoadAPIKeys(string encryptedFile)
        {
            SecureString[] strings = CryptoUtility.LoadProtectedStringsFromFile(encryptedFile);
            if (strings.Length < 2)
            {
                throw new InvalidOperationException("Encrypted keys file should have at least a public and private key, and an optional pass phrase");
            }
            PublicApiKey = strings[0];
            PrivateApiKey = strings[1];
            if (strings.Length > 2)
            {
                Passphrase = strings[2];
            }
        }

        /// <summary>
        /// Load API keys from unsecure strings
        /// </summary>
        /// <param name="publicApiKey">Public Api Key</param>
        /// <param name="privateApiKey">Private Api Key</param>
        /// <param name="passPhrase">Pass phrase, null for none</param>
        public void LoadAPIKeysUnsecure(string publicApiKey, string privateApiKey, string passPhrase = null)
        {
            PublicApiKey = publicApiKey.ToSecureString();
            PrivateApiKey = privateApiKey.ToSecureString();
            Passphrase = passPhrase?.ToSecureString();
        }

        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        public string MakeRequest(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null)
        {
            return MakeRequestAsync(url, baseUrl, payload, method).GetAwaiter().GetResult();
        }

        /// <summary>
        /// ASYNC - Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        public Task<string> MakeRequestAsync(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null) => requestMaker.MakeRequestAsync(url, baseUrl: baseUrl, payload: payload, method: method);

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        public T MakeJsonRequest<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null)
        {
            return MakeJsonRequestAsync<T>(url, baseUrl, payload, requestMethod).GetAwaiter().GetResult();
        }

        /// <summary>
        /// ASYNC - Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        public async Task<T> MakeJsonRequestAsync<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null)
        {
            await new SynchronizationContextRemover();

            string result = await MakeRequestAsync(url, baseUrl: baseUrl, payload: payload, method: requestMethod);
            return JsonConvert.DeserializeObject<T>(result);
        }

        /// <summary>
        /// Connect a web socket to a path on the API and start listening, not all exchanges support this
        /// </summary>
        /// <param name="url">The sub url for the web socket, or null for none</param>
        /// <param name="messageCallback">Callback for messages</param>
        /// <param name="connectCallback">Connect callback</param>
        /// <returns>Web socket - dispose of the wrapper to shutdown the socket</returns>
        public WebSocketWrapper ConnectWebSocket(string url, System.Action<string, WebSocketWrapper> messageCallback, System.Action<WebSocketWrapper> connectCallback = null)
        {
            string fullUrl = BaseUrlWebSocket + (url ?? string.Empty);
            return new WebSocketWrapper(fullUrl, messageCallback, TimeSpan.FromSeconds(30.0), connectCallback);
        }

        /// <summary>
        /// Whether the API can make authenticated (private) API requests
        /// </summary>
        /// <param name="payload">Payload to potentially send</param>
        /// <returns>True if an authenticated request can be made with the payload, false otherwise</returns>
        protected virtual bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
        {
            return (PrivateApiKey != null && PublicApiKey != null && payload != null && payload.ContainsKey("nonce"));
        }

        /// <summary>
        /// Additional handling for request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        protected virtual void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {

        }

        /// <summary>
        /// Additional handling for response
        /// </summary>
        /// <param name="response">Response</param>
        protected virtual void ProcessResponse(HttpWebResponse response)
        {

        }

        /// <summary>
        /// Process a request url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="payload">Payload</param>
        /// <returns>Updated url</returns>
        protected virtual Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            return url.Uri;
        }

        /// <summary>
        /// Get a form for a request (form-encoded, like a query string)
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <param name="includeNonce">Whether to add the nonce</param>
        /// <returns>Form string</returns>
        protected string GetFormForPayload(Dictionary<string, object> payload, bool includeNonce = true)
        {
            if (payload != null && payload.Count != 0)
            {
                StringBuilder form = new StringBuilder();
                foreach (KeyValuePair<string, object> keyValue in payload)
                {
                    if (keyValue.Key != null && keyValue.Value != null && (includeNonce || keyValue.Key != "nonce"))
                    {
                        form.AppendFormat("{0}={1}&", Uri.EscapeDataString(keyValue.Key), Uri.EscapeDataString(keyValue.Value.ToStringInvariant()));
                    }
                }
                if (form.Length != 0)
                {
                    form.Length--; // trim ampersand
                }
                return form.ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Convert a payload into json
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <returns>Json string</returns>
        protected string GetJsonForPayload(Dictionary<string, object> payload)
        {
            if (payload != null && payload.Count != 0)
            {
                return JsonConvert.SerializeObject(payload);
            }
            return string.Empty;
        }

        /// <summary>
        /// Write a form to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="form">Form to write</param>
        protected void WriteFormToRequest(HttpWebRequest request, string form)
        {
            if (!string.IsNullOrEmpty(form))
            {
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII))
                {
                    writer.Write(form);
                }
            }
        }

        /// <summary>
        /// Write a payload to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        /// <returns>The form string that was written</returns>
        protected string WritePayloadToRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            string form = GetFormForPayload(payload);
            WriteFormToRequest(request, form);
            return form;
        }

        /// <summary>
        /// Read a value from the cache
        /// </summary>
        /// <typeparam name="T">Type to read</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>True if read, false if not. If false, value is default(T).</returns>
        protected bool ReadCache<T>(string key, out T value)
        {
            lock (cache)
            {
                if (cache.TryGetValue(key, out KeyValuePair<DateTime, object> cacheValue))
                {
                    // if not expired, return
                    if (cacheValue.Key > DateTime.UtcNow)
                    {
                        value = (T)cacheValue.Value;
                        return true;
                    }
                    cache.Remove(key);
                }
            }
            value = default(T);
            return false;
        }

        /// <summary>
        /// Write a value to the cache
        /// </summary>
        /// <typeparam name="T">Type of value</typeparam>
        /// <param name="key">Key</param>
        /// <param name="expiration">Expiration from now</param>
        /// <param name="value">Value</param>
        protected void WriteCache<T>(string key, TimeSpan expiration, T value)
        {
            lock (cache)
            {
                cache[key] = new KeyValuePair<DateTime, object>(DateTime.UtcNow + expiration, value);
            }
        }

        /// <summary>
        /// Get a dictionary with a nonce key and value of the required nonce type
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Dictionary with nonce</returns>
        protected Dictionary<string, object> GetNoncePayload()
        {
            lock (this)
            {
                Dictionary<string, object> noncePayload = new Dictionary<string, object>
                {
                    ["nonce"] = GenerateNonce()
                };
                if (RequestWindow.Ticks > 0)
                {
                    noncePayload["recvWindow"] = (long)RequestWindow.TotalMilliseconds;
                }
                return noncePayload;
            }
        }

        void IAPIRequestHandler.ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            ProcessRequest(request, payload);
        }

        void IAPIRequestHandler.ProcessResponse(HttpWebResponse response)
        {
            ProcessResponse(response);
        }

        Uri IAPIRequestHandler.ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            return ProcessRequestUrl(url, payload);
        }
    }
}
