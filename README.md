<img src='logo.png' width='600' />

[![GitHub issues](https://img.shields.io/github/issues/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/issues)
[![GitHub forks](https://img.shields.io/github/forks/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/network)
[![GitHub stars](https://img.shields.io/github/stars/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/stargazers)
[![GitHub license](https://img.shields.io/github/license/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/blob/master/LICENSE.txt)
[![Twitter](https://img.shields.io/twitter/url/https/github.com/jjxtra/ExchangeSharp.svg?style=social)](https://twitter.com/intent/tweet?text=Wow:&url=https%3A%2F%2Fgithub.com%2Fjjxtra%2FExchangeSharp)

[![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)](https://en.cryptobadges.io/donate/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)

[![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)](https://en.cryptobadges.io/donate/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)

[![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)](https://en.cryptobadges.io/donate/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=L67Q4KQN5DHLY)

Donation totals:
0.25 LTC

<a href='https://www.nuget.org/packages/DigitalRuby.ExchangeSharp/'>Available on Nuget - ![NuGet](https://img.shields.io/nuget/dt/DigitalRuby.ExchangeSharp.svg)  
``` PM> Install-Package DigitalRuby.ExchangeSharp -Version 0.4.1 ```  
</a> 

ExchangeSharp is a C# console app and framework for trading and communicating with various exchange API end points for cryptocurrency assets. Many exchanges are supported, along with web sockets, withdraws and more!

Visual Studio 2017 is required, along with either .NET 4.7 or .NET standard 2.0.

The following cryptocurrency exchanges are supported:

- Abucoins (public REST, basic private REST, public web socket (tickers), private web socket (orders))
- Binance (public REST, basic private REST, public web socket (tickers))
- Bitfinex (public REST, basic private REST, public web socket (tickers), private web socket (orders))
- Bithumb (public REST)
- Bitstamp (public REST, basic private REST)
- Bittrex (public REST, basic private REST, public web socket (tickers))
- Bleutrade (public REST, basic private REST)
- Cryptopia (public REST, basic private REST)
- Gemini (public REST, basic private REST)
- GDAX (public REST, basic private REST, public web socket (tickers))
- Hitbtc (public REST, basic private REST)
- Huobi (public REST, basic private REST)
- Kraken (public REST, basic private REST)
- Kucoin (public REST, basic private REST)
- Livecoin (public REST, basic private REST)
- Okex (public REST, basic private REST)
- Poloniex (public REST, basic private REST, public web socket (tickers))
- TuxExchange (public REST, basic private REST)
- Yobit (public REST, basic private REST)

The following cryptocurrency services are supported:
- Cryptowatch (partial)

ExchangeSharp uses 'symbol' to refer to markets, or pairs of currencies.

Please send pull requests if you have made a change that you feel is worthwhile, want a bug fixed or want a new feature. You can also donate to get new features.

Nuget package: https://www.nuget.org/packages/DigitalRuby.ExchangeSharp/

**Building**  
```
Windows: Open ExchangeSharp.sln in Visual Studio and build/run  
Other Platforms: dotnet build ExchangeSharp.sln -f netcoreapp2.0
Ubuntu Release Example: dotnet build ExchangeSharp.sln -f netcoreapp2.0 -c Release -r ubuntu.16.10-x64
```

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
ExchangeOrderResult result = api.PlaceOrder(new ExchangeOrderRequest
{
    Amount = 0.01m,
    IsBuy = true,
    Price = ticker.Ask,
    Symbol = "XXBTZUSD"
});

// Kraken is a bit funny in that they don't return the order details in the initial request, so you have to follow up with an order details request
//  if you want to know more info about the order - most other exchanges don't return until they have the order details for you.
// I've also found that Kraken tends to fail if you follow up too quickly with an order details request, so sleep a bit to give them time to get
//  their house in order.
System.Threading.Thread.Sleep(500);
result = api.GetOrderDetails(result.OrderId);

Console.WriteLine("Placed an order on Kraken for 0.01 bitcoin at {0} USD. Status is {1}. Order id is {2}.", ticker.Ask, result.Result, result.OrderId);
```

---
Web socket example:
---
```
public static void Main(string[] args)
{
    // create a web socket connection to Binance. Note you can Dispose the socket anytime to shut it down.
    // the web socket will handle disconnects and attempt to re-connect automatically.
    ExchangeBinanceAPI b = new ExchangeBinanceAPI();
    using (var socket = b.GetTickersWebSocket((tickers) =>
    {
        Console.WriteLine("{0} tickers, first: {1}", tickers.Count, tickers.First());
    }))
    {
        Console.WriteLine("Press ENTER to shutdown.");
        Console.ReadLine();
    }
}
```
---

I do cryptocurrency consulting, please don't hesitate to contact me if you have a custom solution you would like me to implement (jeff@digitalruby.com).

If you want help with your project, have questions that need answering or this project has helped you in any way, I accept donations.

[![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)](https://en.cryptobadges.io/donate/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)

[![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)](https://en.cryptobadges.io/donate/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)

[![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)](https://en.cryptobadges.io/donate/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=L67Q4KQN5DHLY)

Donation totals:
0.25 LTC

<a href='https://www.nuget.org/packages/DigitalRuby.ExchangeSharp/'>Available on Nuget - ![NuGet](https://img.shields.io/nuget/dt/DigitalRuby.ExchangeSharp.svg)  
``` PM> Install-Package DigitalRuby.ExchangeSharp -Version 0.4.1 ```  
</a> 

Thanks for visiting!

Jeff Johnson  
jeff@digitalruby.com  
http://www.digitalruby.com  
