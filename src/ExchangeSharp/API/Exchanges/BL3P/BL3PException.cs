using System;
using System.Runtime.Serialization;

namespace ExchangeSharp.BL3P
{
	[Serializable]
	internal class BL3PException : Exception
	{
		public string ErrorCode { get; }

		internal BL3PException(BL3PResponsePayloadError error)
			: this(error?.Message)
		{
			if (error == null)
				throw new ArgumentNullException(nameof(error));
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
