FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

# Restore dependencies (layer cache)
COPY *.csproj ./
RUN dotnet restore

# Build and publish
COPY . ./
RUN dotnet publish -c Release -o /app/out

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build-env /app/out .

# Create data directories
RUN mkdir -p /app/data /app/auth && chown -R appuser:appgroup /app/data /app/auth

ENV ASPNETCORE_URLS="http://+:5678" \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5678

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5678/users/test || exit 1

USER appuser

ENTRYPOINT ["dotnet", "ShoppingListServer.dll"]
