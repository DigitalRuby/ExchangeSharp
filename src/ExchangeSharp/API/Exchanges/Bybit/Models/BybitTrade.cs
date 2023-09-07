using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.Bybit
{
	public class BybitTrade : ExchangeTrade
	{
		/// <summary>
		/// Cross sequence (internal value)
		/// </summary>
		public long CrossSequence { get; set; }

		public override string ToString()
		{
			return string.Format("{0},{1}", base.ToString(), CrossSequence);
		}
	}
}
