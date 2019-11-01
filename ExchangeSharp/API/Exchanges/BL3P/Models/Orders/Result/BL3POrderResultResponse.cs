using ExchangeSharp.API.Exchanges.BL3P.Converters;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models.Orders.Result
{
	internal class BL3POrderResultResponse : BL3PResponse<BL3POrderResultSuccess>
	{
		[JsonConverter(typeof(BL3PResponseConverter<BL3POrderResultSuccess>))]
		public override BL3POrderResultSuccess Data { get; set; }
	}
}
