# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the NuGet layer is cached between source changes.
COPY SchoolAdmission.sln ./
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.Api.Tests/SchoolAdmission.Api.Tests.csproj tests/SchoolAdmission.Api.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app/publish --no-restore

# ---------- test (used by `docker compose --profile test run backend-tests`) ----------
FROM build AS test
ENTRYPOINT ["dotnet", "test", "tests/SchoolAdmission.Api.Tests/SchoolAdmission.Api.Tests.csproj", "-c", "Release", "--no-restore", "--logger", "console;verbosity=normal"]

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=10 CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
