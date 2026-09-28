# syntax=docker/dockerfile:1

# One publish, two runtime images:
#   docker build --target api    -t ghcr.io/mevljas/domolov-api .     # HTTP API, no browser
#   docker build --target worker -t ghcr.io/mevljas/domolov-worker .  # scheduler + headless Chromium
# The api image also runs one-shot CLI commands: `migrate`, `hash-password <pw>`.
# Headed Chromium under Xvfb lives in Dockerfile.dev. The SPA image is frontend/Dockerfile.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
# .editorconfig matters: EnforceCodeStyleInBuild + TreatWarningsAsErrors.
COPY Directory.Build.props Directory.Packages.props global.json .editorconfig ./
COPY src/Domolov.Domain/Domolov.Domain.csproj src/Domolov.Domain/
COPY src/Domolov.Application/Domolov.Application.csproj src/Domolov.Application/
COPY src/Domolov.Infrastructure/Domolov.Infrastructure.csproj src/Domolov.Infrastructure/
COPY src/Domolov.Api/Domolov.Api.csproj src/Domolov.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/Domolov.Api/Domolov.Api.csproj -a $TARGETARCH
COPY src/ src/
# A RID-specific publish keeps only this platform's Playwright Node driver.
# BuildHost-* come from EF Core Design (needed by dotnet-ef, not at runtime).
# The frontend/openapi document is a CI concern; the image context has no frontend/.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Domolov.Api/Domolov.Api.csproj -c Release -a $TARGETARCH --self-contained false \
    --no-restore -o /app/publish /p:UseAppHost=false /p:OpenApiGenerateDocuments=false \
    && rm -rf /app/publish/BuildHost-*

# The api image never launches a browser, so drop the Playwright driver (~100 MB).
FROM build AS publish-api
RUN rm -rf /app/publish/.playwright

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
# aspnet images ship no curl/wget; this bash /dev/tcp probe backs the Compose healthchecks.
COPY docker/healthcheck.sh /usr/local/bin/healthcheck
RUN sed -i 's/\r$//' /usr/local/bin/healthcheck && chmod 755 /usr/local/bin/healthcheck
EXPOSE 8080

FROM runtime AS api
ENV DOMOLOV_ROLE=api
COPY --from=publish-api /app/publish .
# Numeric UID ($APP_UID=1654, the base image's `app` user) so Kubernetes runAsNonRoot can verify it.
USER $APP_UID
ENTRYPOINT ["dotnet", "Domolov.Api.dll"]

FROM runtime AS worker
ENV DOMOLOV_ROLE=worker \
    DOMOLOV_BROWSER_HEADLESS=true \
    DOMOLOV_BROWSER_USER_DATA_DIR=/data/browser-profile \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
COPY --from=build /app/publish .
COPY docker/entrypoint.sh /entrypoint.sh
# Full Chromium (new headless mode), installed by the bundled Playwright driver so its version
# matches the Microsoft.Playwright package; --with-deps pulls the apt libraries and fonts.
# Xvfb comes in with Playwright's "tools" deps but headless never needs it: purge it so the
# entrypoint refuses headed mode here instead of half-working (headed lives in Dockerfile.dev).
RUN apt-get update \
    && ./.playwright/node/*/node ./.playwright/package/cli.js install --with-deps --no-shell chromium \
    && apt-get install -y --no-install-recommends fonts-liberation fonts-noto-color-emoji \
    && apt-get purge -y --auto-remove xvfb \
    && rm -rf /var/lib/apt/lists/* /ms-playwright/ffmpeg-* \
    && chmod -R a+rX /ms-playwright \
    && sed -i 's/\r$//' /entrypoint.sh \
    && chmod 755 /entrypoint.sh \
    && mkdir -p /data/browser-profile \
    && chown $APP_UID:$APP_UID /data/browser-profile
USER $APP_UID
# A fresh named volume copies this directory's ownership, so the app user can write it.
VOLUME ["/data/browser-profile"]
ENTRYPOINT ["/entrypoint.sh"]
