/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace ExchangeSharp
{
    /// <summary>
    /// Handles all the logic for making API calls.
    /// </summary>
    /// <seealso cref="ExchangeSharp.IAPIRequestMaker" />
    public class APIRequestMaker : IAPIRequestMaker
    {
        private readonly IAPIRequestHandler api;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">API</param>
        public APIRequestMaker(IAPIRequestHandler api)
        {
            this.api = api;
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
            this.api.RateLimit.WaitToProceed();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            else if (url[0] != '/')
            {
                url = "/" + url;
            }

            string fullUrl = (baseUrl ?? this.api.BaseUrl) + url;
            Uri uri = this.api.ProcessRequestUrl(new UriBuilder(fullUrl), payload);
            HttpWebRequest request = HttpWebRequest.CreateHttp(uri);
            request.Headers["Accept-Language"] = "en-us; q=1.0;";
            request.Method = method ?? this.api.RequestMethod;
            request.ContentType = this.api.RequestContentType;
            request.UserAgent = BaseAPI.RequestUserAgent;
            request.CachePolicy = this.api.RequestCachePolicy;
            request.Timeout = request.ReadWriteTimeout = request.ContinueTimeout = (int)this.api.RequestTimeout.TotalMilliseconds;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            api.ProcessRequest(request, payload);
            HttpWebResponse response;
            try
            {
                response = request.GetResponse() as HttpWebResponse;
                if (response == null)
                {
                    throw new APIException("Unknown response from server");
                }
            }
            catch (WebException we)
            {
                response = we.Response as HttpWebResponse;
                if (response == null)
                {
                    throw new APIException(we.Message ?? "Unknown response from server");
                }
            }
            string responseString = null;
            using (Stream responseStream = response.GetResponseStream())
            {
                responseString = new StreamReader(responseStream).ReadToEnd();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new APIException(responseString);
                }
                api.ProcessResponse(response);
            }
            response.Dispose();
            return responseString;
        }
    }
}
