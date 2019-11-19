/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using ExchangeSharp;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
    using System.Diagnostics;
    using System.Globalization;
    using System.Security;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    [TestClass]
    public class CryptoUtilityTests
    {
        private static Action Invoking(Action action) => action;

        [TestMethod]
        public void RoundDown()
        {
            CryptoUtility.RoundDown(1.2345m, 2).Should().Be(1.23m);
            CryptoUtility.RoundDown(1.2345m, 4).Should().Be(1.2345m);
            CryptoUtility.RoundDown(1.2345m, 5).Should().Be(1.2345m);
            CryptoUtility.RoundDown(1.2345m, 0).Should().Be(1m);
            CryptoUtility.RoundDown(1.2345m).Should().Be(1.234m);
        }

        [TestMethod]
        public void RoundDownDefaultRules()
        {
            CryptoUtility.RoundDown(0.000123456789m).Should().Be(0.0001234m);
            CryptoUtility.RoundDown(1.2345678m).Should().Be(1.234m);
            CryptoUtility.RoundDown(10.2345678m).Should().Be(10m);
        }

        [TestMethod]
        public void RoundDownOutOfRange()
        {
            void a() => CryptoUtility.RoundDown(1.2345m, -1);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void ClampPrice()
        {
            CryptoUtility.ClampDecimal(0.00000100m, 100000.00000000m, 0.00000100m, 0.05507632m).Should().Be(0.055076m);
            CryptoUtility.ClampDecimal(0.00000010m, 100000.00000000m, 0.00000010m, 0.00052286m).Should().Be(0.0005228m);
            CryptoUtility.ClampDecimal(0.00001000m, 100000.00000000m, 0.00001000m, 0.02525215m).Should().Be(0.02525m);
            CryptoUtility.ClampDecimal(0.00001000m, 100000.00000000m, null, 0.00401212m).Should().Be(0.00401212m);
        }

        [TestMethod]
        public void ClampDecimalTrailingZeroesRemoved()
        {
            decimal result = CryptoUtility.ClampDecimal(0, Decimal.MaxValue, 0.01m, 1.23456789m);
            result.Should().Be(1.23m);
            result.ToString(CultureInfo.InvariantCulture).Should().NotEndWith("0");
        }

        [TestMethod]
        public void ClampPriceOutOfRange()
        {
            void a() => CryptoUtility.ClampDecimal(-0.00000100m, 100000.00000000m, 0.00000100m, 0.05507632m);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();

            void b() => CryptoUtility.ClampDecimal(0.00000100m, -100000.00000000m, 0.00000100m, 0.05507632m);
            Invoking(b).Should().Throw<ArgumentOutOfRangeException>();

            void c() => CryptoUtility.ClampDecimal(0.00000100m, 100000.00000000m, 0.00000100m, -0.05507632m);
            Invoking(c).Should().Throw<ArgumentOutOfRangeException>();

            void d() => CryptoUtility.ClampDecimal(0.00000100m, 100000.00000000m, -0.00000100m, 0.05507632m);
            Invoking(d).Should().Throw<ArgumentOutOfRangeException>();

            void e() => CryptoUtility.ClampDecimal(100000.00000000m, 0.00000100m, -0.00000100m, 0.05507632m);
            Invoking(e).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void ClampQuantity()
        {
            CryptoUtility.ClampDecimal(0.01000000m, 90000000.00000000m, 0.01000000m, 34.55215m).Should().Be(34.55m);
            CryptoUtility.ClampDecimal(0.00100000m, 90000000.00000000m, 0.00100000m, 941.4192m).Should().Be(941.419m);
            CryptoUtility.ClampDecimal(0.00000100m, 90000000.00000000m, 0.00000100m, 172.94102192m).Should().Be(172.941021m);
            CryptoUtility.ClampDecimal(0.00010000m, 90000000.00000000m, null, 1837.31935m).Should().Be(1837.31935m);
        }

        [TestMethod]
        public void ClampQuantityOutOfRange()
        {
            void a() => CryptoUtility.ClampDecimal(-0.00010000m, 900000.00000000m, 0.00010000m, 33.393832m);
            Invoking(a).Should().Throw<ArgumentOutOfRangeException>();

            void b() => CryptoUtility.ClampDecimal(0.00010000m, -900000.00000000m, 0.00010000m, 33.393832m);
            Invoking(b).Should().Throw<ArgumentOutOfRangeException>();

            void c() => CryptoUtility.ClampDecimal(0.00010000m, 900000.00000000m, 0.00010000m, -33.393832m);
            Invoking(c).Should().Throw<ArgumentOutOfRangeException>();

            void d() => CryptoUtility.ClampDecimal(0.00010000m, 900000.00000000m, -0.00010000m, 33.393832m);
            Invoking(d).Should().Throw<ArgumentOutOfRangeException>();

            void e() => CryptoUtility.ClampDecimal(900000.00000000m, 0.00010000m, -0.00010000m, 33.393832m);
            Invoking(e).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public void CalculatePrecision_NoDecimals_Returns1()
        {
            CryptoUtility.CalculatePrecision("24").Should().Be(1);
            CryptoUtility.CalculatePrecision("1000").Should().Be(1);
            CryptoUtility.CalculatePrecision("123456789123456789465132").Should().Be(1);
        }

        [TestMethod]
        public void CalculatePrecision_WithDecimals()
        {
            CryptoUtility.CalculatePrecision("1.12").Should().Be(0.01m);
            CryptoUtility.CalculatePrecision("1.123456789").Should().Be(0.000000001m);
            CryptoUtility.CalculatePrecision("1.0").Should().Be(0.1m);
            CryptoUtility.CalculatePrecision("0.00000").Should().Be(0.00001m);
        }

        [TestMethod]
        public void AESEncryption()
        {
            byte[] salt = new byte[] { 65, 61, 53, 222, 105, 5, 199, 241, 213, 56, 19, 120, 251, 37, 66, 185 };
            byte[] data = new byte[255];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }
            byte[] password = new byte[16];
            for (int i = password.Length - 1; i >= 0; i--)
            {
                password[i] = (byte)i;
            }
            byte[] encrypted = CryptoUtility.AesEncryption(data, password, salt);
            byte[] decrypted = CryptoUtility.AesDecryption(encrypted, password, salt);
            Assert.IsTrue(decrypted.SequenceEqual(data));

            byte[] protectedData = DataProtector.Protect(salt);
            byte[] unprotectedData = DataProtector.Unprotect(protectedData);
            Assert.IsTrue(unprotectedData.SequenceEqual(salt));
        }

        [TestMethod]
        public void RSAFromFile()
        {
            byte[] originalValue = new byte[256];
            new System.Random().NextBytes(originalValue);

            for (int i = 0; i < 4; i++)
            {
                DataProtector.DataProtectionScope scope = (i < 2 ? DataProtector.DataProtectionScope.CurrentUser : DataProtector.DataProtectionScope.LocalMachine);
                RSA rsa = DataProtector.RSAFromFile(scope);
                byte[] encrypted = rsa.Encrypt(originalValue, RSAEncryptionPadding.Pkcs1);
                byte[] decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
                Assert.IsTrue(originalValue.SequenceEqual(decrypted));
            }
        }

        [TestMethod]
        public void KeyStore()
        {
            // store keys
            string path = Path.Combine(Path.GetTempPath(), "keystore.test.bin");
            string publicKey = "public key test aa45c0";
            string privateKey = "private key test bb270a";
            string[] keys = new string[] { publicKey, privateKey };

            CryptoUtility.SaveUnprotectedStringsToFile(path, keys);

            // read keys
            SecureString[] keysRead = CryptoUtility.LoadProtectedStringsFromFile(path);
            string publicKeyRead = CryptoUtility.ToUnsecureString(keysRead[0]);
            string privateKeyRead = CryptoUtility.ToUnsecureString(keysRead[1]);

            Assert.AreEqual(privateKeyRead, privateKey);
            Assert.AreEqual(publicKeyRead, publicKey);
        }

        [TestMethod]
        public void ConvertInvariantTest()
        {
            CultureInfo info = Thread.CurrentThread.CurrentCulture;
            try
            {
                CultureInfo info2 = new CultureInfo("fr-FR");
                string[] decimals = new string[] { "7800.07", "0.00172", "155975495", "7.93E+3", "0.00018984", "155975362" };
                foreach (string doubleString in decimals)
                {
                    Thread.CurrentThread.CurrentCulture = info;
                    decimal value = doubleString.ConvertInvariant<decimal>();
                    Thread.CurrentThread.CurrentCulture = info2;
                    decimal value2 = doubleString.ConvertInvariant<decimal>();
                    Assert.AreEqual(value, value2);
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    decimal value3 = doubleString.ConvertInvariant<decimal>();
                    Assert.AreEqual(value2, value3);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = info;
            }
        }

        [ConditionalTestMethod]
        [PlatformSpecificTest(
	        ~TestPlatforms.OSX,
	        "Has an issue on MacOS. See https://github.com/dotnet/corefx/issues/42607"
	    )]
        public async Task RateGate()
        {
	        const int timesPerPeriod = 1;
	        const int ms = 100;
	        const int loops = 5;
	        const double msMax = (double) ms * 1.5;
	        const double msMin = (double) ms * (1.0 / 1.5);
	        var gate = new RateGate(timesPerPeriod, TimeSpan.FromMilliseconds(ms));

	        var entered = await gate.WaitToProceedAsync(0);
	        if (!entered)
	        {
		        throw new APIException("Rate gate should have allowed immediate access to first attempt");
	        }

	        for (var i = 0; i < loops; i++)
	        {
		        var timer = Stopwatch.StartNew();
		        await gate.WaitToProceedAsync();
		        timer.Stop();

		        if (i <= 0)
		        {
			        continue;
		        }

		        // check for too much elapsed time with a little fudge
		        Assert.IsTrue(
			        timer.Elapsed.TotalMilliseconds <= msMax,
			        "Rate gate took too long to wait in between calls: " + timer.Elapsed.TotalMilliseconds + "ms"
		        );
		        Assert.IsTrue(
			        timer.Elapsed.TotalMilliseconds >= msMin,
			        "Rate gate took too little to wait in between calls: " + timer.Elapsed.TotalMilliseconds + "ms"
		        );
	        }
        }
    }
}
