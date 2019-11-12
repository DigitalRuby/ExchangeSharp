When deploying a new nuget package, ensure that the assembly info versions are changed, along with the package version in the ExchangeSharp.csproj file.

Then turn on nuget package building under package tab in project settings and build in release mode. Package is in bin/Release.

The 'summary' tag needs to be manually copied from the csproj file into the nuspec file after Visual Studio builds the nuget package due to a bug in Visual Studio.