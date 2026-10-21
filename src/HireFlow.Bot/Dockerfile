FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HireFlow.Bot/HireFlow.Bot.csproj src/HireFlow.Bot/
RUN dotnet restore src/HireFlow.Bot/HireFlow.Bot.csproj
COPY src/HireFlow.Bot/ src/HireFlow.Bot/
RUN dotnet publish src/HireFlow.Bot/HireFlow.Bot.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "HireFlow.Bot.dll"]
