using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.API.Exchanges.FTX.Models
{
	public class FTXTrade : ExchangeTrade
	{
		/// <summary>
		/// if the trade involved a liquidation order
		/// </summary>
		public bool IsLiquidationOrder { get; set; }

		public override string ToString()
		{
			return string.Format("{0}, IsLiquidation: {1}", base.ToString(), IsLiquidationOrder);
		}
	}
}
