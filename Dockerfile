# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Galen.Integration.sln", "./"]
COPY ["src/Galen.Integration.Domain/Galen.Integration.Domain.csproj", "src/Galen.Integration.Domain/"]
COPY ["src/Galen.Integration.Application/Galen.Integration.Application.csproj", "src/Galen.Integration.Application/"]
COPY ["src/Galen.Integration.Infrastructure/Galen.Integration.Infrastructure.csproj", "src/Galen.Integration.Infrastructure/"]
COPY ["src/Galen.Integration.Functions/Galen.Integration.Functions.csproj", "src/Galen.Integration.Functions/"]
COPY ["tests/Galen.Integration.Tests.Unit/Galen.Integration.Tests.Unit.csproj", "tests/Galen.Integration.Tests.Unit/"]
COPY ["tests/Galen.Integration.Tests.Integration/Galen.Integration.Tests.Integration.csproj", "tests/Galen.Integration.Tests.Integration/"]

RUN dotnet restore "Galen.Integration.sln"

COPY . .
RUN dotnet build "src/Galen.Integration.Functions/Galen.Integration.Functions.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/Galen.Integration.Functions/Galen.Integration.Functions.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - Azure Functions on .NET 8
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=publish ["/app/publish", "/home/site/wwwroot"]