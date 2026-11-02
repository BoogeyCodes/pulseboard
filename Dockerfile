# ---- frontend ----
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run lint && npm test && npm run build

# ---- backend ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY PulseBoard.sln ./
COPY src/PulseBoard.Api/PulseBoard.Api.csproj src/PulseBoard.Api/
COPY tests/PulseBoard.Api.Tests/PulseBoard.Api.Tests.csproj tests/PulseBoard.Api.Tests/
RUN dotnet restore
COPY src/ src/
COPY tests/ tests/
RUN dotnet test --no-restore --configuration Release
RUN dotnet publish src/PulseBoard.Api -c Release -o /app --no-restore

# ---- runtime: one container serves the API, the hub and the built SPA ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/pulseboard.db"
VOLUME /data
EXPOSE 8080

COPY --from=api /app .
COPY --from=web /web/dist ./wwwroot
USER app
ENTRYPOINT ["dotnet", "PulseBoard.Api.dll"]
