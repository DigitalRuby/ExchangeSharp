using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class GenericResponse
		{
			[JsonProperty("result")]
			public bool Result { get; set; }
			[JsonProperty("errormsg")]
			public string ErrorMsg { get; set; }
			[JsonProperty("errorcode")]
			public int ErrorCode { get; set; }
			[JsonProperty("detail")]
			public int Detail { get; set; }
		}
	}
}
