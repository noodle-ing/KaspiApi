# Используем официальный образ .NET 8.0 SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию
WORKDIR /src

# Копируем файлы проекта
COPY ["Jetqor kaspi api/Jetqor kaspi api.csproj", "Jetqor kaspi api/"]

# Восстанавливаем зависимости
RUN dotnet restore "Jetqor kaspi api/Jetqor kaspi api.csproj"

# Копируем весь исходный код
COPY . .

# Переходим в директорию проекта
WORKDIR "/src/Jetqor kaspi api"

# Собираем приложение
RUN dotnet build "Jetqor kaspi api.csproj" -c Release -o /app/build

# Публикуем приложение
RUN dotnet publish "Jetqor kaspi api.csproj" -c Release -o /app/publish

# Используем runtime образ для финального контейнера
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Устанавливаемся в рабочей директории
WORKDIR /app

# Копируем опубликованное приложение из предыдущего этапа
COPY --from=build /app/publish .

# Открываем порт 9090
EXPOSE 9090

# Устанавливаем переменную окружения для порта
ENV ASPNETCORE_URLS=http://+:9090

# Запускаем приложение
ENTRYPOINT ["dotnet", "Jetqor kaspi api.dll"]
