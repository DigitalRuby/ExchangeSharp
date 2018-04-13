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

namespace ExchangeSharp
{
    /// <summary>
    /// Interface for making API requests
    /// </summary>
    public interface IAPIRequestMaker
    {
        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        string MakeRequest(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null);

        /// <summary>
        /// ASYNC - Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        Task<string> MakeRequestAsync(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null);
    }

    /// <summary>
    /// Interface for setting up and handling API request and response
    /// </summary>
    public interface IAPIRequestHandler
    {
        /// <summary>
        /// Additional handling for request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload);

        /// <summary>
        /// Additional handling for response
        /// </summary>
        /// <param name="response">Response</param>
        void ProcessResponse(HttpWebResponse response);

        /// <summary>
        /// Process a request url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="payload">Payload</param>
        /// <returns>Updated url</returns>
        Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload);

        /// <summary>
        /// Base url
        /// </summary>
        string BaseUrl { get; }

        /// <summary>
        /// Request method, i.e. GET
        /// </summary>
        string RequestMethod { get; }

        /// <summary>
        /// Request content type, i.e. application/json
        /// </summary>
        string RequestContentType { get; }

        /// <summary>
        /// Request cache policy
        /// </summary>
        System.Net.Cache.RequestCachePolicy RequestCachePolicy { get; }
        
        /// <summary>
        /// Request timeout
        /// </summary>
        TimeSpan RequestTimeout { get; }

        /// <summary>
        /// Rate limiter
        /// </summary>
        RateGate RateLimit { get; }
    }
}