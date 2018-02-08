<img src='logo.png' width='600' />
<br/><br/>

[![GitHub issues](https://img.shields.io/github/issues/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/issues)
[![GitHub forks](https://img.shields.io/github/forks/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/network)
[![GitHub stars](https://img.shields.io/github/stars/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/stargazers)
[![GitHub license](https://img.shields.io/github/license/jjxtra/ExchangeSharp.svg)](https://github.com/jjxtra/ExchangeSharp/blob/master/LICENSE.txt)
[![Twitter](https://img.shields.io/twitter/url/https/github.com/jjxtra/ExchangeSharp.svg?style=social)](https://twitter.com/intent/tweet?text=Wow:&url=https%3A%2F%2Fgithub.com%2Fjjxtra%2FExchangeSharp)

[![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)](https://en.cryptobadges.io/donate/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)

[![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)](https://en.cryptobadges.io/donate/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)

[![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0x0d9Fc4ef1F1fBF8696D276678ef9fA2B6c1a3433)](https://en.cryptobadges.io/donate/0x0d9Fc4ef1F1fBF8696D276678ef9fA2B6c1a3433)

<div style="position: absolute; top: 62pt; left: 165pt;">
<form action="https://www.paypal.com/cgi-bin/webscr" method="post" target="_top">
<div style="float: left;">
<input type="hidden" name="cmd" value="_s-xclick">
<input type="hidden" name="hosted_button_id" value="L67Q4KQN5DHLY">
<input type="image" src="https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif" border="0" name="submit" alt="PayPal">
</div>
<div>
<img alt="" border="0" src="https://www.paypalobjects.com/en_US/i/scr/pixel.gif" width="1" height="1">
</div>
</form>
</div>

ExchangeSharp is a C# console app and framework for trading and communicating with various exchange API end points for stocks or cryptocurrency assets.

Visual Studio 2017 is required, along with either .NET 4.7 or .NET standard 2.0.

The following cryptocurrency exchanges are supported:

- Binance (public REST, basic private REST, public web socket (tickers))
- Bitfinex (public REST, basic private REST, public web socket (tickers), private web socket (orders))
- Bithumb (public REST)
- Bitstamp (public REST)
- Bittrex (public REST, basic private REST, public web socket (tickers))
- Gemini (public REST, basic private REST)
- GDAX (public REST, basic private REST)
- Kraken (public REST, basic private REST)
- Okex (basic public REST)
- Poloniex (public REST, basic private REST, public web socket (tickers))

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

I do cryptocurrency consulting, please don't hesitate to contact me if you have a custom solution you would like me to implement (jjxtra@gmail.com).

If you want help with your project, have questions that need answering or this project has helped you in any way, I accept donations.

[![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)](https://en.cryptobadges.io/donate/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)

[![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)](https://en.cryptobadges.io/donate/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)

[![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0x0d9Fc4ef1F1fBF8696D276678ef9fA2B6c1a3433)](https://en.cryptobadges.io/donate/0x0d9Fc4ef1F1fBF8696D276678ef9fA2B6c1a3433)

<div>
<form action="https://www.paypal.com/cgi-bin/webscr" method="post" target="_top">
<div>
<input type="hidden" name="cmd" value="_s-xclick">
<input type="hidden" name="hosted_button_id" value="L67Q4KQN5DHLY">
<input type="image" src="https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif" border="0" name="submit" alt="PayPal">
</div>
<div>
<img alt="" border="0" src="https://www.paypalobjects.com/en_US/i/scr/pixel.gif" width="1" height="1">
</div>
</form>
</div>

Thanks for visiting!

Jeff Johnson  
jjxtra@gmail.com  
http://www.digitalruby.com  
