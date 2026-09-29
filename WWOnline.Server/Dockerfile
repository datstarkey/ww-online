# syntax=docker/dockerfile:1
#
# WW-Online dedicated server. The build context is the REPO ROOT (the server needs
# Directory.Build.props and WWOnline.Shared); .dockerignore at the root keeps everything else out:
#
#   docker build -f WWOnline.Server/Dockerfile -t ww-online-server .
#   docker run -d --name ww-online -p 6969:6969 ww-online-server
#
# Released images: ghcr.io/datstarkey/ww-online-server (.github/workflows/release.yml).
# Configuration (WWO_* environment variables), compose, TLS: docs/self-hosting.md.

# Build on the build machine's own platform: the output is framework-dependent IL, the same for
# every architecture, so a multi-arch build compiles once and never runs the SDK under emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# VERSION: SemVer without the "v" (1.2.3, 1.3.0-beta.1). Empty = Directory.Build.props's dev version.
ARG VERSION=
# COMMIT: git SHA, reported in /health and the startup log.
ARG COMMIT=
WORKDIR /src

# Restore first, from the project files only, so code changes reuse the cached restore layer.
COPY Directory.Build.props ./
COPY WWOnline.Shared/WWOnline.Shared.csproj WWOnline.Shared/
COPY WWOnline.Server/WWOnline.Server.csproj WWOnline.Server/
RUN dotnet restore WWOnline.Server/WWOnline.Server.csproj

COPY WWOnline.Shared/ WWOnline.Shared/
COPY WWOnline.Server/ WWOnline.Server/
RUN dotnet publish WWOnline.Server/WWOnline.Server.csproj -c Release --no-restore \
        -p:UseAppHost=false \
        ${VERSION:+-p:Version=$VERSION} \
        ${COMMIT:+-p:SourceRevisionId=$COMMIT} \
        -o /app

# Alpine: small, but it still has a shell (docker exec -it ww-online sh) and busybox wget for the
# health check. Runs as the image's non-root "app" user.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
ARG VERSION=
ARG COMMIT=
LABEL org.opencontainers.image.title="WW-Online server" \
      org.opencontainers.image.description="Dedicated room server for WW-Online, online multiplayer for The Legend of Zelda: The Wind Waker" \
      org.opencontainers.image.source="https://github.com/datstarkey/ww-online" \
      org.opencontainers.image.url="https://github.com/datstarkey/ww-online/blob/main/docs/self-hosting.md" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${COMMIT}"

WORKDIR /app
COPY --from=build /app ./
COPY LICENSE /app/LICENSE
# A writable place for WWO_LOG_FILE (mount a volume on it to keep the file).
RUN mkdir -p /data && chown "$APP_UID" /data

# The server binds 0.0.0.0:$WWO_PORT itself; clear the base image's ASPNETCORE_HTTP_PORTS (8080)
# so Kestrel doesn't warn that it overrides it.
ENV ASPNETCORE_HTTP_PORTS= \
    DOTNET_NOLOGO=true \
    WWO_PORT=6969

EXPOSE 6969
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --start-interval=2s --retries=3 \
    CMD wget -q -O /dev/null "http://127.0.0.1:${WWO_PORT:-6969}/health" || exit 1

# Exec form: the server is PID 1 and gets docker stop's SIGTERM (graceful shutdown).
ENTRYPOINT ["dotnet", "WWOnline.Server.dll"]
