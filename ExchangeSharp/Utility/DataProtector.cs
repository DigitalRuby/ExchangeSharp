using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CRYPTPROTECT_PROMPTSTRUCT
        {
            public int cbSize;
            public CryptProtectPromptFlags dwPromptFlags;
            public IntPtr hwndApp;
            public String szPrompt;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("Crypt32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct, CryptProtectFlags dwFlags, ref DATA_BLOB pDataOut);

        [DllImport("Crypt32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, string szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct, CryptProtectFlags dwFlags, ref DATA_BLOB pDataOut);

        private static byte[] CryptOperationWindows(bool protect, byte[] data)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                DATA_BLOB blob = new DATA_BLOB { cbData = data.Length, pbData = handle.AddrOfPinnedObject() };
                DATA_BLOB tmp = new DATA_BLOB();
                DATA_BLOB dataOut = new DATA_BLOB();
                CRYPTPROTECT_PROMPTSTRUCT prompt = new CRYPTPROTECT_PROMPTSTRUCT();
                if (protect)
                {
                    CryptProtectData(ref blob, null, ref tmp, IntPtr.Zero, ref prompt, CryptProtectFlags.CRYPTPROTECT_NONE, ref dataOut);
                }
                else
                {
                    CryptUnprotectData(ref blob, null, ref tmp, IntPtr.Zero, ref prompt, CryptProtectFlags.CRYPTPROTECT_NONE, ref dataOut);
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
            }
        }

        #endregion Windows

        /// <summary>
        /// Protected data using local user account
        /// </summary>
        /// <param name="data">Data to protect</param>
        /// <returns>Protected data</returns>
        public static byte[] Protect(byte[] data)
        {
            if (CryptoUtility.IsWindows)
            {
                return CryptOperationWindows(true, data);
            }
            else
            {
                return ManagedProtection.Protect(data, null, ManagedProtection.DataProtectionScope.CurrentUser);
            }
        }

        /// <summary>
        /// Unprotects data using local user account
        /// </summary>
        /// <param name="data">Data to unprotect</param>
        /// <returns>Unprotected data</returns>
        public static byte[] Unprotect(byte[] data)
        {
            if (CryptoUtility.IsWindows)
            {
                return CryptOperationWindows(false, data);
            }
            else
            {
                return ManagedProtection.Unprotect(data, null, ManagedProtection.DataProtectionScope.CurrentUser);
            }            
        }
    }
}
