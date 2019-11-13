using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class NDAXDepositInfo
		{
			[JsonProperty("AssetManagerId")]
			public long AssetManagerId { get; set; }

			[JsonProperty("AccountId")]
			public long AccountId { get; set; }

			[JsonProperty("AssetId")]
			public long AssetId { get; set; }

			[JsonProperty("ProviderId")]
			public long ProviderId { get; set; }

			[JsonProperty("DepositInfo")]
			public string DepositInfo { get; set; }

			[JsonProperty("result")]
			public bool Result { get; set; }

			[JsonProperty("errormsg")]
			public string Errormsg { get; set; }

			[JsonProperty("statuscode")]
			public long Statuscode { get; set; }

			public ExchangeDepositDetails ToExchangeDepositDetails(string cryptoCode)
			{
				if (!Result)
				{
					throw new APIException($"{Errormsg}");
				}

				var depositInfo = JsonConvert.DeserializeObject(DepositInfo) as JArray;
				var address = depositInfo.Last().ToStringInvariant();
				var addressTag = string.Empty;
				var split = address.Split(new[] { "?dt=", "?memoid=" }, StringSplitOptions.RemoveEmptyEntries);
				if (split.Length > 1)
				{
					address = split[0];
					addressTag = split[1];
				}
				return new ExchangeDepositDetails()
				{
					Address = address,
					AddressTag = addressTag,
					Currency = cryptoCode
				};

			}
		}
    }
}
