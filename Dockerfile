# syntax=docker/dockerfile:1
# Single-image build: React SPA + ASP.NET Core API in one container.

# 1) Build the React SPA -> /client/dist
FROM node:22-alpine AS client
WORKDIR /client
COPY src/clientapp/package*.json ./
RUN npm ci
COPY src/clientapp/ ./
# Override the dev outDir (../Bcrwlr.Api/wwwroot) to a local folder we can copy from.
RUN npm run build -- --outDir dist --emptyOutDir

# 2) Publish the API, bundling the SPA as wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Bcrwlr.Api/Bcrwlr.Api.csproj ./Bcrwlr.Api/
RUN dotnet restore ./Bcrwlr.Api/Bcrwlr.Api.csproj
COPY src/Bcrwlr.Api/ ./Bcrwlr.Api/
COPY --from=client /client/dist ./Bcrwlr.Api/wwwroot
RUN dotnet publish ./Bcrwlr.Api/Bcrwlr.Api.csproj -c Release -o /app /p:UseAppHost=false

# 3) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl is only needed for the container HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Archive__DataDir=/data

# Persist the archive (SQLite DB + saved articles) outside the container layer.
RUN mkdir -p /data && chown -R app:app /data
VOLUME ["/data"]

USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -fsS http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Bcrwlr.Api.dll"]
