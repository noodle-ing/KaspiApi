#!/bin/bash

echo "🚀 Запуск Kaspi API приложения на порте 9090..."

# Проверяем, установлен ли Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker не установлен. Пожалуйста, установите Docker."
    exit 1
fi

# Проверяем, установлен ли Docker Compose
if ! command -v docker compose &> /dev/null; then
    echo "❌ Docker Compose не установлен. Пожалуйста, установите Docker Compose."
    exit 1
fi

# Останавливаем и удаляем существующие контейнеры
echo "🛑 Останавливаем существующие контейнеры..."
docker compose down

# Собираем и запускаем приложение
echo "🔨 Собираем Docker образ..."
docker compose build

echo "🚀 Запускаем приложение..."
docker compose up -d

# Ждем несколько секунд для запуска
sleep 5

# Проверяем статус контейнера
if docker ps | grep -q kaspi-api-container; then
    echo "✅ Приложение успешно запущено!"
    echo "📍 API доступно по адресу: http://localhost:9090"
    echo "📖 Swagger документация: http://localhost:9090/swagger"
    echo ""
    echo "📝 Полезные команды:"
    echo "   - Посмотреть логи: docker-compose logs -f"
    echo "   - Остановить приложение: docker-compose down"
    echo "   - Перезапустить: docker-compose restart"
else
    echo "❌ Ошибка при запуске приложения!"
