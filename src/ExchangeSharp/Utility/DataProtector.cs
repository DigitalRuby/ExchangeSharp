/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Xml;

namespace ExchangeSharp
{
    /// <summary>
    /// Allows protecting data using encryption tied to the local user account
    /// </summary>
    public static class DataProtector
    {
        #region Windows

        [Flags]
        private enum CryptProtectFlags
        {
            /// <summary>
            /// No flags (user level)
            /// </summary>
            CRYPTPROTECT_NONE = 0x0,

            // for remote-access situations where ui is not an option
            // if UI was specified on protect or unprotect operation, the call
            // will fail and GetLastError() will indicate ERROR_PASSWORD_RESTRICTION
            CRYPTPROTECT_UI_FORBIDDEN = 0x1,

            // per machine protected data -- any user on machine where CryptProtectData
            // took place may CryptUnprotectData
            CRYPTPROTECT_LOCAL_MACHINE = 0x4,

            // force credential synchronize during CryptProtectData()
            // Synchronize is only operation that occurs during this operation
            CRYPTPROTECT_CRED_SYNC = 0x8,

            // Generate an Audit on protect and unprotect operations
            CRYPTPROTECT_AUDIT = 0x10,

            // Protect data with a non-recoverable key
            CRYPTPROTECT_NO_RECOVERY = 0x20,


            // Verify the protection of a protected blob
            CRYPTPROTECT_VERIFY_PROTECTION = 0x40
        }

        [Flags]
        private enum CryptProtectPromptFlags
        {
            // prompt on unprotect
            CRYPTPROTECT_PROMPT_ON_UNPROTECT = 0x1,

            // prompt on protect
            CRYPTPROTECT_PROMPT_ON_PROTECT = 0x2
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct CRYPTPROTECT_PROMPTSTRUCT
        {
            public int cbSize;
            public CryptProtectPromptFlags dwPromptFlags;
            public IntPtr hwndApp;
            public String szPrompt;
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("crypt32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct, CryptProtectFlags dwFlags, ref DATA_BLOB pDataOut);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("crypt32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, string? szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct, CryptProtectFlags dwFlags, ref DATA_BLOB pDataOut);

        private static byte[] CryptOperationWindows(bool protect, byte[] data, byte[]? optionalEntropy, DataProtectionScope scope)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            GCHandle handleEntropy = (optionalEntropy != null && optionalEntropy.Length != 0 ? GCHandle.Alloc(optionalEntropy, GCHandleType.Pinned) : new GCHandle());
            try
            {
                DATA_BLOB dataIn = new DATA_BLOB
                {
                    cbData = data.Length,
                    pbData = handle.AddrOfPinnedObject()
                };
                DATA_BLOB entropy = new DATA_BLOB
                {
                    cbData = (optionalEntropy == null ? 0 : optionalEntropy.Length),
                    pbData = (handleEntropy.IsAllocated ? handleEntropy.AddrOfPinnedObject() : IntPtr.Zero)
                };
                DATA_BLOB dataOut = new DATA_BLOB();
                CRYPTPROTECT_PROMPTSTRUCT prompt = new CRYPTPROTECT_PROMPTSTRUCT();
                CryptProtectFlags flags = (scope == DataProtectionScope.CurrentUser ? CryptProtectFlags.CRYPTPROTECT_NONE : CryptProtectFlags.CRYPTPROTECT_LOCAL_MACHINE);
                if (protect)
                {
                    CryptProtectData(ref dataIn, null, ref entropy, IntPtr.Zero, ref prompt, flags, ref dataOut);
                }
                else
                {
                    CryptUnprotectData(ref dataIn, null, ref entropy, IntPtr.Zero, ref prompt, flags, ref dataOut);
                }
                if (dataOut.cbData == 0)
                {
                    throw new System.IO.InvalidDataException("Unable to protect/unprotect data, most likely the data came from a different user account or a different machine");
                }
                byte[] dataCopy = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, dataCopy, 0, dataCopy.Length);
                LocalFree(dataOut.pbData);
                return dataCopy;
            }
            finally
            {
                handle.Free();
                if (handleEntropy.IsAllocated)
                {
                    handleEntropy.Free();
                }
            }
        }

        #endregion Windows

        #region Non-Windows

