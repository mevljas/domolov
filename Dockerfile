# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Domolov.Domain/Domolov.Domain.csproj src/Domolov.Domain/
COPY src/Domolov.Application/Domolov.Application.csproj src/Domolov.Application/
COPY src/Domolov.Infrastructure/Domolov.Infrastructure.csproj src/Domolov.Infrastructure/
COPY src/Domolov/Domolov.csproj src/Domolov/
RUN dotnet restore src/Domolov/Domolov.csproj
COPY src/ src/
RUN dotnet publish src/Domolov/Domolov.csproj -c Release -o /app/publish /p:UseAppHost=false
WORKDIR /app/publish
RUN pwsh ./playwright.ps1 install --with-deps chromium

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOMOLOV_BROWSER_HEADLESS=false \
    DOMOLOV_BROWSER_USER_DATA_DIR=/data/browser-profile \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

COPY --from=build /app/publish .
COPY --from=build /root/.cache/ms-playwright /ms-playwright
COPY docker/entrypoint.sh /entrypoint.sh
# aspnet image has no pwsh; use the Node Playwright driver shipped under .playwright/
RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && node ./.playwright/package/cli.js install-deps chromium \
    && apt-get install -y --no-install-recommends xvfb \
    && rm -rf /var/lib/apt/lists/* \
    && sed -i 's/\r$//' /entrypoint.sh \
    && chmod +x /entrypoint.sh \
    && mkdir -p /data/browser-profile /app/logs

VOLUME ["/data/browser-profile"]
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
