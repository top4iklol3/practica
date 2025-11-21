@echo off
chcp 65001 >nul
echo Скачивание json-20230618.jar...
echo.

REM Прямая ссылка на JAR файл из Maven Central
set JAR_URL=https://repo1.maven.org/maven2/org/json/json/20230618/json-20230618.jar
set JAR_FILE=json-20230618.jar

echo Загрузка из: %JAR_URL%
echo.

REM Проверяем наличие PowerShell для загрузки
powershell -Command "& {[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri '%JAR_URL%' -OutFile '%JAR_FILE%'}"

if exist "%JAR_FILE%" (
    echo.
    echo ✓ Успешно скачан: %JAR_FILE%
    echo Теперь можно запустить run.bat
) else (
    echo.
    echo ✗ Ошибка при скачивании!
    echo.
    echo Попробуйте скачать вручную:
    echo 1. Откройте в браузере: %JAR_URL%
    echo 2. Сохраните файл как: json-20230618.jar
    echo 3. Поместите в папку: %CD%
)

echo.
pause

