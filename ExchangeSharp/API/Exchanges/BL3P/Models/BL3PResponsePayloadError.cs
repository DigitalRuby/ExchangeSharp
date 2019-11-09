using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3PResponsePayloadError : BL3PResponsePayload
	{
		[JsonProperty("code", Required = Required.Always)]
		public string ErrorCode { get; set; }

		[JsonProperty("message", Required = Required.Always)]
		public string Message { get; set; }
	}
}
