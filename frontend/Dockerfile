# syntax=docker/dockerfile:1

# SPA image: Vite build served by unprivileged nginx on 8080, proxying /api and /health.
#   docker build -t ghcr.io/mevljas/domolov-web frontend
# DOMOLOV_API_UPSTREAM (scheme://host:port, no trailing slash) is substituted at container start.

FROM node:24-alpine AS build
WORKDIR /app
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
RUN corepack enable
# Manifests first so dependency layers survive source-only changes.
# The optional files are globbed so the COPY works whether or not they exist.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml* .npmrc* ./
# Fetch the pnpm version pinned by "packageManager" once, into its own layer.
RUN corepack install
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
    pnpm install --frozen-lockfile --store-dir /pnpm/store
COPY . .
RUN pnpm build

FROM nginxinc/nginx-unprivileged:alpine AS web
ENV DOMOLOV_API_UPSTREAM=http://api:8080 \
    NGINX_ENVSUBST_FILTER=^DOMOLOV_
# The image's entrypoint renders /etc/nginx/templates/*.template into /etc/nginx/conf.d/.
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY nginx/security-headers.conf nginx/spa-security-headers.conf /etc/nginx/snippets/
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
