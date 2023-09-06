using ExchangeSharp.Bybit;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSharp
{
    public class ExchangeBybitOptionAPI : ExchangeBybitV5Base
    {
        protected override MarketCategory MarketCategory => MarketCategory.Option;
        public override string BaseUrlWebSocket => "wss://stream.bybit.com/v5/public/option";

        public ExchangeBybitOptionAPI() { }

        public ExchangeBybitOptionAPI(bool isUnifiedAccount)
        {
            IsUnifiedAccount = isUnifiedAccount;
        }
    }

    public partial class ExchangeName
    {
        public const string BybitOption = "BybitOption";
    }
}
