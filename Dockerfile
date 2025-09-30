FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore RenderChatColor.sln
RUN dotnet publish RenderChatColor.ServerApp/RenderChatColor.ServerApp.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "RenderChatColor.ServerApp.dll"]
