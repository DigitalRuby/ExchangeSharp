using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {

            try
            {
                var bitstampapi = new ExchangeBitstampAPI();
                var bitstampticker = bitstampapi.GetTicker("XRPEUR");
                bitstampapi.LoadAPIKeysUnsecure("jQbPed5DTokTMbruVxK7og49ejYtwJ6P", "lwd4Svyu4vxtxEymaLGzBJSgUWK6ufKc");
                bitstampapi.CustomerId = "hxgc3964";
                //bitstampapi.PlaceOrder(new ExchangeOrderRequest
                //{
                //    Amount = 10,
                //    IsBuy = true,
                //    Price = bitstampticker.Ask,
                //    OrderType = OrderType.Market,
                //    Symbol = "XRPEUR"
                //});
                bitstampapi.Withdraw(new ExchangeWithdrawalRequest
                {
                    Symbol = "XRP",
                    Address = "rBqzBQj9fxijF4umn7gmzkgLy7aVMiV6xV",
                    Amount = 1,
                    TakeFeeFromAmount = true
                });
                //The code that causes the error goes here.
            }
            catch (ReflectionTypeLoadException ex)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    FileNotFoundException exFileNotFound = exSub as FileNotFoundException;
                    if (exFileNotFound != null)
                    {
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                        {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                string errorMessage = sb.ToString();
                //Display or log the error based on your application.
            }
            
        }
    }
}