        //
        // ManagedProtection.cs -
        //	Protect (encrypt) data without (user involved) key management
        //
        // Author:
        //	Sebastien Pouliot  <sebastien@ximian.com>
        //
        // Copyright (C) 2005 Novell, Inc (http://www.novell.com)
        //
        // Permission is hereby granted, free of charge, to any person obtaining
        // a copy of this software and associated documentation files (the
        // "Software"), to deal in the Software without restriction, including
        // without limitation the rights to use, copy, modify, merge, publish,
        // distribute, sublicense, and/or sell copies of the Software, and to
        // permit persons to whom the Software is furnished to do so, subject to
        // the following conditions:
        //
        // The above copyright notice and this permission notice shall be
        // included in all copies or substantial portions of the Software.
        //
        // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
        // EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
        // MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
        // NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
        // LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
        // OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
        // WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
        //

        // Managed Protection Implementation
        //
        // Features
        // * Separate RSA 1536 bits keypairs for each user and the computer
        // * AES 128 bits encryption (separate key for each data protected)
        // * SHA256 digest to ensure integrity
        internal static class ManagedProtection
        {
            private static readonly RSA user;
            private static readonly RSA machine;

            static ManagedProtection()
            {
                try
                {
                    CspParameters csp = new CspParameters
                    {
                        KeyContainerName = "DAPI"
                    };
                    user = new RSACryptoServiceProvider(1536, csp);
                }
                catch
                {
                    user = RSAFromFile(DataProtectionScope.CurrentUser);
                }
                try
                {
                    CspParameters csp = new CspParameters
                    {
                        KeyContainerName = "DAPI",
                        Flags = CspProviderFlags.UseMachineKeyStore
                    };
                    machine = new RSACryptoServiceProvider(1536, csp);
                }
                catch
                {
                    machine = RSAFromFile(DataProtectionScope.LocalMachine);
                }
            }

            // FIXME	[KeyContainerPermission (SecurityAction.Assert, KeyContainerName = "DAPI",
            //			Flags = KeyContainerPermissionFlags.Open | KeyContainerPermissionFlags.Create)]
            public static byte[] Protect(byte[] userData, byte[] optionalEntropy, DataProtectionScope scope)
            {
                if (userData == null)
                    throw new ArgumentNullException("userData");

                Rijndael aes = Rijndael.Create();
                aes.KeySize = 128;

                byte[]? encdata = null;
                using (MemoryStream ms = new MemoryStream())
                {
                    ICryptoTransform t = aes.CreateEncryptor();
                    using CryptoStream cs = new CryptoStream(ms, t, CryptoStreamMode.Write);
                    cs.Write(userData, 0, userData.Length);
                    cs.Close();
                    encdata = ms.ToArray();
                }

                byte[]? key = null;
                byte[]? iv = null;
                byte[]? secret = null;
                byte[]? header = null;
                SHA256 hash = SHA256.Create();

                try
                {
                    key = aes.Key;
                    iv = aes.IV;
                    secret = new byte[1 + 1 + 16 + 1 + 16 + 1 + 32];

                    byte[] digest = hash.ComputeHash(userData);
                    if ((optionalEntropy != null) && (optionalEntropy.Length > 0))
                    {
                        // the same optionalEntropy will be required to get the data back
                        byte[] mask = hash.ComputeHash(optionalEntropy);
                        for (int i = 0; i < 16; i++)
                        {
                            key[i] ^= mask[i];
                            iv[i] ^= mask[i + 16];
                        }
                        secret[0] = 2; // entropy
                    }
                    else
                    {
                        secret[0] = 1; // without entropy
                    }

                    secret[1] = 16; // key size
                    Buffer.BlockCopy(key, 0, secret, 2, 16);
                    secret[18] = 16; // iv size
                    Buffer.BlockCopy(iv, 0, secret, 19, 16);
                    secret[35] = 32; // digest size
                    Buffer.BlockCopy(digest, 0, secret, 36, 32);

                    RSAOAEPKeyExchangeFormatter formatter = new RSAOAEPKeyExchangeFormatter(GetKey(scope));
                    header = formatter.CreateKeyExchange(secret);
                }
                finally
                {
                    if (key != null)
                    {
                        Array.Clear(key, 0, key.Length);
                        key = null;
                    }
                    if (secret != null)
                    {
                        Array.Clear(secret, 0, secret.Length);
                        secret = null;
                    }
                    if (iv != null)
                    {
                        Array.Clear(iv, 0, iv.Length);
                        iv = null;
                    }
                    aes.Clear();
                    hash.Clear();
                }

                byte[] result = new byte[header.Length + encdata.Length];
                Buffer.BlockCopy(header, 0, result, 0, header.Length);
                Buffer.BlockCopy(encdata, 0, result, header.Length, encdata.Length);
                return result;
            }

