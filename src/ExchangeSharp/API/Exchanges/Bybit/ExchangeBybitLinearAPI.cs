using System;
using System.Collections.Generic;
using System.Text;
using ExchangeSharp.Bybit;

namespace ExchangeSharp
{
	public class ExchangeBybitLinearAPI : ExchangeBybitV5Base
	{
		protected override MarketCategory MarketCategory => MarketCategory.Linear;
		public override string BaseUrlWebSocket => "wss://stream.bybit.com/v5/public/linear";

		public ExchangeBybitLinearAPI() { }

		public ExchangeBybitLinearAPI(bool isUnifiedAccount)
		{
			IsUnifiedAccount = isUnifiedAccount;
		}
	}

	public partial class ExchangeName
	{
		public const string BybitLinear = "BybitLinear";
	}
}
