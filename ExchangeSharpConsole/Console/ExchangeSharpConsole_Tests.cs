/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;

using ExchangeSharp;

namespace ExchangeSharpConsoleApp
{
	public static partial class ExchangeSharpConsole
    {
        private static void Assert(bool expression)
        {
            if (!expression)
            {
                throw new ApplicationException("Test failure, unexpected result");
            }
        }

        private static string GetSymbol(IExchangeAPI api)
        {
            if (api is ExchangeKrakenAPI)
            {
                return api.NormalizeSymbol("XXBTZUSD");
            }
            else if (api is ExchangeBittrexAPI || api is ExchangePoloniexAPI)
            {
                return api.NormalizeSymbol("BTC-LTC");
            }
            else if (api is ExchangeBinanceAPI || api is ExchangeOkexAPI)
            {
                return api.NormalizeSymbol("ETH-BTC");
            }
            return api.NormalizeSymbol("BTC-USD");
        }

        private static void TestRateGate()
        {
            try
            {
                int timesPerPeriod = 1;
                int ms = 500;
                int loops = 10;
                double msMax = (double)ms * 1.1;
                double msMin = (double)ms * 0.9;
                RateGate gate = new RateGate(timesPerPeriod, TimeSpan.FromMilliseconds(ms));
                if (!gate.WaitToProceed(0))
                {
                    throw new APIException("Rate gate should have allowed immediate access to first attempt");
                }
                for (int i = 0; i < loops; i++)
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    gate.WaitToProceed();
                    timer.Stop();

                    if (i > 0)
                    {
                        // check for too much elapsed time with a little fudge
                        if (timer.Elapsed.TotalMilliseconds > msMax)
                        {
                            throw new APIException("Rate gate took too long to wait in between calls: " + timer.Elapsed.TotalMilliseconds + "ms");
                        }
                        // check for too little elapsed time with a little fudge
                        else if (timer.Elapsed.TotalMilliseconds < msMin)
                        {
                            throw new APIException("Rate gate took too little to wait in between calls: " + timer.Elapsed.TotalMilliseconds + "ms");
                        }
                    }
                }
                Console.WriteLine("TestRateGate OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("TestRateGate fail: {0}", ex);
            }
        }

        private static void TestAESEncryption()
        {
            try
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
                if (!decrypted.SequenceEqual(data))
                {
                    throw new ApplicationException("AES encryption test fail");
                }

                byte[] protectedData = DataProtector.Protect(salt);
                byte[] unprotectedData = DataProtector.Unprotect(protectedData);
                if (!unprotectedData.SequenceEqual(salt))
                {
                    throw new ApplicationException("Protected data API fail");
                }
                Console.WriteLine("TestAESEncryption OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("TestAESEncryption failed: {0}", ex);
            }
        }

