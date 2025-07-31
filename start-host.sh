#!/bin/bash

echo "🚀 Запуск Kaspi API с host networking..."

# Проверяем, установлен ли Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker не установлен. Пожалуйста, установите Docker."
    exit 1
fi

# Останавливаем существующий контейнер если он есть
echo "🛑 Останавливаем существующие контейнеры..."
docker stop kaspiapi-kaspi-api-1 2>/dev/null || true
docker rm kaspiapi-kaspi-api-1 2>/dev/null || true

# Собираем образ
echo "🔨 Собираем Docker образ..."
docker build -t kaspiapi-kaspi-api .

# Запускаем с host network
echo "🚀 Запускаем приложение с host networking..."
docker run -d \
  --network host \
  --name kaspiapi-kaspi-api-1 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ASPNETCORE_URLS=http://+:9090 \
  -v "$(pwd)/Jetqor kaspi api/appsettings.json:/app/appsettings.json:ro" \
  kaspiapi-kaspi-api

# Ждем несколько секунд для запуска
sleep 5

# Проверяем статус контейнера
if docker ps | grep -q kaspiapi-kaspi-api-1; then
    echo "✅ Приложение успешно запущено с host networking!"
    echo "📍 API доступно по адресу: http://localhost:9090"
    echo "📖 Swagger документация: http://localhost:9090/swagger"
    echo ""
    echo "📝 Полезные команды:"
    echo "   - Посмотреть логи: docker logs -f kaspiapi-kaspi-api-1"
    echo "   - Остановить приложение: docker stop kaspiapi-kaspi-api-1"
    echo "   - Войти в контейнер: docker exec -it kaspiapi-kaspi-api-1 /bin/bash"
else
    echo "❌ Ошибка при запуске приложения!"
    echo "📝 Для диагностики используйте: docker logs kaspiapi-kaspi-api-1"
fi