FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HireFlow.Domain/HireFlow.Domain.csproj src/HireFlow.Domain/
COPY src/HireFlow.Application/HireFlow.Application.csproj src/HireFlow.Application/
COPY src/HireFlow.Infrastructure/HireFlow.Infrastructure.csproj src/HireFlow.Infrastructure/
COPY src/HireFlow.Api/HireFlow.Api.csproj src/HireFlow.Api/
RUN dotnet restore src/HireFlow.Api/HireFlow.Api.csproj
COPY src/HireFlow.Domain/ src/HireFlow.Domain/
COPY src/HireFlow.Application/ src/HireFlow.Application/
COPY src/HireFlow.Infrastructure/ src/HireFlow.Infrastructure/
COPY src/HireFlow.Api/ src/HireFlow.Api/
RUN dotnet publish src/HireFlow.Api/HireFlow.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
# Каталоги под volume: резюме и ключи Data Protection, владелец — пользователь приложения.
RUN mkdir -p /app/storage /app/keys && chown $APP_UID /app/storage /app/keys
USER $APP_UID
ENTRYPOINT ["dotnet", "HireFlow.Api.dll"]
