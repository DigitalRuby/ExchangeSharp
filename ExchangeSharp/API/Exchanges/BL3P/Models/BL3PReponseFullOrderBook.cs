using ExchangeSharp.API.Exchanges.BL3P.Converters;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models
{
	internal class BL3PReponseFullOrderBook : BL3PResponse<BL3POrderBook>
	{
		[JsonConverter(typeof(BL3PResponseConverter<BL3POrderBook>))]
		protected override BL3PResponsePayload Data { get; set; }
	}
}
