using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.Bitflyer
{
	public class BitflyerTrade : ExchangeTrade
	{
		public string BuyChildOrderAcceptanceId { get; set; }
		public string SellChildOrderAcceptanceId { get; set; }

		public override string ToString()
		{
			return string.Format(
					"{0},{1}, {2}",
					base.ToString(),
					BuyChildOrderAcceptanceId,
					SellChildOrderAcceptanceId
			);
		}
	}
}
