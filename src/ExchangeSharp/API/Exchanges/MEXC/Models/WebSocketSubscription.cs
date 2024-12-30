using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.API.Exchanges.MEXC.Models
{
	internal class WebSocketSubscription
	{
		public string Method { get; set; }

		public List<string> Params { get; set; }
	}
}
