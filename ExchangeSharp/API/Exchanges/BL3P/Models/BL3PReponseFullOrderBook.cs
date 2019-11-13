using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3PReponseFullOrderBook : BL3PResponse<BL3POrderBook>
	{
		[JsonConverter(typeof(BL3PResponseConverter<BL3POrderBook>))]
		protected override BL3PResponsePayload Data { get; set; }
	}
}
