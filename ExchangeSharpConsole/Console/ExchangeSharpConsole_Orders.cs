using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ExchangeSharp;

namespace ExchangeSharpConsole
{
    public static partial class ExchangeSharpConsoleMain
    {
        public static void RunGetOrderHistory(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "exchangeName", "marketSymbol");

            string exchangeName = dict["exchangeName"];
            IExchangeAPI api = ExchangeAPI.GetExchangeAPI(exchangeName);
            string marketSymbol = dict["marketSymbol"];

            Authenticate(api);

            DateTime? startDate = null;
            if (dict.ContainsKey("startDate"))
            {
                startDate = DateTime.Parse(dict["startDate"]).ToUniversalTime();
            }

            var completedOrders = api.GetCompletedOrderDetailsAsync(marketSymbol, startDate).Sync();
            foreach (var completedOrder in completedOrders)
            {
                Console.WriteLine(completedOrder);
            }

            Console.Write("Press enter to exit..");
            Console.ReadLine();
        }

        public static void RunGetOrderDetails(Dictionary<string, string> dict)
        {
            RequireArgs(dict, "exchangeName", "orderId");

            string exchangeName = dict["exchangeName"];
            IExchangeAPI api = ExchangeAPI.GetExchangeAPI(exchangeName);
            string orderId = dict["orderId"];

            Authenticate(api);

            string marketSymbol = null;
            if (dict.ContainsKey("marketSymbol"))
            {
                marketSymbol = dict["marketSymbol"];
            }

            var orderDetails = api.GetOrderDetailsAsync(orderId, marketSymbol).Sync();
            Console.WriteLine(orderDetails);

            Console.Write("Press enter to exit..");
            Console.ReadLine();
        }

        private static void Authenticate(IExchangeAPI api)
        {
            Console.Write("Enter Public Api Key: ");
            var publicApiKey = GetSecureInput();
            api.PublicApiKey = publicApiKey;
            Console.WriteLine();
            Console.Write("Enter Private Api Key: ");
            var privateApiKey = GetSecureInput();
            api.PrivateApiKey = privateApiKey;
            Console.WriteLine();
            Console.Write("Enter Passphrase: ");
            var passphrase = GetSecureInput();
            api.Passphrase = passphrase;
            Console.WriteLine();
        }

        private static SecureString GetSecureInput()
        {
            var pwd = new SecureString();
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (i.Key == ConsoleKey.Backspace)
                {
                    if (pwd.Length > 0)
                    {
                        pwd.RemoveAt(pwd.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                {
                    pwd.AppendChar(i.KeyChar);
                    Console.Write("*");
                }
            }

            return pwd;
        }
    }
}