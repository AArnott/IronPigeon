# This script assumes the following command has already been run:
# dotnet publish -c release .\src\IronPigeon.Relay

FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app
EXPOSE 80
EXPOSE 443

WORKDIR /app
COPY /bin/IronPigeon.Relay/Release/net5.0/publish .
ENTRYPOINT ["dotnet", "IronPigeon.Relay.dll"]
