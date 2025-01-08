FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*
EXPOSE 50051

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["RecipesGrpc.csproj", "./"]
RUN dotnet restore "RecipesGrpc.csproj"

COPY . .

RUN dotnet build "RecipesGrpc.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RecipesGrpc.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["sh", "-c", "until pg_isready -h postgres -U postgres; do echo waiting for postgres; sleep 2; done; dotnet RecipesGrpc.dll"]
