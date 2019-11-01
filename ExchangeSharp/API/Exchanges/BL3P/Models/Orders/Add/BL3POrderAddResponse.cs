using ExchangeSharp.API.Exchanges.BL3P.Converters;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models.Orders.Add
{
	internal class BL3POrderAddResponse : BL3PResponse<BL3POrderAddSuccess>

	{
		[JsonConverter(typeof(OrderAddResponseConverter))]
		public override BL3POrderAddSuccess Data { get; set; }
	}
}
