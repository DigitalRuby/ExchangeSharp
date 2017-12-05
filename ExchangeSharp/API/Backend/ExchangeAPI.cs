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
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Exception class for ExchangeAPI exceptions
    /// </summary>
    public class ExchangeAPIException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public ExchangeAPIException(string message) : base(message) { }
    }

    /// <summary>
    /// Base class for all exchange API
    /// </summary>
    public abstract class ExchangeAPI : IExchangeAPI
    {
        /// <summary>
        /// Bitfinex
        /// </summary>
        public const string ExchangeNameBitfinex = "Bitfinex";

        /// <summary>
        /// Bithumb
        /// </summary>
        public const string ExchangeNameBithumb = "Bithumb";

        /// <summary>
        /// Bittrex
        /// </summary>
        public const string ExchangeNameBittrex = "Bittrex";

        /// <summary>
        /// GDAX
        /// </summary>
        public const string ExchangeNameGDAX = "GDAX";

        /// <summary>
        /// Gemini
        /// </summary>
        public const string ExchangeNameGemini = "Gemini";

        /// <summary>
        /// Kraken
        /// </summary>
        public const string ExchangeNameKraken = "Kraken";

        /// <summary>
        /// Poloniex
        /// </summary>
        public const string ExchangeNamePoloniex = "Poloniex";

        /// <summary>
        /// Base URL for the exchange API
        /// </summary>
        public abstract string BaseUrl { get; set; }

        /// <summary>
        /// Public API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString PublicApiKey { get; set; }

        /// <summary>
        /// Private API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString PrivateApiKey { get; set; }

        /// <summary>
        /// Pass phrase API key  - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// Most exchanges do not require this, but GDAX is an example of one that does
        /// </summary>
        public System.Security.SecureString Passphrase { get; set; }

        /// <summary>
        /// Rate limiter - set this to a new limit if you are seeing your ip get blocked by the exchange
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
        /// User agent for requests
        /// </summary>
        public string RequestUserAgent { get; set; } = "ExchangeSharp (https://github.com/jjxtra/ExchangeSharp)";

        /// <summary>
        /// Timeout for requests
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30.0);

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        public System.Net.Cache.RequestCachePolicy CachePolicy { get; set; } = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

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

        protected string GetFormForPayload(Dictionary<string, object> payload)
        {
            if (payload != null && payload.Count != 0)
            {
                StringBuilder form = new StringBuilder();
                foreach (KeyValuePair<string, object> keyValue in payload)
                {
                    form.AppendFormat("{0}={1}&", Uri.EscapeDataString(keyValue.Key), Uri.EscapeDataString(keyValue.Value.ToString()));
                }
                form.Length--; // trim ampersand
                return form.ToString();
            }
            return string.Empty;
        }

        protected string GetJsonForPayload(Dictionary<string, object> payload)
        {
            if (payload != null && payload.Count != 0)
            {
                return JsonConvert.SerializeObject(payload);
            }
            return string.Empty;
        }

        protected void PostFormToRequest(HttpWebRequest request, string form)
        {
            if (!string.IsNullOrEmpty(form))
            {
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII))
                {
                    writer.Write(form);
                }
            }
        }

        protected string PostPayloadToRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            string form = GetFormForPayload(payload);
            PostFormToRequest(request, form);
            return form;
        }

        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a double value, set to unix timestamp in seconds.
        /// The encoding of payload is exchange dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        public string MakeRequest(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null)
        {
            RateLimit.WaitToProceed();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            else if (url[0] != '/')
            {
                url = "/" + url;
            }

            string fullUrl = (baseUrl ?? BaseUrl) + url;
            Uri uri = ProcessRequestUrl(new UriBuilder(fullUrl), payload);
            HttpWebRequest request = HttpWebRequest.CreateHttp(uri);
            request.Method = method ?? RequestMethod;
            request.ContentType = RequestContentType;
            request.UserAgent = RequestUserAgent;
            request.CachePolicy = CachePolicy;
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            ProcessRequest(request, payload);
            HttpWebResponse response;
            try
            {
                response = request.GetResponse() as HttpWebResponse;
                if (response == null)
                {
                    throw new ExchangeAPIException("Unknown response from server");
                }
            }
            catch (WebException we)
            {
                response = we.Response as HttpWebResponse;
                if (response == null)
                {
                    throw new ExchangeAPIException(we.Message ?? "Unknown response from server");
                }
            }
            string responseString = null;
            using (Stream responseStream = response.GetResponseStream())
            {
                responseString = new StreamReader(responseStream).ReadToEnd();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new ExchangeAPIException(responseString);
                }
                ProcessResponse(response);
            }
            response.Dispose();
            return responseString;
        }

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a double value, set to unix timestamp in seconds.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        public T MakeJsonRequest<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null)
        {
            string response = MakeRequest(url, baseUrl, payload, requestMethod);
            return JsonConvert.DeserializeObject<T>(response);
        }

        /// <summary>
        /// Get an exchange API given an exchange name (see public constants at top of this file)
        /// </summary>
        /// <param name="exchangeName">Exchange name</param>
        /// <returns>Exchange API or null if not found</returns>
        public static IExchangeAPI GetExchangeAPI(string exchangeName)
        {
            GetExchangeAPIDictionary().TryGetValue(exchangeName, out IExchangeAPI api);
            return api;
        }

        /// <summary>
        /// Get a dictionary of exchange APIs for all exchanges
        /// </summary>
        /// <returns>Dictionary of string exchange name and value exchange api</returns>
        public static Dictionary<string, IExchangeAPI> GetExchangeAPIDictionary()
        {
            Dictionary<string, IExchangeAPI> apis = new Dictionary<string, IExchangeAPI>(StringComparer.OrdinalIgnoreCase);
            foreach (Type type in typeof(ExchangeAPI).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ExchangeAPI))))
            {
                ExchangeAPI api = Activator.CreateInstance(type) as ExchangeAPI;
                apis[api.Name] = api;
            }
            return apis;
        }

        /// <summary>
        /// Load API keys from an encrypted file - keys will stay encrypted in memory
        /// </summary>
        /// <param name="encryptedFile">Encrypted file to load keys from</param>
        public virtual void LoadAPIKeys(string encryptedFile)
        {
            SecureString[] strings = CryptoUtility.LoadProtectedStringsFromFile(encryptedFile);
            if (strings.Length != 2)
            {
                throw new InvalidOperationException("Encrypted keys file should have a public and private key");
            }
            PublicApiKey = strings[0];
            PrivateApiKey = strings[1];
        }

        /// <summary>
        /// Normalize a symbol for use on this exchange
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <returns>Normalized symbol</returns>
        public virtual string NormalizeSymbol(string symbol) { return symbol; }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public virtual IReadOnlyCollection<string> GetSymbols() { throw new NotImplementedException(); }

        /// <summary>
        /// Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker</returns>
        public virtual ExchangeTicker GetTicker(string symbol) { throw new NotImplementedException(); }

        /// <summary>
        /// Get all tickers
        /// </summary>
        /// <returns>Key value pair of symbol and tickers array</returns>
        public virtual IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>> GetTickers() { throw new NotImplementedException(); }

        /// <summary>
        /// Get exchange order book
        /// </summary>
        /// <param name="symbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public virtual ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100) { throw new NotImplementedException(); }

        /// <summary>
        /// Get all pending orders for all symbols. Not all exchanges support this. Depending on the exchange, the number of bids and asks will have different counts, typically 50-100.
        /// </summary>
        /// <param name="maxCount">Max count of bids and asks - not all exchanges will honor this parameter</param>
        /// <returns>Symbol and order books pairs</returns>
        public virtual IReadOnlyCollection<KeyValuePair<string, ExchangeOrderBook>> GetOrderBooks(int maxCount = 100) { throw new NotImplementedException(); }

        /// <summary>
        /// Get historical trades for the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="sinceDateTime">Optional date time to start getting the historical data at, null for the most recent data</param>
        /// <returns>An enumerator that iterates all historical data, this can take quite a while depending on how far back the sinceDateTime parameter goes</returns>
        public virtual IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null) { throw new NotImplementedException(); }

        /// <summary>
        /// Get recent trades on the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all trades</returns>
        public virtual IEnumerable<ExchangeTrade> GetRecentTrades(string symbol) { return GetHistoricalTrades(symbol, null); }

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual Dictionary<string, decimal> GetAmountsAvailableToTrade() { throw new NotImplementedException(); }

        /// <summary>
        /// Place a limit order
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="amount">Amount</param>
        /// <param name="price">Price</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Result</returns>
        public virtual ExchangeOrderResult PlaceOrder(string symbol, decimal amount, decimal price, bool buy) { throw new NotImplementedException(); }

        /// <summary>
        /// Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <returns>Order details</returns>
        public virtual ExchangeOrderResult GetOrderDetails(string orderId) { throw new NotImplementedException(); }

        /// <summary>
        /// Get the details of all open orders
        /// </summary>
        /// <param name="symbol">Symbol to get open orders for or null for all</param>
        /// <returns>All open order details</returns>
        public virtual IEnumerable<ExchangeOrderResult> GetOpenOrderDetails(string symbol = null) { throw new NotImplementedException(); }

        /// <summary>
        /// Cancel an order, an exception is thrown if error
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        public virtual void CancelOrder(string orderId) { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the name of the exchange
        /// </summary>
        public virtual string Name { get { return "NullExchange"; } }
    }
}
