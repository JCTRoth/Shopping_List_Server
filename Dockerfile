# syntax=docker/dockerfile:1

# The build stage always runs on the *host* architecture ($BUILDPLATFORM) and
# cross-publishes for the image architecture ($TARGETARCH). Running the .NET SDK
# under QEMU emulation (e.g. building linux/amd64 on an Apple Silicon Mac) is
# extremely memory hungry and typically dies with:
#   Out of memory.
#   qemu: uncaught target signal 6 (Aborted) - core dumped
# Building natively avoids the emulator entirely and is much faster.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
ARG TARGETARCH
WORKDIR /app

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# ${TARGETARCH:+-a "$TARGETARCH"} is a no-op when TARGETARCH is unset (plain
# `docker build`), and pins the target RID when buildx provides it.
COPY WebApi.csproj ./
RUN dotnet restore WebApi.csproj ${TARGETARCH:+-a "$TARGETARCH"}

COPY . ./
RUN dotnet publish WebApi.csproj -c Release -o out ${TARGETARCH:+-a "$TARGETARCH"}

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Local file storage for the user shopping lists ("AppSettings:DataStorageFolder").
# In Kubernetes this path is backed by a PersistentVolumeClaim (see k8s/storage.yaml).
RUN mkdir -p /app/data

# Firebase admin credentials (only used when "UseFirebase" is enabled).
ENV GOOGLE_APPLICATION_CREDENTIALS=/app/auth/cert-files/google-firebase-admin-sdk-key/shoppingnow-1519d-firebase-adminsdk-fqk5x-36c14e1451.json

# The app listens on port 5678. --urls (command line) wins over UseUrls() in Program.cs.
EXPOSE 5678
ENV ASPNETCORE_URLS=http://0.0.0.0:5678

ENTRYPOINT ["dotnet", "ShoppingListServer.dll", "--urls", "http://0.0.0.0:5678"]

