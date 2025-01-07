# Базовый образ для выполнения приложения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*
EXPOSE 50051

# Образ для сборки приложения
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем csproj файл для восстановления зависимостей
COPY ["RecipesGrpc.csproj", "./"]
RUN dotnet restore "RecipesGrpc.csproj"

# Копируем все оставшиеся файлы проекта
COPY . .

# Выполняем сборку
RUN dotnet build "RecipesGrpc.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Выполняем публикацию приложения
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RecipesGrpc.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Финальный образ для запуска приложения
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Выполняем миграции перед запуском приложения
ENTRYPOINT ["sh", "-c", "until pg_isready -h postgres -U postgres; do echo waiting for postgres; sleep 2; done; dotnet RecipesGrpc.dll"]