            //TODO: FIXME	[KeyContainerPermission (SecurityAction.Assert, KeyContainerName = "DAPI",
            //			Flags = KeyContainerPermissionFlags.Open | KeyContainerPermissionFlags.Decrypt)]
            public static byte[] Unprotect(byte[] encryptedData, byte[] optionalEntropy, DataProtectionScope scope)
            {
                if (encryptedData == null)
                    throw new ArgumentNullException("encryptedData");

                byte[]? decdata = null;

                Rijndael aes = Rijndael.Create();
                RSA rsa = GetKey(scope);
                int headerSize = (rsa.KeySize >> 3);
                bool valid1 = (encryptedData.Length >= headerSize);
                if (!valid1)
                    headerSize = encryptedData.Length;

                byte[] header = new byte[headerSize];
                Buffer.BlockCopy(encryptedData, 0, header, 0, headerSize);

                byte[]? secret = null;
                byte[]? key = null;
                byte[]? iv = null;
                bool valid2 = false;
                bool valid3 = false;
                bool valid4 = false;
                using SHA256 hash = SHA256.Create();
                try
                {
                    try
                    {
                        RSAOAEPKeyExchangeDeformatter deformatter = new RSAOAEPKeyExchangeDeformatter(rsa);
                        secret = deformatter.DecryptKeyExchange(header);
                        valid2 = (secret.Length == 68);
                    }
                    catch
                    {
                        valid2 = false;
                    }

                    if (!valid2)
                        secret = new byte[68];

                    // known values for structure (version 1 or 2)
                    valid3 = ((secret![1] == 16) && (secret[18] == 16) && (secret[35] == 32));

                    key = new byte[16];
                    Buffer.BlockCopy(secret, 2, key, 0, 16);
                    iv = new byte[16];
                    Buffer.BlockCopy(secret, 19, iv, 0, 16);

                    if ((optionalEntropy != null) && (optionalEntropy.Length > 0))
                    {
                        // the decrypted data won't be valid if the entropy isn't
                        // the same as the one used to protect (encrypt) it
                        byte[] mask = hash.ComputeHash(optionalEntropy);
                        for (int i = 0; i < 16; i++)
                        {
                            key[i] ^= mask[i];
                            iv[i] ^= mask[i + 16];
                        }
                        valid3 &= (secret[0] == 2); // with entropy
                    }
                    else
                    {
                        valid3 &= (secret[0] == 1); // without entropy
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        ICryptoTransform t = aes.CreateDecryptor(key, iv);
                        using (CryptoStream cs = new CryptoStream(ms, t, CryptoStreamMode.Write))
                        {
                            try
                            {
                                cs.Write(encryptedData, headerSize, encryptedData.Length - headerSize);
                                cs.Close();
                            }
                            catch
                            {
                                // whatever, we keep going
                            }
                        }
                        decdata = ms.ToArray();
                    }

                    byte[] digest = hash.ComputeHash(decdata);
                    valid4 = true;
                    for (int i = 0; i < 32; i++)
                    {
                        if (digest[i] != secret[36 + i])
                            valid4 = false;
                    }
                }
                finally
                {
                    if (key != null)
                    {
                        Array.Clear(key, 0, key.Length);
                        key = null;
                    }
                    if (secret != null)
                    {
                        Array.Clear(secret, 0, secret.Length);
                        secret = null;
                    }
                    if (iv != null)
                    {
                        Array.Clear(iv, 0, iv.Length);
                        iv = null;
                    }
                    aes.Clear();
                    hash.Clear();
                }

                // single point of error (also limits timing informations)
                if (!valid1 || !valid2 || !valid3 || !valid4)
                {
                    if (decdata != null)
                    {
                        Array.Clear(decdata, 0, decdata.Length);
                        decdata = null;
                    }
                    throw new CryptographicException("Invalid data.");
                }
                return decdata;
            }

