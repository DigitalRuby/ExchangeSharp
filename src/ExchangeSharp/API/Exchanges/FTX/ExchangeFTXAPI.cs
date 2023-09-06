using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp
{
	public sealed class ExchangeFTXAPI : FTXGroupCommon
	{
		public override string BaseUrl { get; set; } = "https://ftx.com/api";
		public override string BaseUrlWebSocket { get; set; } = "wss://ftx.com/ws/";
	}

	public partial class ExchangeName
	{
		public const string FTX = "FTX";
	}
}