        private static void TestKeyStore()
        {
            try
            {
                // store keys
                string path = Path.Combine(Path.GetTempPath(), "keystore.test.bin");
                string publicKey  = "public key test aa45c0";
                string privateKey = "private key test bb270a";
                string[] keys = new string[] { publicKey, privateKey };

                CryptoUtility.SaveUnprotectedStringsToFile(path, keys);

                // read keys
                SecureString[] keysRead = CryptoUtility.LoadProtectedStringsFromFile(path);
                string publicKeyRead = CryptoUtility.SecureStringToString(keysRead[0]);
                string privateKeyRead = CryptoUtility.SecureStringToString(keysRead[1]);

                if (privateKeyRead != privateKey || publicKeyRead != publicKey)
                {
                    throw new InvalidDataException("TestKeyStore failed (mismatch)");
                }
                else
                {
                    Console.WriteLine("TestKeyStore OK");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestKeyStore failed ({ex.GetType().Name}: {ex.Message})");
            }
        }

        private static void TestRSAFromFile()
        {
            try
            {
                byte[] originalValue = new byte[256];
                new System.Random().NextBytes(originalValue);

                for (int i = 0; i < 4; i++)
                {
                    DataProtector.DataProtectionScope scope = (i < 2 ? DataProtector.DataProtectionScope.CurrentUser : DataProtector.DataProtectionScope.LocalMachine);
                    RSA rsa = DataProtector.RSAFromFile(scope);
                    byte[] encrypted = rsa.Encrypt(originalValue, RSAEncryptionPadding.Pkcs1);
                    byte[] decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
                    if (!originalValue.SequenceEqual(decrypted))
                    {
                        throw new InvalidDataException("TestRSAFromFile failure, original value not equal to decrypted value, scope: " + scope);
                    }
                }

                Console.WriteLine("TestRSAFromFile OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("TestRSAFromFile failed: {0}", ex);
            }
        }

        private static void TestExchanges()
        {
            IExchangeAPI[] apis = ExchangeAPI.GetExchangeAPIDictionary().Values.ToArray();
            foreach (IExchangeAPI api in apis)
            {
                // test all public API for each exchange
                try
                {
                    string symbol = GetSymbol(api);

                    IReadOnlyCollection<string> symbols = api.GetSymbols().ToArray();
                    Assert(symbols != null && symbols.Count != 0 && symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase));
                    Console.WriteLine($"API {api.Name} GetSymbols OK (default: {symbol}; {symbols.Count} symbols)");

                    ExchangeTrade[] trades = api.GetHistoricalTrades(symbol).ToArray();
                    Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
                    Console.WriteLine($"API {api.Name} GetHistoricalTrades OK ({trades.Length})");

                    var book = api.GetOrderBook(symbol);
                    Assert(book.Asks.Count != 0 && book.Bids.Count != 0 && book.Asks[0].Amount > 0m &&
                        book.Asks[0].Price > 0m && book.Bids[0].Amount > 0m && book.Bids[0].Price > 0m);
                    Console.WriteLine($"API {api.Name} GetOrderBook OK ({book.Asks.Count} asks, {book.Bids.Count} bids)");

                    trades = api.GetRecentTrades(symbol).ToArray();
                    Assert(trades.Length != 0 && trades[0].Price > 0m && trades[0].Amount > 0m);
                    Console.WriteLine($"API {api.Name} GetRecentTrades OK ({trades.Length} trades)");

                    var ticker = api.GetTicker(symbol);
                    Assert(ticker != null && ticker.Ask > 0m && ticker.Bid > 0m && ticker.Last > 0m &&
                        ticker.Volume != null && ticker.Volume.PriceAmount > 0m && ticker.Volume.QuantityAmount > 0m);
                    Console.WriteLine($"API {api.Name} GetTicker OK (ask: {ticker.Ask}, bid: {ticker.Bid}, last: {ticker.Last})");

                    try
                    {
                        var candles = api.GetCandles(symbol, 86400, DateTime.UtcNow.Subtract(TimeSpan.FromDays(7.0)), null).ToArray();
                        Assert(candles.Length != 0 && candles[0].ClosePrice > 0m && candles[0].HighPrice > 0m && candles[0].LowPrice > 0m && candles[0].OpenPrice > 0m &&
                            candles[0].HighPrice >= candles[0].LowPrice && candles[0].HighPrice >= candles[0].ClosePrice && candles[0].HighPrice >= candles[0].OpenPrice &&
                            !string.IsNullOrWhiteSpace(candles[0].Name) && candles[0].ExchangeName == api.Name && candles[0].PeriodSeconds == 86400 && candles[0].VolumePrice > 0.0 &&
                            candles[0].VolumeQuantity > 0.0 && candles[0].WeightedAverage >= 0m);

                        Console.WriteLine($"API {api.Name} GetCandles OK ({candles.Length})");
                    }
                    catch (NotSupportedException)
                    {
                        Console.WriteLine($"API {api.Name} GetCandles not supported");
                    }
                    catch (NotImplementedException)
                    {
                        Console.WriteLine($"API {api.Name} GetCandles not implemented");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Request failed, api: {0}, error: {1}", api, ex.Message);
                }
            }
        }

        private static void TestMovingAverageCalculator() {
            // MA without change
            const int len1 = 10;
            var ma1 = new MovingAverageCalculator(len1);
            for(int i=0; i<len1; i++) {
                ma1.NextValue(5.0);
            }
            Assert(ma1.IsMature);
            Assert(ma1.MovingAverage == 5.0);
            Assert(ma1.Slope == 0);
            Assert(ma1.ExponentialMovingAverage == 5.0);
            Assert(ma1.ExponentialSlope == 0);

            // constant rise
            const int len2 = 10;
            var ma2 = new MovingAverageCalculator(len2);
            for(int i=0; i<len2; i++) {
                ma2.NextValue(i);
            }
            Assert(ma2.IsMature);
            Assert(ma2.MovingAverage == 4.5);
            Assert(ma2.Slope == 0.5);
            Assert(ma2.ExponentialMovingAverage == 5.2393685730326505);
            Assert(ma2.ExponentialSlope == 0.83569590309968422);

            Console.WriteLine("TestMovingAverageCalculator OK");
        }

        public static void RunPerformTests(Dictionary<string, string> dict)
        {
            TestMovingAverageCalculator();
            TestRSAFromFile();
            TestAESEncryption();
            TestKeyStore();
            TestRateGate();
            TestExchanges();
        }
    }
}
