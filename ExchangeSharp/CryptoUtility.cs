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

namespace ExchangeSharp
{
    public static class CryptoUtility
    {
        public static string SecureStringToString(SecureString s)
        {
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

        public static byte[] SecureStringToBytesBase64Decode(SecureString s)
        {
            string unsecure = SecureStringToString(s);
            byte[] bytes = Convert.FromBase64String(unsecure);
            unsecure = null;
            return bytes;
        }

        public static SecureString StringToSecureString(string unsecure)
        {
            SecureString secure = new SecureString();
            foreach (char c in unsecure)
            {
                secure.AppendChar(c);
            }
            return secure;
        }

        public static DateTime UnixTimeStampToDateTimeSeconds(double unixTimeStampSeconds)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStampSeconds);
            return dtDateTime;
        }

        public static DateTime UnixTimeStampToDateTimeMilliseconds(double unixTimeStampMilliseconds)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixTimeStampMilliseconds);
            return dtDateTime;
        }

        public static double UnixTimestampFromDateTimeSeconds(DateTime dt)
        {
            return (dt - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static double UnixTimestampFromDateTimeMilliseconds(DateTime dt)
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

        public static byte[] GenerateSalt(int length)
        {
            byte[] salt = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        public static void AesEncryption(Stream input, string file, string password)
        {
            using (var encrypted = new FileStream(file, FileMode.Create, FileAccess.Write))
            {
                byte[] salt = GenerateSalt(32);
                var AES = new RijndaelManaged()
                {
                    KeySize = 256,
                    BlockSize = 128,
                    Padding = PaddingMode.PKCS7,
                };

                var key = new Rfc2898DeriveBytes(password, salt, 1024);
                AES.Key = key.GetBytes(AES.KeySize / 8);
                AES.IV = key.GetBytes(AES.BlockSize / 8);

                AES.Mode = CipherMode.CFB;

                encrypted.Write(salt, 0, salt.Length);

                var cs = new CryptoStream(encrypted, AES.CreateEncryptor(), CryptoStreamMode.Write);
                var buffer = new byte[4096];
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    cs.Write(buffer, 0, read);
                cs.FlushFinalBlock();
            }
        }

        public static Stream AesDecryption(string file, string password)
        {
            Stream output = new MemoryStream();
            using (Stream input = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                byte[] salt = new byte[32];
                input.Read(salt, 0, 32);

                var AES = new RijndaelManaged()
                {
                    KeySize = 256,
                    BlockSize = 128,
                    Padding = PaddingMode.PKCS7,
                };

                var key = new Rfc2898DeriveBytes(password, salt, 1024);
                AES.Key = key.GetBytes(AES.KeySize / 8);
                AES.IV = key.GetBytes(AES.BlockSize / 8);

                AES.Mode = CipherMode.CFB;

                var cs = new CryptoStream(input, AES.CreateDecryptor(), CryptoStreamMode.Read);
                var buffer = new byte[4096];
                int read;

                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, read);
            }
            output.Seek(0, SeekOrigin.Begin);
            return output;
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
            byte[] unprotectedBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);

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
            File.WriteAllBytes(path, ProtectedData.Protect(memory.ToArray(), null, DataProtectionScope.CurrentUser));
        }
    }
}
