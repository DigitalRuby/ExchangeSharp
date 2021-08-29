using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp.API.Exchanges.FTX.Models
{
	public sealed partial class ExchangeFTXAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://ftx.com/api";
	}
}
