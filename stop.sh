#!/bin/bash

echo "🛑 Остановка Kaspi API приложения..."

# Останавливаем и удаляем контейнеры
docker-compose down

echo "✅ Приложение остановлено!"

# Опционально удаляем образы (раскомментируйте при необходимости)
