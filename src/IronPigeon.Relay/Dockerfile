#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app
EXPOSE 80
EXPOSE 443

WORKDIR /app
COPY /bin/IronPigeon.Relay/Release/net5.0/publish .
ENTRYPOINT ["dotnet", "IronPigeon.Relay.dll"]
