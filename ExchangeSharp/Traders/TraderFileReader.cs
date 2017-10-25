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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Read trades from bin files, convert csv to bin file, etc.
    /// </summary>
    public class TraderFileReader
    {
        public static void ConvertCSVFilesToBinFiles(string folder)
        {
            foreach (string csvFile in Directory.GetFiles(folder, "*.csv", SearchOption.AllDirectories))
            {
                CreateBinFileFromCSVFiles(Path.Combine(Path.GetDirectoryName(csvFile), Path.GetFileNameWithoutExtension(csvFile) + ".bin"), csvFile);
            }
        }

        public static void CreateBinFileFromCSVFiles(string outputFile, params string[] inputFiles)
        {
            unsafe
            {
                Trade trade = new Trade();
                byte[] bytes = new byte[16];
                fixed (byte* ptr = bytes)
                {
                    foreach (string file in inputFiles)
                    {
                        using (StreamReader reader = new StreamReader(file, Encoding.ASCII))
                        using (Stream writer = File.Create(outputFile))
                        {
                            string line;
                            string[] lines;
                            DateTime dt;
                            while ((line = reader.ReadLine()) != null)
                            {
                                lines = line.Split(',');
                                if (lines.Length == 3)
                                {
                                    dt = CryptoUtility.UnixTimeStampToDateTimeSeconds(double.Parse(lines[0]));
                                    trade.Ticks = (long)CryptoUtility.UnixTimestampFromDateTimeMilliseconds(dt);
                                    trade.Price = float.Parse(lines[1]);
                                    trade.Amount = float.Parse(lines[2]);
                                    if (trade.Amount > 0.01f && trade.Price > 0.5f)
                                    {
                                        *(Trade*)ptr = trade;
                                        writer.Write(bytes, 0, bytes.Length);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static byte[] GetBytesFromBinFiles(string path, DateTime startDate, DateTime endDate)
        {
            string fileName;
            Match m;
            int year, month;
            MemoryStream stream = new MemoryStream();
            byte[] bytes;
            DateTime dt;
            int index;

            unsafe
            {
                Trade* ptrStart, ptrEnd, tradePtr;
                foreach (string binFile in Directory.GetFiles(path, "*.bin", SearchOption.AllDirectories))
                {
                    fileName = Path.GetFileNameWithoutExtension(binFile);
                    m = Regex.Match(fileName, "[0-9][0-9][0-9][0-9]-[0-9][0-9]$");
                    if (m.Success)
                    {
                        year = int.Parse(m.Value.Substring(0, 4));
                        month = int.Parse(m.Value.Substring(5, 2));
                        dt = new DateTime(year, month, startDate.Day, startDate.Hour, startDate.Minute, startDate.Second, startDate.Millisecond, DateTimeKind.Utc);
                        if (dt >= startDate && dt <= endDate)
                        {
                            bytes = File.ReadAllBytes(binFile);
                            fixed (byte* ptr = bytes)
                            {
                                index = 0;
                                ptrStart = (Trade*)ptr;
                                ptrEnd = (Trade*)(ptr + bytes.Length);
                                for (tradePtr = ptrStart; tradePtr != ptrEnd; tradePtr++)
                                {
                                    dt = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(tradePtr->Ticks);
                                    if (dt >= startDate && dt <= endDate)
                                    {
                                        stream.Write(bytes, index, sizeof(Trade));
                                    }
                                    index += sizeof(Trade);
                                    ptrStart++;
                                }
                            }
                        }
                    }
                }
            }

            return stream.ToArray();
        }
    }
}
