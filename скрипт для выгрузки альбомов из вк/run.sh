#!/bin/bash
# Скрипт для запуска VkPhotoDownloader на Linux/Mac
# Использование: ./run.sh

echo "Проверка Java..."
if ! command -v java &> /dev/null; then
    echo "ОШИБКА: Java не установлена или не найдена в PATH"
    echo "Установите Java 11 или выше"
    exit 1
fi

echo "Проверка Maven..."
if ! command -v mvn &> /dev/null; then
    echo "ВНИМАНИЕ: Maven не найден. Используется ручная компиляция..."
    echo ""
    echo "Убедитесь, что скачали json-20230618.jar в эту папку"
    echo "Скачать можно здесь: https://mvnrepository.com/artifact/org.json/json/20230618"
    echo ""
    read -p "Нажмите Enter для продолжения..."
    
    echo "Компиляция..."
    javac -cp json-20230618.jar VkPhotoDownloader.java
    if [ $? -ne 0 ]; then
        echo "ОШИБКА компиляции!"
        exit 1
    fi
    
    echo "Запуск..."
    java -cp .:json-20230618.jar VkPhotoDownloader
else
    echo "Сборка проекта..."
    mvn clean compile
    if [ $? -ne 0 ]; then
        echo "ОШИБКА сборки!"
        exit 1
    fi
    
    echo "Запуск..."
    mvn exec:java -Dexec.mainClass="VkPhotoDownloader"
fi

