using System.Collections.Generic;
using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class WithdrawTemplates : GenericResponse
		{
			[JsonProperty("TemplateTypes")]
			public IEnumerable<string> TemplateTypes { get; set; }
		}
	}
}
