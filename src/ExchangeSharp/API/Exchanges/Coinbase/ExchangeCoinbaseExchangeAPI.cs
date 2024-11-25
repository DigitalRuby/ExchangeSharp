using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp.Coinbase
{
	/// <summary>
	/// partial implementation for Coinbase Exchange, which is for businesses (rather than Advanced which is for individuals). Since there may not be many users of Coinbase Exchange, will not expose this for now to avoid confusion
	/// </summary>
	public sealed partial class ExchangeCoinbaseExchangeAPI : ExchangeCoinbaseAPI
	{
		protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
		{ // Coinbase Exchange uses the old signing method rather than JWT
			if (CanMakeAuthenticatedRequest(payload))
			{
				string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToStringInvariant(); // If you're skittish about the local clock, you may retrieve the timestamp from the Coinbase Site
				string body = CryptoUtility.GetJsonForPayload(payload);

				// V2 wants PathAndQuery, V3 wants LocalPath for the sig (I guess they wanted to shave a nano-second or two - silly)
				string path = request.RequestUri.AbsoluteUri.StartsWith(BaseUrlV2) ? request.RequestUri.PathAndQuery : request.RequestUri.LocalPath;
				string signature = CryptoUtility.SHA256Sign(timestamp + request.Method.ToUpperInvariant() + path + body, PrivateApiKey.ToUnsecureString());

				request.AddHeader("CB-ACCESS-KEY", PublicApiKey.ToUnsecureString());
				request.AddHeader("CB-ACCESS-SIGN", signature);
				request.AddHeader("CB-ACCESS-TIMESTAMP", timestamp);
				if (request.Method == "POST") await CryptoUtility.WriteToRequestAsync(request, body);
			}
		}
	}
}
