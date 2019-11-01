using System;
using System.Runtime.Serialization;
using ExchangeSharp.API.Exchanges.BL3P.Models;

namespace ExchangeSharp.API.Exchanges.BL3P
{
	[Serializable]
	public class BL3PException : Exception
	{
		public string ErrorCode { get; }

		internal BL3PException(BL3PResponsePayloadError error)
			: this(error.Message)
		{
			ErrorCode = error.ErrorCode;
		}

		public BL3PException(string message)
			: base(message)
		{
		}

		public BL3PException(string message, Exception inner)
			: base(message, inner)
		{
		}

		protected BL3PException(
			SerializationInfo info,
			StreamingContext context
		) : base(info, context)
		{
		}
	}
}
