using System;
using System.Collections.Generic;
using System.Text;
using ExchangeSharp.Bybit;

namespace ExchangeSharp
{
	public class ExchangeBybitSpotAPI : ExchangeBybitV5Base
	{
		protected override MarketCategory MarketCategory => MarketCategory.Spot;
		public override string BaseUrlWebSocket => "wss://stream.bybit.com/v5/public/spot";

		public ExchangeBybitSpotAPI() { }

		public ExchangeBybitSpotAPI(bool isUnified)
		{
			IsUnifiedAccount = isUnified;
		}
	}

	public partial class ExchangeName
	{
		public const string BybitSpot = "BybitSpot";
	}
}
