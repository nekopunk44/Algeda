# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

# Files that can affect NuGet restore must be present before the cached restore layer.
COPY ["global.json", "Directory.Build.props", "./"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["API/API.csproj", "API/"]
RUN --mount=type=cache,id=algeda-nuget,target=/root/.nuget/packages \
    dotnet restore "API/API.csproj"

COPY . .
RUN --mount=type=cache,id=algeda-nuget,target=/root/.nuget/packages \
    dotnet publish "API/API.csproj" \
      --configuration Release \
      --output /app/publish \
      --no-restore \
      /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS final
WORKDIR /app

COPY --chown=$APP_UID:$APP_UID --from=build /app/publish ./
COPY --chmod=0555 deploy/healthcheck.sh /usr/local/bin/algeda-healthcheck

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD ["/usr/local/bin/algeda-healthcheck", "/health/live"]

ENTRYPOINT ["dotnet", "API.dll"]
