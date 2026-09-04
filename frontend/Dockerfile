# syntax=docker/dockerfile:1
#
# Build context is the repository root (the codegen step reads the Gateway's committed
# src/Gateway/WeakAppHandler.Gateway.Api/schema.graphql, which sits outside frontend/) - build with:
#   docker build -f frontend/Dockerfile -t weakapphandler-frontend .
#
# See frontend/Dockerfile.dockerignore for what actually reaches this build from that root context.

FROM node:22-alpine AS build
WORKDIR /repo

COPY frontend/package.json frontend/package-lock.json frontend/
RUN npm --prefix frontend ci

COPY frontend/ frontend/
COPY src/Gateway/WeakAppHandler.Gateway.Api/schema.graphql src/Gateway/WeakAppHandler.Gateway.Api/schema.graphql

# Baked in at build time (Vite inlines import.meta.env.VITE_* as static string literals, see
# frontend/src/shared/config/runtimeConfig.ts) - left unset here to keep the image itself
# environment-agnostic; TASK-047's compose wiring passes the real per-environment values via
# `docker build --build-arg`.
ARG VITE_GATEWAY_GRAPHQL_HTTP_URL
ARG VITE_GATEWAY_GRAPHQL_WS_URL
ARG VITE_NOTIFICATION_ALERTS_HUB_URL
ARG VITE_GATEWAY_REST_URL
ARG VITE_NOTIFICATION_API_URL
ARG VITE_AUTH_API_URL
ENV VITE_GATEWAY_GRAPHQL_HTTP_URL=$VITE_GATEWAY_GRAPHQL_HTTP_URL \
    VITE_GATEWAY_GRAPHQL_WS_URL=$VITE_GATEWAY_GRAPHQL_WS_URL \
    VITE_NOTIFICATION_ALERTS_HUB_URL=$VITE_NOTIFICATION_ALERTS_HUB_URL \
    VITE_GATEWAY_REST_URL=$VITE_GATEWAY_REST_URL \
    VITE_NOTIFICATION_API_URL=$VITE_NOTIFICATION_API_URL \
    VITE_AUTH_API_URL=$VITE_AUTH_API_URL

RUN npm --prefix frontend run build

# nginxinc/nginx-unprivileged (not the plain nginx image) already runs as a non-root "nginx" user
# (uid 101) and listens on 8080 instead of 80, so it needs no custom user/port setup to satisfy the
# same non-root requirement the .NET services meet via $APP_UID.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=nginx:nginx /repo/frontend/dist /usr/share/nginx/html

EXPOSE 8080

# Read-only rootfs / no-new-privileges (runtime flags set where the container is started, TASK-047's
# compose wiring - see src/Auth/WeakAppHandler.Auth/Dockerfile for the same note on the .NET side):
# nginx itself only ever writes to /var/cache/nginx and /tmp (for its pid file), so
# `docker run --read-only --security-opt=no-new-privileges --tmpfs /var/cache/nginx --tmpfs /tmp`
# is all `--read-only` needs here; no other path under the image is ever written to.
