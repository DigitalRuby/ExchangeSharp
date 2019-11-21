### Building

#### Required tools
 - [.NET Core SDK 3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) (you can check the exact version on `global.json` in the root folder).

#### Editing (Optional tooling)

On all platforms:
 - JetBrains Rider
 - VS Code (with C#/Omnisharp extension)
 - MonoDevelop

On Windows:
 - Visual Studio 2019

On Mac:
 - Visual Studio for Mac

### Compiling

All Platforms (command line): `dotnet build ExchangeSharp.sln` \
Windows: Open ExchangeSharp.sln in Visual Studio and build/run \

#### Creating a release version

##### From the command line (bash/powershell):

`dotnet publish src/ExchangeSharpConsole -o $PWD/dist -c Release -r <RID>`

Change `<RID>` to whatever platform you are using. More info [here](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog#using-rids).

##### From Visual Studio

You can also publish from Visual Studio (right click project, select publish), which allows easily changing the platform, .NET core version and self-contained binary settings.