/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Base interface for all API implementations
    /// </summary>
    public interface IBaseAPI : INamed
    {
        #region Properties

        /// <summary>
        /// Optional public API key
        /// </summary>
        SecureString? PublicApiKey { get; set; }

        /// <summary>
        /// Optional private API key
        /// </summary>
        SecureString? PrivateApiKey { get; set; }

        /// <summary>
        /// Pass phrase API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// Most exchanges do not require this, but Coinbase is an example of one that does
        /// </summary>
        System.Security.SecureString? Passphrase { get; set; }

        /// <summary>
        /// Request timeout
        /// </summary>
        TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Request window - most services do not use this, but Binance API is an example of one that does
        /// </summary>
        TimeSpan RequestWindow { get; set; }

        /// <summary>
        /// Nonce style
        /// </summary>
        NonceStyle NonceStyle { get; }

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        System.Net.Cache.RequestCachePolicy RequestCachePolicy { get; set; }

        /// <summary>
        /// Cache policy for api methods (method name, cache time)
        /// </summary>
        Dictionary<string, TimeSpan> MethodCachePolicy { get; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Load API keys from an encrypted file - keys will stay encrypted in memory
        /// </summary>
        /// <param name="encryptedFile">Encrypted file to load keys from</param>
        void LoadAPIKeys(string encryptedFile);

        /// <summary>
        ///  Load API keys from unsecure strings
        /// <param name="publicApiKey">Public Api Key</param>
        /// <param name="privateApiKey">Private Api Key</param>
        /// <param name="passPhrase">Pass phrase, null for none</param>
        /// </summary>
        void LoadAPIKeysUnsecure(string publicApiKey, string privateApiKey, string? passPhrase = null);

        /// <summary>
        /// Generate a nonce
        /// </summary>
        /// <returns>Nonce (can be string, long, double, etc., so object is used)</returns>
        Task<object> GenerateNonceAsync();

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        Task<T> MakeJsonRequestAsync<T>(string url, string? baseUrl = null, Dictionary<string, object>? payload = null, string? requestMethod = null);

        #endregion Methods
    }
}
