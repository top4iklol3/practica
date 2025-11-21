@echo off
chcp 65001 >nul
REM Скрипт для запуска VkPhotoDownloader на Windows

echo Проверка Java...
java -version >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: Java не установлена или не найдена в PATH
    echo Установите Java 11 или выше с https://adoptium.net/
    pause
    exit /b 1
)

echo Проверка Maven...
mvn -version >nul 2>&1
if not errorlevel 1 (
    echo Сборка проекта через Maven...
    mvn clean compile
    if errorlevel 1 (
        echo ОШИБКА сборки!
        pause
        exit /b 1
    )
    echo Запуск...
    mvn exec:java -Dexec.mainClass="VkPhotoDownloader"
    pause
    exit /b 0
)

echo ВНИМАНИЕ: Maven не найден. Используется ручная компиляция...
echo.

if not exist "json-20230618.jar" (
    echo ========================================
    echo ОШИБКА: json-20230618.jar не найден!
    echo ========================================
    echo.
    echo Вам нужно скачать библиотеку JSON:
    echo.
    echo Вариант 1: Установите Maven
    echo   Скачайте с https://maven.apache.org/download.cgi
    echo.
    echo Вариант 2: Скачайте JAR файл вручную
    echo   1. Откройте: https://repo1.maven.org/maven2/org/json/json/20230618/
    echo   2. Скачайте файл: json-20230618.jar
    echo   3. Сохраните в эту папку
    echo.
    echo После скачивания запустите run.bat снова
    echo ========================================
    pause
    exit /b 1
)

echo Файл json-20230618.jar найден
echo.
echo Компиляция...
javac -encoding UTF-8 -cp json-20230618.jar VkPhotoDownloader.java
if errorlevel 1 (
    echo ОШИБКА компиляции!
    pause
    exit /b 1
)

echo Запуск...
java -cp .;json-20230618.jar VkPhotoDownloader
pause
