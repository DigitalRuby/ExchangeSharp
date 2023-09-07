using System;
using System.Collections.Generic;
using System.Text;
using ExchangeSharp.Bybit;

namespace ExchangeSharp
{
	public class ExchangeBybitInverseAPI : ExchangeBybitV5Base
	{
		protected override MarketCategory MarketCategory => MarketCategory.Inverse;
		public override string BaseUrlWebSocket => "wss://stream.bybit.com/v5/public/inverse";

		public ExchangeBybitInverseAPI() { }

		public ExchangeBybitInverseAPI(bool isUnifiedAccount)
		{
			IsUnifiedAccount = isUnifiedAccount;
		}
	}

	public partial class ExchangeName
	{
		public const string BybitInverse = "BybitInverse";
	}
}
