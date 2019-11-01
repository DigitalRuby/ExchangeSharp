using ExchangeSharp.API.Exchanges.BL3P.Enums;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Models
{
	internal class BL3PEmptyResponse
		: BL3PResponse<BL3PResponsePayload, BL3PResponsePayloadError>
	{
		[JsonProperty("data")]
		public override BL3PResponsePayload Data { get; set; }
	}

	internal abstract class BL3PResponse<TSuccess>
		: BL3PResponse<TSuccess, BL3PResponsePayloadError>
		where TSuccess : BL3PResponsePayload
	{
	}

	internal abstract class BL3PResponse<TSuccess, TFail>
		where TSuccess : BL3PResponsePayload
		where TFail : BL3PResponsePayloadError
	{
		[JsonProperty("result", Required = Required.Always)]
		public BL3PResponseType Result { get; set; }

		[JsonProperty("data", Required = Required.Always)]
		public virtual TSuccess Data { get; set; }

		/// <summary>
		/// Returns TSuccess or nothing
		/// </summary>
		public virtual TSuccess Unwrap()
		{
			return Result switch
			{
				BL3PResponseType.Success => Data,
				_ => null
			};
		}

		/// <summary>
		/// Returns TSuccess or throws an exception
		/// </summary>
		/// <exception cref="BL3PException"></exception>
		public virtual TSuccess Except()
		{
			return Result switch
			{
				BL3PResponseType.Success => Data,
				_ => throw new BL3PException(Data as TFail)
			};
		}
	}
}
