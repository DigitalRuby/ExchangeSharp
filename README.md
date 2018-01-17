ExchangeSharp is a C# console app and framework for trading and communicating with various exchange API end points for stocks or cryptocurrency assets.

Visual Studio 2017 is required, along with either .NET 4.7 or .NET standard 2.0.

The following cryptocurrency exchanges are supported:

- Binance (public, basic private)
- Bitfinex (public, basic private)
- Bithumb (public)
- Bitstamp (public)
- Bittrex (public, basic private)
- Gemini (public, basic private)
- GDAX (public, basic private)
- Kraken (public, basic private)
- Poloniex (public, basic private)

The following cryptocurrency services are supported:
- Cryptowatch (partial)

// TODO: Consider adding eTrade, although stocks are a low priority right now... :)

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

I do cryptocurrency consulting, please don't hesitate to contact me if you have a custom solution you would like me to implement (jjxtra@gmail.com).

If this project has helped you in any way or you need support / questions answered, donations are always appreciated. I maintain this code for free and for the glory of the crypto revolution.

Donation addresses...

Paypal: jjxtra@gmail.com (pick the send to friends and family with bank account option to avoid fees)

Bitcoin: 1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG  
Ethereum: 0x0d9Fc4ef1F1fBF8696D276678ef9fA2B6c1a3433  
Litecoin: LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i  
Vertcoin: Vcu6Fqh8MGiLEyyifNSCgoCuQShTijzwFx  

Thanks for visiting!

Jeff Johnson  
jjxtra@gmail.com  
http://www.digitalruby.com  
