using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp
{
	public sealed class ExchangeFTXUSAPI : FTXGroupCommon
	{
		public override string BaseUrl { get; set; } = "https://ftx.us/api";
		public override string BaseUrlWebSocket { get; set; } = "wss://ftx.us/ws/";
	}

	public partial class ExchangeName
	{
		public const string FTXUS = "FTXUS";
	}
}