            private static RSA GetKey(DataProtectionScope scope)
            {
                switch (scope)
                {
                    case DataProtectionScope.CurrentUser:
                        return user;

                    case DataProtectionScope.LocalMachine:
                        return machine;

                    default:
                        throw new CryptographicException("Invalid scope.");
                }
            }
        }

        #endregion Non-Windows

        /// <summary>
        /// Data protection scope
        /// </summary>
        public enum DataProtectionScope
        {
            /// <summary>
            /// Key for local machine
            /// </summary>
            LocalMachine,

            /// <summary>
            /// Key for current user
            /// </summary>
            CurrentUser
        };

        /// <summary>
        /// Store an encrypted key file for user or machine level usage
        /// </summary>
        /// <param name="scope">Scope</param>
        /// <returns>RSA key</returns>
        public static RSA RSAFromFile(DataProtectionScope scope)
        {
            byte[] esp = new byte[] { 69, 155, 31, 254, 7, 18, 99, 187 };
            byte[] esl = new byte[] { 101, 5, 79, 221, 48, 42, 26, 123 };
            string xmlFile = (scope == DataProtectionScope.CurrentUser ? Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "esku_123_abc.bin") :
                Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "eskm_123_abc.bin"));
            RSACryptoServiceProvider rsa;
            if (File.Exists(xmlFile))
            {
                byte[] xmlBytes = File.ReadAllBytes(xmlFile);
                xmlBytes = CryptoUtility.AesDecryption(xmlBytes, esp, esl) ?? throw new InvalidDataException("Unable to decrypt file " + xmlFile);
                rsa = new RSACryptoServiceProvider();
                RSAKeyExtensions.FromXmlString(rsa, CryptoUtility.UTF8EncodingNoPrefix.GetString(xmlBytes));
            }
            else
            {
                rsa = new RSACryptoServiceProvider(4096);
                byte[] xmlBytes = RSAKeyExtensions.ToXmlString(rsa, true).ToBytesUTF8();
                xmlBytes = CryptoUtility.AesEncryption(xmlBytes, esp, esl);
                File.WriteAllBytes(xmlFile, xmlBytes);
            }
            return rsa;
        }

        /// <summary>
        /// Protected data using local user account
        /// </summary>
        /// <param name="data">Data to protect</param>
        /// <returns>Protected data</returns>
        public static byte[] Protect(byte[] data, byte[]? optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            if (CryptoUtility.IsWindows)
            {
                return CryptOperationWindows(true, data, optionalEntropy, scope);
            }
            else
            {
                return ManagedProtection.Protect(data, optionalEntropy, scope);
            }
        }

        /// <summary>
        /// Unprotects data using local user account
        /// </summary>
        /// <param name="data">Data to unprotect</param>
        /// <returns>Unprotected data</returns>
        public static byte[] Unprotect(byte[] data, byte[]? optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.CurrentUser)
        {
            if (CryptoUtility.IsWindows)
            {
                return CryptOperationWindows(false, data, optionalEntropy, scope);
            }
            else
            {
                return ManagedProtection.Unprotect(data, optionalEntropy, scope);
            }
        }
    }

    internal static class RSAKeyExtensions
    {
        public static void FromXmlString(this RSA rsa, string xmlString)
        {
            RSAParameters parameters = new RSAParameters();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);

            if (xmlDoc.DocumentElement.Name.Equals("RSAKeyValue"))
            {
                foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "Modulus": parameters.Modulus = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "Exponent": parameters.Exponent = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "P": parameters.P = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "Q": parameters.Q = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "DP": parameters.DP = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "DQ": parameters.DQ = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "InverseQ": parameters.InverseQ = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        case "D": parameters.D = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                    }
                }
            }
            else
            {
                throw new Exception("Invalid XML RSA key.");
            }

            rsa.ImportParameters(parameters);
        }

        public static string ToXmlString(this RSA rsa, bool includePrivateParameters)
        {
            RSAParameters parameters = rsa.ExportParameters(includePrivateParameters);

            return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent><P>{2}</P><Q>{3}</Q><DP>{4}</DP><DQ>{5}</DQ><InverseQ>{6}</InverseQ><D>{7}</D></RSAKeyValue>",
                  parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
                  parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null,
                  parameters.P != null ? Convert.ToBase64String(parameters.P) : null,
                  parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null,
                  parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null,
                  parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null,
                  parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null,
                  parameters.D != null ? Convert.ToBase64String(parameters.D) : null);
        }
    }
}
