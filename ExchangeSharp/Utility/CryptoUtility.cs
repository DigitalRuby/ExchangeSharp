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
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public static class CryptoUtility
    {
        /// <summary>
        /// Static constructor
        /// </summary>
        static CryptoUtility()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsMono = (Type.GetType("Mono.Runtime") != null);
        }

        /// <summary>
        /// Convert an object to string using invariant culture
        /// </summary>
        /// <param name="obj">Object</param>
        /// <returns>String</returns>
        public static string ToStringInvariant(this object obj)
        {
            return System.Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
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
        /// Convert an object to another type invariant
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="obj">Object</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Converted value or defaultValue if not found in token</returns>
        public static T ConvertInvariant<T>(this object obj, T defaultValue = default(T))
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
            return (T)System.Convert.ChangeType(jValue == null ? obj : jValue.Value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string NormalizeSymbol(string symbol)
        {
            return symbol?.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        public static string ToUnsecureString(this SecureString s)
        {
            return SecureStringToString(s);
        }

        public static SecureString ToSecureString(this string s)
        {
            return StringToSecureString(s);
        }

        public static string SecureStringToString(SecureString s)
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

        public static byte[] SecureStringToBytes(SecureString s)
        {
            string unsecure = SecureStringToString(s);
            byte[] bytes = Encoding.ASCII.GetBytes(unsecure);
            unsecure = null;
            return bytes;
        }

        public static byte[] SecureStringToBytesBase64Decode(SecureString s)
        {
            string unsecure = SecureStringToString(s);
            byte[] bytes = System.Convert.FromBase64String(unsecure);
            unsecure = null;
            return bytes;
        }

        public static SecureString StringToSecureString(string unsecure)
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

        public static decimal ClampQuantity(decimal minQuantity, decimal maxQuantity, decimal? stepSize, decimal quantity)
        {
            if(minQuantity < 0) throw new ArgumentOutOfRangeException(nameof(minQuantity));
            if (maxQuantity < 0) throw new ArgumentOutOfRangeException(nameof(maxQuantity));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (minQuantity > maxQuantity) throw new ArgumentOutOfRangeException(nameof(minQuantity));

            if (stepSize.HasValue)
            {
                if (stepSize < 0) throw new ArgumentOutOfRangeException(nameof(stepSize));

                quantity = Math.Min(maxQuantity, quantity);
                quantity = Math.Max(minQuantity, quantity);
                quantity -= quantity % stepSize.Value;
                quantity = RoundDown(quantity);
            }

            return quantity;
        }

        public static decimal ClampPrice(decimal minPrice, decimal maxPrice, decimal? tickSize, decimal price)
        {
            if (minPrice < 0) throw new ArgumentOutOfRangeException(nameof(minPrice));
            if (maxPrice < 0) throw new ArgumentOutOfRangeException(nameof(maxPrice));
            if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
            if(minPrice > maxPrice) throw new ArgumentOutOfRangeException(nameof(minPrice));
            
            if (tickSize.HasValue)
            {
                if (tickSize < 0) throw new ArgumentOutOfRangeException(nameof(tickSize));
                
                price = Math.Min(maxPrice, price);
                price = Math.Max(minPrice, price);
                price -= price % tickSize.Value;
                price = RoundDown(price);
            }
            return price;
        }

        public static DateTime UnixTimeStampToDateTimeSeconds(this double unixTimeStampSeconds)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStampSeconds);
            return dtDateTime;
        }

        public static DateTime UnixTimeStampToDateTimeMilliseconds(this double unixTimeStampMilliseconds)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixTimeStampMilliseconds);
            return dtDateTime;
        }

        public static double UnixTimestampFromDateTimeSeconds(this DateTime dt)
        {
            return (dt - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static double UnixTimestampFromDateTimeMilliseconds(this DateTime dt)
        {
            return (dt - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        public static string SHA256Sign(string message, string key)
        {
            return new HMACSHA256(Encoding.UTF8.GetBytes(key)).ComputeHash(Encoding.UTF8.GetBytes(message)).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        public static string SHA256Sign(string message, byte[] key)
        {
            return new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(message)).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        public static string SHA256SignBase64(string message, byte[] key)
        {
            return System.Convert.ToBase64String(new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(message)));
        }

        public static string SHA384Sign(string message, string key)
        {
            return new HMACSHA384(Encoding.UTF8.GetBytes(key)).ComputeHash(Encoding.UTF8.GetBytes(message)).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        public static string SHA384Sign(string message, byte[] key)
        {
            return new HMACSHA384(key).ComputeHash(Encoding.UTF8.GetBytes(message)).Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:x2}", b), (sb) => sb.ToString());
        }

        public static string SHA384SignBase64(string message, byte[] key)
        {
            return System.Convert.ToBase64String(new HMACSHA384(key).ComputeHash(Encoding.UTF8.GetBytes(message)));
        }

        public static string SHA512Sign(string message, string key)
        {
            var hmac = new HMACSHA512(Encoding.ASCII.GetBytes(key));
            var messagebyte = Encoding.ASCII.GetBytes(message);
            var hashmessage = hmac.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
        }

        public static string SHA512Sign(string message, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            var messagebyte = Encoding.ASCII.GetBytes(message);
            var hashmessage = hmac.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
        }

        public static string SHA512SignBase64(string message, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            var messagebyte = Encoding.ASCII.GetBytes(message);
            var hashmessage = hmac.ComputeHash(messagebyte);
            return System.Convert.ToBase64String(hashmessage);
        }

        public static byte[] GenerateSalt(int length)
        {
            byte[] salt = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

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
        /// <param name="spelledOut">Whether the string should be spelled out, i.e. hour, day, thirtyMin, etc.</param>
        /// <returns>Period string</returns>
        public static string SecondsToPeriodString(int seconds, bool spelledOut = false)
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
        /// Load protected data as strings from file. Call this function in your production environment, loading in a securely encrypted file which will stay encrypted in memory.
        /// On non-Windows platforms, the file is plain text and must be secured using file permissions.
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
        /// On non-Windows platforms, the file is plain text and must be secured using file permissions.
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

        public static bool IsWindows { get; private set; }
        public static bool IsMono { get; private set; }
    }
}
