/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp.BinanceGroup
{
	/// <summary>
	/// Binance DEX doesn't suppport streaming aggregate trades like Binance/US
	/// </summary>
	public class BinanceDEXTrade : ExchangeTrade
	{
		public string BuyerOrderId { get; set; }
		public string SellerOrderId { get; set; }
		public string BuyerAddress { get; set; }
		public string SellerAddress { get; set; }
		public TickerType TickerType { get; set; }
		public override string ToString()
		{
			return string.Format("{0},{1},{2},{3},{4},{5}", base.ToString(), BuyerOrderId, SellerOrderId, BuyerAddress, SellerAddress, TickerType);
		}
	}

	public enum TickerType : byte
	{ // tiekertype 0: Unknown 1: SellTaker 2: BuyTaker 3: BuySurplus 4: SellSurplus 5: Neutral
		Unknown = 0, SellTaker = 1, BuyTaker = 2, BuySurplus = 3, SellSurplus = 4, Neutral = 5
	}
}
