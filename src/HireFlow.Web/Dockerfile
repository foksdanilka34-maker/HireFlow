FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HireFlow.Web/HireFlow.Web.csproj src/HireFlow.Web/
RUN dotnet restore src/HireFlow.Web/HireFlow.Web.csproj
COPY src/HireFlow.Web/ src/HireFlow.Web/
RUN dotnet publish src/HireFlow.Web/HireFlow.Web.csproj -c Release --no-restore -o /out

FROM caddy:2.10
COPY --from=build /out/wwwroot /srv
COPY deploy/Caddyfile /etc/caddy/Caddyfile
