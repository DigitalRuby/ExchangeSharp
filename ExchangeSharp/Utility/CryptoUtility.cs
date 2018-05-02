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
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;

    public static class CryptoUtility
    {
        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

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
            return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? string.Empty;
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
                // fallback to float conversion, i.e. 1E-1 for a decimal conversion will fail
                string stringValue = (jValue == null ? obj.ToStringInvariant() : jValue.Value.ToStringInvariant());
                decimal decimalValue = decimal.Parse(stringValue, System.Globalization.NumberStyles.Float);
                return (T)Convert.ChangeType(decimalValue, typeof(T), CultureInfo.InvariantCulture);
            }
            return result;
        }

        public static string ToUnsecureString(this SecureString s)
        {
            return SecureStringToString(s);
        }

        public static SecureString ToSecureString(this string s)
        {
            return StringToSecureString(s);
        }

        public static string SecureStringToString(this SecureString s)
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

        public static byte[] SecureStringToBytes(this SecureString s)
        {
            if (s == null)
            {
                return null;
            }
            string unsecure = SecureStringToString(s);
            byte[] bytes = Encoding.ASCII.GetBytes(unsecure);
            unsecure = null;
            return bytes;
        }

        public static byte[] SecureStringToBytesBase64Decode(this SecureString s)
        {
            if (s == null)
            {
                return null;
            }
            string unsecure = SecureStringToString(s);
            byte[] bytes = Convert.FromBase64String(unsecure);
            unsecure = null;
            return bytes;
        }

        public static SecureString StringToSecureString(this string unsecure)
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

        public static decimal ClampDecimal(decimal minValue, decimal maxValue, decimal? stepSize, decimal value)
        {
            if (minValue < 0) throw new ArgumentOutOfRangeException(nameof(minValue));
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));

            if (stepSize.HasValue)
            {
                if (stepSize < 0) throw new ArgumentOutOfRangeException(nameof(stepSize));

                value = Math.Min(maxValue, value);
                value = Math.Max(minValue, value);
                decimal mod = value % stepSize.Value;
                value -= mod;
            }

            return value.Normalize();
        }

        /// <summary>Remove trailing zeroes on the decimal.</summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>1.230000 becomes 1.23</returns>
        public static decimal Normalize(this decimal value)
        {
            return value / 1.000000000000000000000000000000000m;
        }

        public static DateTime UnixTimeStampToDateTimeSeconds(this double unixTimeStampSeconds)
        {
            return unixEpoch.AddSeconds(unixTimeStampSeconds);
        }

        public static DateTime UnixTimeStampToDateTimeMilliseconds(this double unixTimeStampMilliseconds)
        {
            return unixEpoch.AddMilliseconds(unixTimeStampMilliseconds);
        }

        public static double UnixTimestampFromDateTimeSeconds(this DateTime dt)
        {
            return (dt - unixEpoch).TotalSeconds;
        }

        public static double UnixTimestampFromDateTimeMilliseconds(this DateTime dt)
        {
            return (dt - unixEpoch).TotalMilliseconds;
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
            return Convert.ToBase64String(new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(message)));
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
            return Convert.ToBase64String(new HMACSHA384(key).ComputeHash(Encoding.UTF8.GetBytes(message)));
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
            return Convert.ToBase64String(hashmessage);
        }

        public static string MD5Sign(string message)
        {
            var md5 = new MD5CryptoServiceProvider();
            var messagebyte = Encoding.ASCII.GetBytes(message);
            var hashmessage = md5.ComputeHash(messagebyte);
            return BitConverter.ToString(hashmessage).Replace("-", "");
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
    }
}
