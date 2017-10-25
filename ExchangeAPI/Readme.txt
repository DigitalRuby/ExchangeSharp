
ExchangeSharp, v 0.1.0.0

Thanks for using ExchangeSharp, a full featured C# API to interact with cryptocurrency, stock and other exchanges.

Simple example:

// --------------------------------------------------------------------------------

using ExchangeSharp;

public static class ExchangeSharpExample
{
	public static void Example()
	{
		ExchangeKrakenAPI api = new ExchangeKrakenAPI();
		ExchangeTicker ticker = api.GetTicker("XXBTZUSD");
		Console.WriteLine("On the Kraken exchange, 1 bitcoin is worth {0} USD.", ticker.Bid);

		// load API keys created from ExchangeSharpConsole.exe keys mode=create path=keys.bin keylist=key1,key2,key3,key4
		// assuming key1 is the public key for Kraken and key2 is the private key for Kraken
		System.Security.SecureString[] keys = CryptoUtility.LoadProtectedStringsFromFile("keys.bin");
		api.PublicApiKey = keys[0];
		api.PrivateApiKey = keys[1];

		/// place limit order for 0.01 bitcoin at ticker.Ask USD
		ExchangeOrderResult result = api.PlaceOrder("XXBTZUSD", 0.01m, ticker.Ask, true);

		// Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
		//  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
		// I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
		//  their house in order.
		System.Threading.Thread.Sleep(500);
		result = api.GetOrderDetails(result.OrderId);

		Console.WriteLine("Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.", ticker.Ask, result.Result, result.OrderId);
	}
}

// --------------------------------------------------------------------------------

- Jeff Johnson (jjxtra)
http://www.digitalruby.com