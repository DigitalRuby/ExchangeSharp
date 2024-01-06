<img src='logo.png' width='600' alt="Project's logo" />

[![Github Sponsorship](.github/github_sponsor_btn.svg)](https://github.com/sponsors/jjxtra)

[![NuGet](https://img.shields.io/nuget/dt/DigitalRuby.ExchangeSharp.svg)][nuget]

## Overview

ExchangeSharp is a C# **framework/lib** and [console app](#installing-the-cli) for trading and communicating with [various](#exchanges) exchange API end points for cryptocurrency assets. Many exchanges are supported, along with [web sockets](#websockets), withdraws and more!

Feel free to visit the discord channel at <https://discord.gg/58ktxXuTVK> and chat with other developers.

### Features

- Many [exchanges](/src//ExchangeSharp/API/Exchanges/) supported with public, private and web socket API
- Easy to use code and API
- Optional global market symbol normalization, since each exchange has their own way of doing market symbols
- Runs anywhere .NET runs. (Windows, Mac, Linux, Containers, Serverless, iOS, Android, [etc.](https://docs.microsoft.com/en-us/dotnet/core/about))
- Can be used from [many different C# platforms](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard2.0.md#platform-support)
- Has a great [CLI](#installing-the-cli) that enables you to use all features from all exchanges right from your command line.

### Exchanges

The following cryptocurrency exchanges are supported:  
(Web socket key: T = tickers, R = trades, B = orderbook / delta orderbook, O = private orders, U = user data)

| Exchange Name           | Public REST | Private REST | Web Socket    | Notes                                       |
| ----------------------- | ----------- | ------------ | ------------- | ------------------------------------------- |
| ApolloX                 | x           | x            | T R B O U     |                                             |
| Aquanow                 | wip         | x            |               |                                             |
| Binance                 | x           | x            | T R B O U     |                                             |
| ~~Binance Jersey~~      | ~~x~~       | ~~x~~        | ~~T R B O U~~ | Ceased operations                           |
| Binance.US              | x           | x            | T R B O U     |                                             |
| Binance DEX             |             |              |   R           |                                             |
| Bitbank                 | x           | x            |               |                                             |
| Bitfinex                | x           | x            | T R   O       |                                             |
| Bitflyer                |             |              |   R           |                                             |
| Bithumb                 | x           |              |   R           |                                             |
| BitMEX                  | x           | x            |   R   O       |                                             |
| Bitstamp                | x           | x            |   R           |                                             |
| Bittrex                 | x           | x            | T R           |                                             |
| BL3P                    | x           | x            |   R B         | Trades stream does not send trade's ids.    |
| Bleutrade               | x           | x            |               |                                             |
| BtcTurk                 |             |              |   R           |                                             |
| BTSE                    | x           | x            |               |                                             |
| Bybit                   | x           | x            |   R           | Has public method for Websocket Positions   |
| Coinbase (Pro)          | x           | x            | T R   O U     |                                             |
| Coincheck               |             |              |   R           |                                             |
| Coinmate                | x           | x            |               |                                             |
| Crypto.com              |             |              |   R           |                                             |
| Digifinex               | x           | x            |   R B         |                                             |
| Dydx                    |             |              |   R           |                                             |
| FTX                     | x           | x            | T R           |                                             |
| FTX.us                  | x           | x            | T R           |                                             |
| gate.io                 | x           | x            |   R           |                                             |
| Gemini                  | x           | x            | T R B         |                                             |
| HitBTC                  | x           | x            |   R           |                                             |
| Huobi                   | x           | x            |   R B         |                                             |
| Kraken                  | x           | x            |   R           | Dark order symbols not supported            |
| KuCoin                  | x           | x            | T R           |                                             |
| LBank                   | x           | x            |   R           |                                             |
| Livecoin                | x           | x            |               |                                             |
| NDAX                    | x           | x            | T R           |                                             |
| OKCoin                  | x           | x            |   R B         |                                             |
| OKEx                    | x           | x            | T R B O       |                                             |
| Poloniex                | x           | x            | T R B         |                                             |
| UPbit                   |             |              |   R           |                                             |
| YoBit                   | x           | x            |               |                                             |
| ZB.com                  | wip         |              |   R           |                                             |

The following cryptocurrency services are supported:

- Cryptowatch (partial)

Exchange constructors are private, to get access to an exchange in code use:

`ExchangeAPI.GetExchangeAPIAsync<>()`.

### Installing the CLI

On \*nix systems:

- Run this command `curl https://github.com/DigitalRuby/ExchangeSharp/raw/main/install-console.sh | sh`

On Windows (or manually):

- Download the [latest binaries](https://github.com/jjxtra/ExchangeSharp/releases/latest) for your OS.
- Unzip it into a folder that is in your environment variable `PATH` (`ctrl` + `shift` + `pause|break` -> Environment Variables)
- Use it from your preferred command-line emulator (e.g. Powershell, cmd, etc.)
- `exchange-sharp --help` shows all available commands

### Notes

ExchangeSharp uses **`marketSymbol`** to refer to markets, or pairs of currencies.

Please send pull requests if you have made a change that you feel is worthwhile, want a bug fixed or want a new feature. You can also donate to get new features.

### [Building/Compiling](./BUILDING.md)

### Websockets

If you must use an older Windows (older than win8.1), you'll need to use the [Websocket4Net][websocket4net] nuget package, and override the web socket implementation by calling

```csharp
ExchangeSharp.ClientWebSocket.RegisterWebSocketCreator(
    () => new ExchangeSharpConsole.WebSocket4NetClientWebSocket()
);
```

See [`WebSocket4NetClientWebSocket.cs`][websocket4net] for implementation details.

### Nuget

#### dotnet CLI

[`dotnet add package DigitalRuby.ExchangeSharp --version 1.0.4`][nuget]

#### Package Manager on VS

[`PM> Install-Package DigitalRuby.ExchangeSharp -Version 1.0.4`][nuget]

### Examples

#### Creating an order

There's a lot of examples on how to use the API in our [console application](./src/ExchangeSharpConsole/Options). \
e.g.
[`ExampleOption.cs`](./src/ExchangeSharpConsole/Options/ExampleOption.cs)

#### Getting ticker info via Web Sockets

```csharp
public static async Task Main(string[] args)
{
    // create a web socket connection to Binance. Note you can Dispose the socket anytime to shut it down.
    using var api = await ExchangeAPI.GetExchangeAPIAsync<ExchangeBinanceAPI>();
    // the web socket will handle disconnects and attempt to re-connect automatically.
    using var socket = await api.GetTickersWebSocket(tickers =>
    {
        Console.WriteLine("{0} tickers, first: {1}", tickers.Count, tickers.First());
    });

    Console.WriteLine("Press ENTER to shutdown.");
    Console.ReadLine(true);
}
```

### Authentication

Private api calls like placing orders require you to call `LoadApiKeys` first. You can generate an api keys file by running the bundled console application and choosing the generate key file option.

### Logging

ExchangeSharp uses NLog internally _currently_. To log, use `ExchangeSharp.Logger`.

Do **not** use `Console.WriteLine` to log messages in the lib project.

Provide your own `nlog.config` or `app.config` nlog configuration if you want to change logging settings or turn logging off.

### Caching

The `ExchageAPI` class provides a method caching mechanism. Use `MethodCachePolicy` to put caching behind public methods, or clear to remove caching. Some methods are cached by default. You can set `ExchangeAPI.UseDefaultMethodCachePolicy` to `false` to stop all caching as well.

You can also set request cache policy if you want to tweak how the http caching behaves.

### How to contribute

Please read the [contributing guideline](CONTRIBUTING.md) **before** submitting a **pull request**.

### Consulting

I'm happy to make customizations to the software for you and keep in private repo, email <exchangesharp@digitalruby.com>.

### Donations Gratefully Accepted

Believe it or not, donations are quite rare. I've posted publicly the total donation amounts below. If ExchangeSharp has helped you in any way, please consider donating.

[![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)](https://en.cryptobadges.io/donate/1GBz8ithHvTqeRZxkmpHx5kQ9wBXuSH8AG)

[![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)](https://en.cryptobadges.io/donate/LWxRMaVFeXLmaq5munDJxADYYLv2szYi9i)

[![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)](https://en.cryptobadges.io/donate/0x77d3D990859a8c3e3486b5Ad63Da223f7F3778dc)

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=L67Q4KQN5DHLY)

Donation totals:
0.039 BTC, 10.25 LTC

Thanks for visiting!

Jeff Johnson  
<jeff@digitalruby.com>  
<http://www.digitalruby.com>

[nuget]: https://www.nuget.org/packages/DigitalRuby.ExchangeSharp/
[websocket4net]: https://github.com/kerryjiang/WebSocket4Net
