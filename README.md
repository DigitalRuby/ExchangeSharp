<img src='logo.png' width='600' />

# Overview
ExchangeSharp is a C# console app and framework for trading and communicating with various exchange API end points for cryptocurrency assets. Many exchanges are supported, along with web sockets, withdraws and more!

I do cryptocurrency consulting, please don't hesitate to contact me if you have a custom solution you would like me to implement (exchangesharp@digitalruby.com).

## Features
- Many exchanges supported with public, private and web socket API
- Easy to use and well documented code and API
- Optional global symbol normalization, since each exchange has their own way of doing symbols
- Runs anywhere .NET core will run (Windows, MAC, Linux, iOS, Android, Unity 2018+, etc.)

## Exchanges
The following cryptocurrency exchanges are supported:  
(Web socket key: T = tickers, R = trades, B = order book, O = private orders)

|Exchange Name     |Public REST|Private REST |Web Socket |
| ---------------- | --------- | ----------- | --------- |
| Abucoins         | x         | x           | TO        |
| Binance          | x         | x           | TRB       |
| Bitfinex         | x         | x           | TO        |
| Bithumb          | x         |             |           |
| Bitstamp         | x         | x           |           |
| Bittrex          | x         | x           | T         |
| Bleutrade        | x         | x           |           |
| Cryptopia        | x         | x           |           |
| Gemini           | x         | x           |           |
| GDAX             | x         | x           | T         |
| Hitbtc           | x         | x           |           |
| Huobi            | x         | x           | RB        |
| Kraken           | x         | x           |           |
| Kucoin           | x         | x           |           |
| Livecoin         | x         | x           |           |
| Okex             | x         | x           | RB        |
| Poloniex         | x         | x           | TB        |
| TuxExchange      | x         | x           |           |
| Yobit            | x         | x           |           |

The following cryptocurrency services are supported:
- Cryptowatch (partial)

## Notes
ExchangeSharp uses 'symbol' to refer to markets, or pairs of currencies.

Please send pull requests if you have made a change that you feel is worthwhile, want a bug fixed or want a new feature. You can also donate to get new features.

## Building
Visual Studio 2017 is recommended. .NET 4.7.1+ or .NET core 2.0+ is required.  
<a href='https://www.nuget.org/packages/DigitalRuby.ExchangeSharp/'>Available on Nuget: ![NuGet](https://img.shields.io/nuget/dt/DigitalRuby.ExchangeSharp.svg)  
``` PM> Install-Package DigitalRuby.ExchangeSharp -Version 0.5.1 ```  
</a> 
```
Windows: Open ExchangeSharp.sln in Visual Studio and build/run  
Other Platforms: dotnet build ExchangeSharp.sln -f netcoreapp2.1
Ubuntu Release Example: dotnet build ExchangeSharp.sln -f netcoreapp2.1 -c Release -r ubuntu.16.10-x64
```

### Simple Example
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

### Web Socket Example
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

## I Can Help
I do cryptocurrency consulting, please don't hesitate to contact me if you have a custom solution you would like me to implement (exchangesharp@digitalruby.com).

## Donations Gratefully Accepted
If you want help with your project, have questions that need answering or this project has helped you in any way, I accept donations.

[![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)](https://en.cryptobadges.io/donate/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)

[![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)](https://en.cryptobadges.io/donate/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)

[![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)](https://en.cryptobadges.io/donate/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=L67Q4KQN5DHLY)

Donation totals:
0.02350084 BTC, 0.25 LTC

Thanks for visiting!

Jeff Johnson  
jeff@digitalruby.com  
http://www.digitalruby.com  
