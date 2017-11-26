ExchangeSharp is a C# console app and framework for trading and communicating with various exchange API end points for stocks or cryptocurrency assets.

The following exchanges are supported:
- Bitfinex (public, basic private)
- Bittrex (public, basic private)
- Gemini (public, basic private)
- GDAX (public, basic private)
- Kraken (public, basic private)
- Poloniex (public, basic private)

Please send pull requests if you have made a change that you feel is worthwhile.

Nuget package: https://www.nuget.org/packages/DigitalRuby.ExchangeSharp/

---
Simple example:
---
```
ExchangeKrakenAPI api = new ExchangeKrakenAPI();
ExchangeTicker ticker = api.GetTicker("XXBTZUSD");
Console.WriteLine("On the Kraken exchange, 1 bitcoin is worth {0} USD.", ticker.Bid);

// load API keys created from ExchangeSharpConsole.exe keys mode=create path=keys.bin keylist=public_key,private_key
api.LoadAPIKeys("keys.bin");

/// place limit order for 0.01 bitcoin at ticker.Ask USD
ExchangeOrderResult result = api.PlaceOrder("XXBTZUSD", 0.01m, ticker.Ask, true);

// Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
//  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
// I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
//  their house in order.
System.Threading.Thread.Sleep(500);
result = api.GetOrderDetails(result.OrderId);

Console.WriteLine("Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.", ticker.Ask, result.Result, result.OrderId);
```
---

If this project has helped you in any way, donations are always appreciated. I maintain this code for free.

Missing a key piece of functionality or private or public API for your favorite exchange? Tips and donations are a great motivator :)
Donate via...

Bitcoin: 15i8jW7jHJLJjAsCD2zYVaqyf3it4kusLr

Ethereum: 0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc

Litecoin: LYrut92FnkBgd5Mmnt7RcyFpuTWn8vKFQq

Thanks for visiting!

- Jeff Johnson
