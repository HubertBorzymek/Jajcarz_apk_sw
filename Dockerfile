# syntax=docker/dockerfile:1

# Etap 1: interfejs. Vite buduje go do src/Jajcarz.Server/wwwroot.
FROM node:24-alpine AS client
WORKDIR /repo/src/Jajcarz.WebClient
COPY src/Jajcarz.WebClient/package.json src/Jajcarz.WebClient/package-lock.json ./
RUN npm ci
COPY src/Jajcarz.WebClient/ ./
RUN npm run build

# Etap 2: serwer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Jajcarz.Domain/Jajcarz.Domain.csproj src/Jajcarz.Domain/
COPY src/Jajcarz.Application/Jajcarz.Application.csproj src/Jajcarz.Application/
COPY src/Jajcarz.Infrastructure/Jajcarz.Infrastructure.csproj src/Jajcarz.Infrastructure/
COPY src/Jajcarz.Server/Jajcarz.Server.csproj src/Jajcarz.Server/
RUN dotnet restore src/Jajcarz.Server/Jajcarz.Server.csproj
COPY src/Jajcarz.Domain/ src/Jajcarz.Domain/
COPY src/Jajcarz.Application/ src/Jajcarz.Application/
COPY src/Jajcarz.Infrastructure/ src/Jajcarz.Infrastructure/
COPY src/Jajcarz.Server/ src/Jajcarz.Server/
COPY --from=client /repo/src/Jajcarz.Server/wwwroot/ src/Jajcarz.Server/wwwroot/
RUN dotnet publish src/Jajcarz.Server/Jajcarz.Server.csproj --no-restore --configuration Release --output /app/publish

# Etap 3: uruchomienie.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Obraz nie ma klienta HTTP, a potrzebuje go sprawdzanie stanu aplikacji.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

# Klucze logowania są na wolumenie. Katalog powstaje wcześniej, żeby wolumen miał właściwego właściciela.
RUN mkdir -p /app/keys && chown $APP_UID /app/keys

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Pierwszy start wykonuje migracje, stąd długi czas przed pierwszym sprawdzeniem.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/healthz || exit 1

USER $APP_UID
ENTRYPOINT ["dotnet", "Jajcarz.Server.dll"]
