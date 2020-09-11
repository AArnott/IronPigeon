# Contributing

All contributions made to this library must be under terms compatible with the license in the accompanying LICENSE file.

This project has adopted the [Microsoft Open Source Code of
Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct
FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com)
with any additional questions or comments.

## Best practices

* Use Windows PowerShell or [PowerShell Core][pwsh] (including on Linux/OSX) to run .ps1 scripts.
  Some scripts set environment variables to help you, but they are only retained if you use PowerShell as your shell.

## Prerequisites

1. [SQL Server LocalDB](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-ver15). Manual installation required. This may already be included in your VS installation.
1. [Azure Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator). `init.ps1` can install this.
1. [.NET Core SDK](https://get.dot.net/) `init.ps1` can install this.

   You should install the version specified in `global.json` or a later version within
the same major.minor.Bxx "hundreds" band.
For example if 2.2.300 is specified, you may install 2.2.300, 2.2.301, or 2.2.310
while the 2.2.400 version would not be considered compatible by .NET SDK.
See [.NET Core Versioning](https://docs.microsoft.com/en-us/dotnet/core/versions/) for more information.

All other dependencies can be installed by running the `init.ps1` script at the root of the repository
using Windows PowerShell or [PowerShell Core][pwsh] (on any OS).
Some dependencies installed by `init.ps1` may only be discoverable from the same command line environment the init script was run from due to environment variables, so be sure to launch Visual Studio or build the repo from that same environment.
Alternatively, run `init.ps1 -InstallLocality Machine` (which may require elevation) in order to install dependencies at machine-wide locations so Visual Studio and builds work everywhere.

## Package restore

The easiest way to restore packages may be to run `init.ps1` which automatically authenticates
to the feeds that packages for this repo come from, if any.
`dotnet restore` or `nuget restore` also work but may require extra steps to authenticate to any applicable feeds.

## Building

This repository can be built on Windows, Linux, and OSX.

Building, testing, and packing this repository can be done by using Visual Studio or the standard dotnet CLI commands (e.g. `dotnet build`, `dotnet test`, `dotnet pack`, etc.).

## Testing

Our unit tests requires that the Azure Storage Emulator be started first.
After installing the emulator (see Prerequisites) it can be started from the Windows Start menu.
Running the `init` script in the repo root also starts the Azure Storage Emulator.

[pwsh]: https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-6
