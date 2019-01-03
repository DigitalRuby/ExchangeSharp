/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Useful extension methods for all sorts of things. Who says a kitchen sink class is bad?
    /// </summary>
    public static class CryptoUtility
    {
        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime unixEpochLocal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
        private static readonly Encoding utf8EncodingNoPrefix = new UTF8Encoding(false, true);

        private static Func<DateTime> utcNowFunc = UtcNowFuncImpl;

        private static DateTime UtcNowFuncImpl()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// Set utc now func for override CryptoUtility.UtcNow. Set to null to go back to default CryptoUtility.UtcNow.
        /// This is primarily useful for unit or integration testing.
        /// </summary>
        /// <param name="utcNowFunc">Utc now override func</param>
        public static void SetDateTimeUtcNowFunc(Func<DateTime> utcNowFunc)
        {
            CryptoUtility.utcNowFunc = utcNowFunc ?? UtcNowFuncImpl;
        }

        /// <summary>
        /// Empty object array
        /// </summary>
        public static readonly object[] EmptyObjectArray = new object[0];

        /// <summary>
        /// Empty string array
        /// </summary>
        public static readonly string[] EmptyStringArray = new string[0];

        /// <summary>
        /// Throw ArgumentNullException if obj is null
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="name">Parameter name</param>
        /// <param name="message">Message</param>
        public static void ThrowIfNull(this object obj, string name, string message = null)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(name, message);
            }
        }

        /// <summary>
        /// Convert an object to string using invariant culture
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="defaultValue">Default value if null</param>
        /// <returns>String</returns>
        public static string ToStringInvariant(this object obj, string defaultValue = "")
        {
            return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? defaultValue;
        }

        /// <summary>
        /// Convert an object to string uppercase using invariant culture
        /// </summary>
        /// <param name="obj">Object</param>
        /// <returns>String</returns>
        public static string ToStringUpperInvariant(this object obj)
        {
            return ToStringInvariant(obj).ToUpperInvariant();
        }

        /// <summary>
        /// Convert an object to string lowercase using invariant culture
        /// </summary>
        /// <param name="obj">Object</param>
        /// <returns>String</returns>
        public static string ToStringLowerInvariant(this object obj)
        {
            return ToStringInvariant(obj).ToLowerInvariant();
        }

        /// <summary>
        /// Convenience method to compare strings. The default uses OrginalIgnoreCase.
        /// </summary>
        /// <param name="s">String</param>
        /// <param name="other">Other string</param>
        /// <param name="option">Option</param>
        /// <returns>True if equal, false if not</returns>
        public static bool EqualsWithOption(this string s, string other, StringComparison option = StringComparison.OrdinalIgnoreCase)
        {
            if (s == null || other == null)
            {
                return false;
            }
            return s.Equals(other, option);
        }

        /// <summary>
        /// Decompress gzip bytes
        /// </summary>
        /// <param name="bytes">Bytes that are gzipped</param>
        /// <returns>Uncompressed bytes</returns>
        public static byte[] DecompressGzip(byte[] bytes)
        {
            using (var compressStream = new MemoryStream(bytes))
            {
                using (var zipStream = new GZipStream(compressStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        zipStream.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Decompress deflate bytes
        /// </summary>
        /// <param name="bytes">Bytes that are Deflate</param>
        /// <returns>Uncompressed bytes</returns>
        public static byte[] DecompressDeflate(byte[] bytes)
        {
            using (var compressStream = new MemoryStream(bytes))
            {
                using (var zipStream = new DeflateStream(compressStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        zipStream.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Convert a DateTime and set the kind using the DateTimeKind property.
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <param name="defaultValue">Default value if no conversion is possible</param>
        /// <returns>DateTime with DateTimeKind kind or defaultValue if no conversion possible</returns>
        public static DateTime ToDateTimeInvariant(this object obj, DateTime defaultValue = default)
        {
            if (obj == null)
            {
                return defaultValue;
            }
            JValue jValue = obj as JValue;
            if (jValue != null && jValue.Value == null)
            {
                return defaultValue;
            }
            DateTime dt = (DateTime)Convert.ChangeType(jValue == null ? obj : jValue.Value, typeof(DateTime), CultureInfo.InvariantCulture);
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return dt;
        }

        /// <summary>
        /// Convert an object to another type using invariant culture. Consider using the string or DateTime conversions if you are dealing with those types.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="obj">Object</param>
        /// <param name="defaultValue">Default value if no conversion is possible</param>
        /// <returns>Converted value or defaultValue if not conversion was possible</returns>
        public static T ConvertInvariant<T>(this object obj, T defaultValue = default)
        {
            if (obj == null)
            {
                return defaultValue;
            }
            JValue jValue = obj as JValue;
            if (jValue != null && jValue.Value == null)
            {
                return defaultValue;
            }
            T result;
            try
            {
                result = (T)Convert.ChangeType(jValue == null ? obj : jValue.Value, typeof(T), CultureInfo.InvariantCulture);
                if (typeof(T) == typeof(decimal))
                {
                    return (T)(object)((decimal)(object)result).Normalize();
                }
            }
            catch
            {
                if (typeof(T) == typeof(bool))
                {
                    // try zero or one
                    return (T)(object)((jValue ?? obj).ToStringInvariant() == "1");
                }
                else
                {
                    // fallback to float conversion, i.e. 1E-1 for a decimal conversion will fail
                    string stringValue = (jValue == null ? obj.ToStringInvariant() : jValue.Value.ToStringInvariant());
                    if (string.IsNullOrWhiteSpace(stringValue)) return defaultValue;
                    decimal decimalValue = decimal.Parse(stringValue, System.Globalization.NumberStyles.Float);
                    return (T)Convert.ChangeType(decimalValue, typeof(T), CultureInfo.InvariantCulture);
                }
            }
            return result;
        }

        /// <summary>
        /// Covnert a secure string to a non-secure string
        /// </summary>
        /// <param name="s">SecureString</param>
        /// <returns>Non-secure string</returns>
        public static string ToUnsecureString(this SecureString s)
        {
            if (s == null)
            {
                return null;
            }
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(s);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        /// <summary>
        /// Convert a secure string to non-secure binary data
        /// </summary>
        /// <param name="s">SecureString</param>
        /// <returns>Binary data</returns>
        public static byte[] ToUnsecureBytesUTF8(this SecureString s)
        {
            if (s == null)
            {
                return null;
            }
            return ToUnsecureString(s).ToBytesUTF8();
        }

        /// <summary>
        /// Decode a secure string that is in base64 format to binary data
        /// </summary>
        /// <param name="s">SecureString in base64 format</param>
        /// <returns>Binary data</returns>
        public static byte[] ToBytesBase64Decode(this SecureString s)
        {
            if (s == null)
            {
                return null;
            }
            return Convert.FromBase64String(ToUnsecureString(s));
        }

        /// <summary>
        /// Convert a string to a secure string
        /// </summary>
        /// <param name="unsecure">Plain text string</param>
        /// <returns>SecureString</returns>
        public static SecureString ToSecureString(this string unsecure)
        {
            if (unsecure == null)
            {
                return null;
            }
            SecureString secure = new SecureString();
            foreach (char c in unsecure)
            {
                secure.AppendChar(c);
            }
            return secure;
        }

        /// <summary>
        /// Get utf-8 bytes from a string
        /// </summary>
        /// <param name="s">String</param>
        /// <returns>UTF8 bytes or null if s is null</returns>
        public static byte[] ToBytesUTF8(this string s)
        {
            if (s == null)
            {
                return null;
            }
            return utf8EncodingNoPrefix.GetBytes(s);
        }

        /// <summary>
        /// Convert utf-8 bytes to a string
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="index">Offset</param>
        /// <param name="length">Length</param>
        /// <returns>UTF-8 string or null if bytes is null</returns>
        public static string ToStringFromUTF8(this byte[] bytes, int index = 0, int length = 0)
        {
            if (bytes == null)
            {
                return null;
            }
            length = (length <= 0 ? bytes.Length : length);
            return utf8EncodingNoPrefix.GetString(bytes, index, length);
        }

        /// <summary>
        /// Convert gzipped utf-8 bytes to a string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>UTF-8 string or null if bytes is null</returns>
        public static string ToStringFromUTF8Gzip(this byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }
            return DecompressGzip(bytes).ToStringFromUTF8();
        }

        /// <summary>
        /// Convert Deflate utf-8 bytes to a string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>UTF-8 string or null if bytes is null</returns>
        public static string ToStringFromUTF8Deflate(this byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }
            return DecompressDeflate(bytes).ToStringFromUTF8();
        }

        /// <summary>
        /// JWT encode - converts to base64 string first then replaces + with - and / with _
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Encoded string</returns>
        public static string JWTEncode(this byte[] input)
        {
            return Convert.ToBase64String(input)
                .Trim('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// JWT encode - converts to base64 string first then replaces + with - and / with _
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Encoded string</returns>
        public static string JWTEncode(this string input)
        {
            return Convert.ToBase64String(input.ToBytesUTF8())
                .Trim('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// JWT decode from JWTEncode
        /// </summary>
        /// <param name="input">Input</param>
        /// <returns>Decoded string</returns>
        public static string JWTDecodedString(this string input)
        {
            string output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: output += "=="; break; // Two pad chars
                case 3: output += "="; break; // One pad char
                default: throw new ArgumentException("Bad JWT string: " + input);
            }
            return Convert.FromBase64String(output).ToStringFromUTF8();
        }

        /// <summary>
        /// JWT decode from JWTEncode
        /// </summary>
        /// <param name="input">Input</param>
        /// <returns>Decoded bytes</returns>
        public static byte[] JWTDecodedBytes(this string input)
        {
            string output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: output += "=="; break; // Two pad chars
                case 3: output += "="; break; // One pad char
                default: throw new ArgumentException("Bad JWT string: " + input);
            }
            return Convert.FromBase64String(output);
        }

        /// <summary>
        /// Url encode extension - use this for ALL url encoding / escaping
        /// </summary>
        /// <param name="s">String to url encode</param>
        /// <returns>Url encoded string</returns>
        public static string UrlEncode(this string s)
        {
            return WebUtility.UrlEncode((s ?? string.Empty));
        }

        /// <summary>
        /// Form encode extension - use this for ALL form encoding / escaping
        /// </summary>
        /// <param name="s">String to form encode</param>
        /// <returns>Form encoded string</returns>
        public static string FormEncode(this string s)
        {
            return (s ?? string.Empty)
                .Replace("%", "%25")
                .Replace("+", "%2B")
                .Replace(' ', '+')
                .Replace("&", "%26")
                .Replace("=", "%3D")
                .Replace("\r", "%0D")
                .Replace("\n", "%0A")
                .Replace(":", "%3A");
        }

        /// <summary>
        /// Clamp a decimal to a min and max value
        /// </summary>
        /// <param name="minValue">Min value</param>
        /// <param name="maxValue">Max value</param>
        /// <param name="stepSize">Smallest unit value should be evenly divisible by</param>
        /// <param name="value">Value to clamp</param>
        /// <returns>Clamped value</returns>
        public static decimal ClampDecimal(decimal minValue, decimal maxValue, decimal? stepSize, decimal value)
        {
            if (minValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue));
            }
            else if (maxValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            }
            else if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            else if (minValue > maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue));
            }

            if (stepSize.HasValue)
            {
                if (stepSize < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(stepSize));
                }
                decimal mod = value % stepSize.Value;
                value -= mod;
            }
           
            if (maxValue > 0)
            {
                value = Math.Min(maxValue, value);
            }
                
            value = Math.Max(minValue, value);

            return value.Normalize();
        }

        /// <summary>Remove trailing zeroes on the decimal.</summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>1.230000 becomes 1.23</returns>
        public static decimal Normalize(this decimal value)
        {
            return value / 1.000000000000000000000000000000000m;
        }

        /// <summary>
        /// Get a UTC date time from a unix epoch in seconds
        /// </summary>
        /// <param name="unixTimeStampSeconds">Unix epoch in seconds</param>
        /// <returns>UTC DateTime</returns>
        public static DateTime UnixTimeStampToDateTimeSeconds(this double unixTimeStampSeconds)
        {
            return unixEpoch.AddSeconds(unixTimeStampSeconds);
        }

        /// <summary>
        /// Get a UTC date time from a unix epoch in milliseconds
        /// </summary>
        /// <param name="unixTimeStampSeconds">Unix epoch in milliseconds</param>
        /// <returns>UTC DateTime</returns>
        public static DateTime UnixTimeStampToDateTimeMilliseconds(this double unixTimeStampMilliseconds)
        {
            return unixEpoch.AddMilliseconds(unixTimeStampMilliseconds);
        }

        /// <summary>
        /// Get a utc date time from a local unix epoch in seconds
        /// </summary>
        /// <param name="unixTimeStampSeconds">Unix epoch in seconds</param>
        /// <returns>UTC DateTime</returns>
        public static DateTime UnixTimeStampLocalToDateTimeSeconds(this double unixTimeStampSeconds)
        {
            return unixEpochLocal.AddSeconds(unixTimeStampSeconds).ToUniversalTime();
        }

        /// <summary>
        /// Get a utc date time from a local unix epoch in milliseconds
        /// </summary>
        /// <param name="unixTimeStampSeconds">Unix epoch in milliseconds</param>
        /// <returns>Local DateTime</returns>
        public static DateTime UnixTimeStampLocalToDateTimeMilliseconds(this double unixTimeStampMilliseconds)
        {
            return unixEpochLocal.AddMilliseconds(unixTimeStampMilliseconds).ToUniversalTime();
        }

        /// <summary>
        /// Get a unix timestamp in seconds from a DateTime
        /// </summary>
        /// <param name="dt">DateTime</param>
        /// <returns>Unix epoch in seconds</returns>
        public static double UnixTimestampFromDateTimeSeconds(this DateTime dt)
        {
            if (dt.Kind != DateTimeKind.Utc)
            {
                dt = dt.ToUniversalTime();
            }
            return (dt - unixEpoch).TotalSeconds;
        }

        /// <summary>
        /// Get a unix timestamp in milliseconds from a DateTime
        /// </summary>
        /// <param name="dt">DateTime</param>
        /// <returns>Unix timestamp in milliseconds</returns>
        public static double UnixTimestampFromDateTimeMilliseconds(this DateTime dt)
        {
            if (dt.Kind != DateTimeKind.Utc)
            {
                dt = dt.ToUniversalTime();
            }
            return (dt - unixEpoch).TotalMilliseconds;
        }

        /// <summary>
        /// Convert a timestamp to DateTime. If value is null, CryptoUtility.UtcNow is returned.
        /// </summary>
        /// <param name="value">Timestamp object (JToken, string, double, etc.)</param>
        /// <param name="type">Type of timestamp</param>
        /// <returns>DateTime</returns>
        public static DateTime ParseTimestamp(object value, TimestampType type)
        {
            if (value == null || type == TimestampType.None)
            {
                return CryptoUtility.UtcNow;
            }

            switch (type)
            {
                case TimestampType.Iso8601:
                    return value.ToDateTimeInvariant();

                case TimestampType.UnixMillisecondsDouble:
                    return UnixTimeStampToDateTimeMilliseconds(value.ConvertInvariant<double>());

                case TimestampType.UnixMilliseconds:
                    return UnixTimeStampToDateTimeMilliseconds(value.ConvertInvariant<long>());

                case TimestampType.UnixSecondsDouble:
                    return UnixTimeStampToDateTimeSeconds(value.ConvertInvariant<double>());

                case TimestampType.UnixSeconds:
                    return UnixTimeStampToDateTimeSeconds(value.ConvertInvariant<long>());

                default:
                    throw new ArgumentException("Invalid timestamp type " + type);
            }
        }

        /// <summary>
        /// Get a string that can be used for basic authentication. Put this in the 'Authorization' http header, and ensure you are using https.
        /// </summary>
        /// <param name="userName">User name or public key</param>
        /// <param name="password">Password or private key</param>
        /// <returns>Full authorization header text</returns>
        public static string BasicAuthenticationString(string userName, string password)
        {
            return "Basic " + Convert.ToBase64String((userName + ":" + password).ToBytesUTF8());
        }

        /// <summary>
        /// Forces GetJsonForPayload to serialize the value of this key and nothing else.
        /// This is a little hacky, but allows posting arrays instead of dictionary to API for bulk calls.
        /// Normally a good API design would require a dictionary post with an array key and then
        /// an array of values, allowing other key/values to be posted with the bulk call,
        /// but some API (I'm looking at you Bitmex) just accept an array and nothing else.
        /// </summary>
        public const string PayloadKeyArray = "__hacky_array_key__";

        /// <summary>
        /// Convert a payload into json. If payload contains key PayloadKeyArray, that item is used only and serialized by itself
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <returns>Json string</returns>
        public static string GetJsonForPayload(this Dictionary<string, object> payload)
        {
            if (payload != null && payload.Count != 0)
            {
                if (payload.TryGetValue(PayloadKeyArray, out object array))
                {
                    return JsonConvert.SerializeObject(array, DecimalConverter.Instance);
                }
                return JsonConvert.SerializeObject(payload, DecimalConverter.Instance);
            }
            return string.Empty;
        }

        /// <summary>
        /// Write a form to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="form">Form to write</param>
        public static async Task WriteToRequestAsync(this IHttpWebRequest request, string form)
        {
            if (string.IsNullOrEmpty(form) && request.Method != "GET")
            {
                request.AddHeader("content-length", "0");
            }
            else if (!string.IsNullOrEmpty(form))
            {
                byte[] bytes = form.ToBytesUTF8();
                request.AddHeader("content-length", bytes.Length.ToStringInvariant());
                await request.WriteAllAsync(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Write a payload form to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        /// <returns>The form string that was written</returns>
        public static async Task<string> WritePayloadFormToRequestAsync(this IHttpWebRequest request, Dictionary<string, object> payload)
        {
            string form = GetFormForPayload(payload);
            await WriteToRequestAsync(request, form);
            return form;
        }

        /// <summary>
        /// Write a payload json to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        /// <returns>The json string that was written</returns>
        public static async Task<string> WritePayloadJsonToRequestAsync(this IHttpWebRequest request, Dictionary<string, object> payload)
        {
            string json = GetJsonForPayload(payload);
            await WriteToRequestAsync(request, json);
            return json;
        }

        /// <summary>
        /// Get a form for a request (form-encoded, like a query string)
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <param name="includeNonce">Whether to add the nonce</param>
        /// <param name="orderByKey">Whether to order by the key</param>
        /// <param name="formEncode">True to use form encoding, false to use url encoding</param>
        /// <returns>Form string</returns>
        public static string GetFormForPayload(this IReadOnlyDictionary<string, object> payload, bool includeNonce = true, bool orderByKey = true, bool formEncode = true)
        {
            if (payload != null && payload.Count != 0)
            {
                StringBuilder form = new StringBuilder();
                IEnumerable<KeyValuePair<string, object>> e = (orderByKey ? payload.OrderBy(kv => kv.Key) : payload.AsEnumerable<KeyValuePair<string, object>>());
                foreach (KeyValuePair<string, object> keyValue in e)
                {
                    if (!string.IsNullOrWhiteSpace(keyValue.Key) && keyValue.Value != null && (includeNonce || keyValue.Key != "nonce"))
                    {
                        string key = (formEncode ? keyValue.Key.FormEncode() : keyValue.Key.UrlEncode());
                        string value = (formEncode ? keyValue.Value.ToStringInvariant().FormEncode() : keyValue.Value.ToStringInvariant().UrlEncode());
                        form.Append($"{key}={value}&");
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
        /// Append a dictionary of key/values to a url builder query
        /// </summary>
        /// <param name="uri">Uri builder</param>
        /// <param name="payload">Payload to append</param>
        public static void AppendPayloadToQuery(this UriBuilder uri, Dictionary<string, object> payload)
        {
            if (uri.Query.Length > 1)
            {
                uri.Query += "&";
            }
            foreach (var kv in payload)
            {
                uri.Query += $"{kv.Key.UrlEncode()}={kv.Value.ToStringInvariant().UrlEncode()}&";
            }
            uri.Query = uri.Query.Trim('&');
        }

        /// <summary>
        /// Get a value from dictionary with default fallback
        /// </summary>
        /// <typeparam name="TKey">Key type</typeparam>
        /// <typeparam name="TValue">Value type</typeparam>
        /// <param name="dictionary">Dictionary</param>
        /// <param name="key">Key</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Found value or default</returns>
        public static TValue TryGetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            if (!dictionary.TryGetValue(key, out TValue value))
            {
                value = defaultValue;
            }
            return value;
        }

        /// <summary>
        /// Copy dictionary to another dictionary
        /// </summary>
        /// <typeparam name="TKey">Key type</typeparam>
        /// <typeparam name="TValue">Value type</typeparam>
        /// <param name="sourceDictionary"></param>
        /// <param name="destinationDictionary"></param>
        public static void CopyTo<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> sourceDictionary, IDictionary<TKey, TValue> destinationDictionary)
        {
            foreach (var kv in sourceDictionary)
            {
                destinationDictionary[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// Sign a message with SHA256 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key</param>
        /// <returns>Signature in hex</returns>
        public static string SHA256Sign(string message, string key)
        {
            return new HMACSHA256(key.ToBytesUTF8()).ComputeHash(message.ToBytesUTF8()).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        /// <summary>
        /// Sign a message with SHA256 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key bytes</param>
        /// <returns>Signature in hex</returns>
        public static string SHA256Sign(string message, byte[] key)
        {
            return new HMACSHA256(key).ComputeHash(message.ToBytesUTF8()).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        /// <summary>
        /// Sign a message with SHA256 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key bytes</param>
        /// <returns>Signature in base64</returns>
        public static string SHA256SignBase64(string message, byte[] key)
        {
            return Convert.ToBase64String(new HMACSHA256(key).ComputeHash(message.ToBytesUTF8()));
        }

        /// <summary>
        /// Sign a message with SHA384 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key</param>
        /// <returns>Signature in hex</returns>
        public static string SHA384Sign(string message, string key)
        {
            return new HMACSHA384(key.ToBytesUTF8()).ComputeHash(message.ToBytesUTF8()).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        /// <summary>
        /// Sign a message with SHA384 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key bytes</param>
        /// <returns>Signature</returns>
        public static string SHA384Sign(string message, byte[] key)
        {
            return new HMACSHA384(key).ComputeHash(message.ToBytesUTF8()).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        /// <summary>
        /// Sign a message with SHA384 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key bytes</param>
        /// <returns>Signature in base64</returns>
        public static string SHA384SignBase64(string message, byte[] key)
        {
            return Convert.ToBase64String(new HMACSHA384(key).ComputeHash(message.ToBytesUTF8()));
        }

        /// <summary>
        /// Sign a message with SHA512 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key</param>
        /// <returns>Signature in hex</returns>
        public static string SHA512Sign(string message, string key)
        {
            var hmac = new HMACSHA512(key.ToBytesUTF8());
            var messagebyte = message.ToBytesUTF8();
            var hashmessage = hmac.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
        }

        /// <summary>
        /// Sign a message with SHA512 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key bytes</param>
        /// <returns>Signature in hex</returns>
        public static string SHA512Sign(string message, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            var messagebyte = message.ToBytesUTF8();
            var hashmessage = hmac.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
        }

        /// <summary>
        /// Sign a message with SHA512 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <param name="key">Private key bytes</param>
        /// <returns>Signature in base64</returns>
        public static string SHA512SignBase64(string message, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            var messagebyte = message.ToBytesUTF8();
            var hashmessage = hmac.ComputeHash(messagebyte);
            return Convert.ToBase64String(hashmessage);
        }

        /// <summary>
        /// Sign a message with MD5 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <returns>Signature in hex</returns>
        public static string MD5Sign(string message, byte[] key)
        {
            var hmac = new HMACMD5(key);
            var messagebyte = message.ToBytesUTF8();
            var hashmessage = hmac.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
        }

        /// <summary>
        /// Sign a message with MD5 hash
        /// </summary>
        /// <param name="message">Message to sign</param>
        /// <returns>Signature in hex</returns>
        public static string MD5Sign(string message)
        {
            var md5 = new MD5CryptoServiceProvider();
            var messagebyte = message.ToBytesUTF8();
            var hashmessage = md5.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
        }

        /// <summary>
        /// Generate random salt
        /// </summary>
        /// <param name="length">Length of salt</param>
        /// <returns>Salt</returns>
        public static byte[] GenerateSalt(int length)
        {
            byte[] salt = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        /// <summary>
        /// AES encrypt data with a password and salt
        /// </summary>
        /// <param name="input">Data to encrypt</param>
        /// <param name="password">Password</param>
        /// <param name="salt">Salt</param>
        /// <returns>Encrypted data</returns>
        public static byte[] AesEncryption(byte[] input, byte[] password, byte[] salt)
        {
            if (input == null || input.Length == 0 || password == null || password.Length == 0 || salt == null || salt.Length == 0)
            {
                return null;
            }
            var encrypted = new MemoryStream();
            var AES = new RijndaelManaged()
            {
                KeySize = 256,
                BlockSize = 128,
                Padding = PaddingMode.PKCS7,
            };
            var key = new Rfc2898DeriveBytes(password, salt, 1024);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Mode = CipherMode.CBC;
            encrypted.Write(salt, 0, salt.Length);
            var cs = new CryptoStream(encrypted, AES.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(input, 0, input.Length);
            cs.FlushFinalBlock();
            return encrypted.ToArray();
        }

        /// <summary>
        /// AES decrypt data with a password and salt
        /// </summary>
        /// <param name="input">Data to decrypt</param>
        /// <param name="password">Password</param>
        /// <param name="salt">Salt</param>
        /// <returns>Decrypted data</returns>
        public static byte[] AesDecryption(byte[] input, byte[] password, byte[] salt)
        {
            if (input == null || input.Length == 0 || password == null || password.Length == 0 || salt == null || salt.Length == 0)
            {
                return null;
            }
            MemoryStream decrypted = new MemoryStream();
            var AES = new RijndaelManaged()
            {
                KeySize = 256,
                BlockSize = 128,
                Padding = PaddingMode.PKCS7,
            };
            var key = new Rfc2898DeriveBytes(password, salt, 1024);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Mode = CipherMode.CBC;
            MemoryStream encrypted = new MemoryStream(input);
            byte[] saltMatch = new byte[salt.Length];
            if (encrypted.Read(saltMatch, 0, saltMatch.Length) != salt.Length || !salt.SequenceEqual(saltMatch))
            {
                throw new InvalidOperationException("Invalid salt");
            }
            var cs = new CryptoStream(encrypted, AES.CreateDecryptor(), CryptoStreamMode.Read);
            byte[] buffer = new byte[8192];
            int count;
            while ((count = cs.Read(buffer, 0, buffer.Length)) > 0)
            {
                decrypted.Write(buffer, 0, count);
            }
            return decrypted.ToArray();
        }

        /// <summary>
        /// Convert seconds to a period string, i.e. 5s, 1m, 2h, 3d, 1w, 1M, etc.
        /// </summary>
        /// <param name="seconds">Seconds. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <returns>Period string</returns>
        public static string SecondsToPeriodString(int seconds)
        {
            const int minuteThreshold = 60;
            const int hourThreshold = 60 * 60;
            const int dayThreshold = 60 * 60 * 24;
            const int weekThreshold = dayThreshold * 7;
            const int monthThreshold = dayThreshold * 30;

            if (seconds >= monthThreshold)
            {
                return seconds / monthThreshold + "M";
            }
            else if (seconds >= weekThreshold)
            {
                return seconds / weekThreshold + "w";
            }
            else if (seconds >= dayThreshold)
            {
                return seconds / dayThreshold + "d";
            }
            else if (seconds >= hourThreshold)
            {
                return seconds / hourThreshold + "h";
            }
            else if (seconds >= minuteThreshold)
            {
                return seconds / minuteThreshold + "m";
            }
            return seconds + "s";
        }

        /// <summary>
        /// Convert seconds to a period string, i.e. 5sec, 1min, 2hour, 3day, 1week, 1month, 1year etc.
        /// </summary>
        /// <param name="seconds">Seconds. Use 60 for minute, 3600 for hour, 3600*24 for day, 3600*24*30 for month.</param>
        /// <returns>Period string</returns>
        public static string SecondsToPeriodStringLong(int seconds)
        {
            const int minuteThreshold = 60;
            const int hourThreshold = 60 * 60;
            const int dayThreshold = 60 * 60 * 24;
            const int weekThreshold = dayThreshold * 7;
            const int monthThreshold = dayThreshold * 30;
            const int yearThreshold = monthThreshold * 12;

            if (seconds >= yearThreshold)
            {
                return seconds / yearThreshold + "year";
            }
            else if (seconds >= monthThreshold)
            {
                return seconds / monthThreshold + "mon";
            }
            else if (seconds >= weekThreshold)
            {
                return seconds / weekThreshold + "week";
            }
            else if (seconds >= dayThreshold)
            {
                return seconds / dayThreshold + "day";
            }
            else if (seconds >= hourThreshold)
            {
                return seconds / hourThreshold + "hour";
            }
            else if (seconds >= minuteThreshold)
            {
                return seconds / minuteThreshold + "min";
            }
            return seconds + "sec";
        }

        /// <summary>
        /// Load protected data as strings from file. Call this function in your production environment, loading in a securely encrypted file which will stay encrypted in memory.
        /// </summary>
        /// <param name="path">Path to load from</param>
        /// <returns>Protected data</returns>
        public static SecureString[] LoadProtectedStringsFromFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            // while unprotectedBytes is populated, app is vulnerable - we clear this array ASAP to remove sensitive data from memory
            byte[] unprotectedBytes = DataProtector.Unprotect(bytes);

            MemoryStream memory = new MemoryStream(unprotectedBytes);
            BinaryReader reader = new BinaryReader(memory, Encoding.UTF8);
            SecureString current;
            int len;
            List<SecureString> strings = new List<SecureString>();

            while (memory.Position != memory.Length)
            {
                // copy char by char into secure string to avoid making additional string copies of sensitive data
                current = new SecureString();
                strings.Add(current);
                len = reader.ReadInt32();
                while (len-- > 0)
                {
                    current.AppendChar(reader.ReadChar());
                }
            }

            // cleanup and zero it out, hopefully GC hasn't moved unprotectedBytes around in memory
            Array.Clear(bytes, 0, bytes.Length);
            Array.Clear(unprotectedBytes, 0, unprotectedBytes.Length);

            return strings.ToArray();
        }

        /// <summary>
        /// Save unprotected data as strings to a file, where it will be encrypted for the current user account. Call this method offline with the data you need to secure.
        /// Call CryptoUtility.LoadProtectedStringsFromFile to later load these strings from the file.
        /// </summary>
        /// <param name="path">Path to save to</param>
        /// <param name="strings">Strings to save.</param>
        /// <example><![CDATA[ 
        /// CryptoUtility.SaveUnprotectedStringsToFile("test.bin", new string[] { "my super secret user name", "my super secret password with a ❤heart" });
        /// SecureString[] secure = CryptoUtility.LoadProtectedStringsFromFile("test.bin");
        /// string s;
        /// for (int i = 0; i<secure.Length; i++)
        /// {
        ///     s = CryptoUtility.SecureStringToString(secure[i]);
        ///     Console.WriteLine(s);
        /// }
        /// Console.ReadLine();
        /// ]]></example>
        public static void SaveUnprotectedStringsToFile(string path, string[] strings)
        {
            MemoryStream memory = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(memory, Encoding.UTF8);
            char[] chars;
            foreach (string s in strings)
            {
                chars = s.ToArray();
                writer.Write(chars.Length);
                foreach (char c in chars)
                {
                    writer.Write(c);
                }
            }
            writer.Flush();
            File.WriteAllBytes(path, DataProtector.Protect(memory.ToArray()));
        }

        /// <summary>
        /// Round an amount appropriate to its quantity
        /// </summary>
        /// <param name="amount">Amount</param>
        /// <returns>Rounded amount</returns>
        /// <remarks>
        /// Less than 1 : 7 decimal places
        /// Less than 10 : 3 decimal places
        /// Everything else : floor, no decimal places
        /// </remarks>
        public static decimal RoundAmount(decimal amount)
        {
            int places = GetDecimalPlaces(amount);
            if (places == 0)
            {
                return Math.Floor(amount);
            }

            return Math.Round(amount, places);
        }

        /// <summary>Gets the number of decimal places to preserve based on the size of the amount.</summary>
        /// <param name="amount">The amount.</param>
        /// <returns>The number of decimal places</returns>
        public static int GetDecimalPlaces(decimal amount)
        {
            if (amount < 1.0m)
            {
                return 7;
            }

            if (amount < 10.0m)
            {
                return 3;
            }

            return 0;
        }

        /// <summary>Rounds down at the specified number of decimal places. Do not specify places to use default decimal rules</summary>
        /// <param name="amount">The amount to round.</param>
        /// <param name="places">The decimal places to preserve.</param>
        /// <returns>(amount: 1.23456, places: 2) = 1.23</returns>
        public static decimal RoundDown(decimal amount, int? places = null)
        {
            if (!places.HasValue)
            {
                places = GetDecimalPlaces(amount);
            }
            else if (places.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(places));
            }

            decimal adjustment = (decimal)Math.Pow(10, places.Value);
            return Math.Floor(amount * adjustment) / adjustment;
        }

        /// <summary>Calculates the precision allowed based on the number of decimal points in a number.</summary>
        /// <param name="numberWithDecimals">The number on which to count decimal points.</param>
        /// <returns>A number indicating how many digits are after the decimal point. 
        /// For example, 5 zeroes after the decimal would indicate a price step size of 0.00001</returns>
        public static decimal CalculatePrecision(string numberWithDecimals)
        {
            int precision = 0;
            int decPoint = numberWithDecimals.IndexOf('.');
            if (decPoint != -1)
            {
                precision = numberWithDecimals.Length - decPoint - 1;
            }

            return (decimal)Math.Pow(10, -1 * precision);
        }

        /// <summary>
        /// Make a task execute synchronously
        /// </summary>
        /// <param name="task">Task</param>
        public static void Sync(this Task task)
        {
            task.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Make a task execute synchronously
        /// </summary>
        /// <param name="task">Task</param>
        /// <returns>Result</returns>
        public static T Sync<T>(this Task<T> task)
        {
            return task.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Wrap a method in caching logic. Also takes care of making a new SynchronizationContextRemover.
        /// This should not be used for post requests or other requests that operate on real-time data that changes with each request.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="cache">Cache</param>
        /// <param name="methodCachePolicy">Method cache policy</param>
        /// <param name="method">Method implementation</param>
        /// <param name="arguments">Function arguments - function name and then param name, value, name, value, etc.</param>
        /// <returns></returns>
        public static async Task<T> CacheMethod<T>(this ICache cache, Dictionary<string, TimeSpan> methodCachePolicy, Func<Task<T>> method, params object[] arguments) where T : class
        {
            await new SynchronizationContextRemover();
            methodCachePolicy.ThrowIfNull(nameof(methodCachePolicy));
            if (arguments.Length % 2 == 0)
            {
                throw new ArgumentException("Must pass function name and then name and value of each argument");
            }
            string methodName = arguments[0].ToStringInvariant();
            string cacheKey = methodName;
            for (int i = 1; i < arguments.Length;)
            {
                cacheKey += "|" + arguments[i++].ToStringInvariant() + "=" + arguments[i++].ToStringInvariant("(null)");
            }
            if (methodCachePolicy.TryGetValue(methodName, out TimeSpan cacheTime))
            {
                return (await cache.Get<T>(cacheKey, async () =>
                {
                    T innerResult = await method();
                    return new CachedItem<T>(innerResult, CryptoUtility.UtcNow.Add(cacheTime));
                })).Value;
            }
            else
            {
                return await method();
            }
        }

        /// <summary>
        /// Utf-8 encoding with no prefix bytes
        /// </summary>
        public static Encoding UTF8EncodingNoPrefix { get { return utf8EncodingNoPrefix; } }

        /// <summary>
        /// Return CryptoUtility.UtcNow or override if SetDateTimeUtcNowFunc has been called
        /// </summary>
        public static DateTime UtcNow { get { return utcNowFunc(); } }

        /// <summary>
        /// True if platform is Windows, false otherwise
        /// </summary>
        public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// True if running under Mono (https://www.mono-project.com/), false if not
        /// </summary>
        public static bool IsMono { get; } = (Type.GetType("Mono.Runtime") != null);
    }

    /// <summary>
    /// Possible types of timestamps
    /// </summary>
    public enum TimestampType
    {
        /// <summary>
        /// No timestamp type
        /// </summary>
        None,

        /// <summary>
        /// Unix milliseconds (double)
        /// </summary>
        UnixMillisecondsDouble,

        /// <summary>
        /// Unix milliseconds (long)
        /// </summary>
        UnixMilliseconds,

        /// <summary>
        /// Unix seconds (double)
        /// </summary>
        UnixSecondsDouble,

        /// <summary>
        /// Unix seconds (long)
        /// </summary>
        UnixSeconds,

        /// <summary>
        /// ISO 8601
        /// </summary>
        Iso8601
    }
}
