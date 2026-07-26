# Build from the repository root:  docker build -f src/PromptRegistry.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/PromptRegistry.Core/PromptRegistry.Core.csproj src/PromptRegistry.Core/
COPY src/PromptRegistry.Api/PromptRegistry.Api.csproj  src/PromptRegistry.Api/
RUN dotnet restore src/PromptRegistry.Api/PromptRegistry.Api.csproj

COPY src/PromptRegistry.Core/ src/PromptRegistry.Core/
COPY src/PromptRegistry.Api/  src/PromptRegistry.Api/
RUN dotnet publish src/PromptRegistry.Api/PromptRegistry.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Migrations are applied at startup from this directory.
COPY db/migrations ./db/migrations
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PromptRegistry.Api.dll"]
