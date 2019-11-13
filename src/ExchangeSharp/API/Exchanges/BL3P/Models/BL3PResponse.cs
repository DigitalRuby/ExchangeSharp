using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3PEmptyResponse
		: BL3PResponse<BL3PResponsePayload, BL3PResponsePayloadError>
	{
		[JsonProperty("data")]
		protected override BL3PResponsePayload Data { get; set; }
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
		protected abstract BL3PResponsePayload Data { get; set; }

		[JsonIgnore]
		public virtual TSuccess Success => (TSuccess) Data;

		[JsonIgnore]
		public virtual TFail Error => (TFail) Data;

		/// <summary>
		/// Returns TSuccess or nothing
		/// </summary>
		public virtual TSuccess Unwrap()
		{
			return Result switch
			{
				BL3PResponseType.Success => Success,
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
				BL3PResponseType.Success => Success,
				_ => throw new BL3PException(Error)
			};
		}
	}
}
