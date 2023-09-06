using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ExchangeSharp.API.Exchanges.Kraken.Models.Types
{
	internal enum ActionType
	{
		[EnumMember(Value = "subscribe")]
		Subscribe,

		[EnumMember(Value = "unsubscribe")]
		Unsubscribe
	}
}
