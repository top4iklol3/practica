# Скрипт для скачивания фотографий из группы ВКонтакте (Java версия)

## Быстрый старт

1. **Установите Java 11+** (если еще не установлена)
2. **Получите токен VK API** на https://vkhost.github.io/
3. **Откройте `VkPhotoDownloader.java`** и вставьте токен в `ACCESS_TOKEN`
4. **Запустите:**
   - Windows: `run.bat`
   - Linux/Mac: `chmod +x run.sh && ./run.sh`

## Требования

- **Java 11 или выше** (JDK)
- **Maven 3.6+** (для управления зависимостями) - опционально

### Проверка установки

Проверьте, установлена ли Java:
```bash
java -version
```

Если установлен Maven, проверьте:
```bash
mvn -version
```

### Установка Java (если не установлена)

- **Windows**: Скачайте с https://adoptium.net/ или https://www.oracle.com/java/technologies/downloads/
- **Linux**: `sudo apt install openjdk-11-jdk` (Ubuntu/Debian) или `sudo yum install java-11-openjdk` (CentOS/RHEL)
- **Mac**: `brew install openjdk@11`

### Установка Maven (если используете Вариант 1)

- **Windows**: Скачайте с https://maven.apache.org/download.cgi и добавьте в PATH
- **Linux**: `sudo apt install maven` (Ubuntu/Debian) или `sudo yum install maven` (CentOS/RHEL)
- **Mac**: `brew install maven`

## Установка и запуск

### Быстрый запуск (рекомендуется)

**Windows:**
```bash
run.bat
```

**Linux/Mac:**
```bash
chmod +x run.sh
./run.sh
```

Скрипт автоматически проверит наличие Java и Maven, и запустит программу.

### Вариант 1: С Maven (рекомендуется)

1. Убедитесь, что Maven установлен
2. Соберите проект и загрузите зависимости:
```bash
mvn clean compile
```

3. Запустите:
```bash
mvn exec:java -Dexec.mainClass="VkPhotoDownloader"
```

Или соберите JAR и запустите:
```bash
mvn package
java -cp target/vk-photo-downloader-1.0.jar:target/dependency/* VkPhotoDownloader
```

### Вариант 2: Без Maven (ручная установка)

1. Скачайте библиотеку JSON:
   - Перейдите на https://mvnrepository.com/artifact/org.json/json/20230618
   - Скачайте JAR файл (например, `json-20230618.jar`)
   - Сохраните его в той же папке, где находится `VkPhotoDownloader.java`

2. Скомпилируйте:
```bash
javac -cp json-20230618.jar VkPhotoDownloader.java
```

3. Запустите:

**Windows:**
```bash
java -cp .;json-20230618.jar VkPhotoDownloader
```

**Linux/Mac:**
```bash
java -cp .:json-20230618.jar VkPhotoDownloader
```

**Примечание:** На Windows используйте `;` для разделения путей, на Linux/Mac - `:`

## Получение токена VK API

1. Перейдите на https://vkhost.github.io/
2. Выберите необходимые права (например, "VK Admin" или "Photos")
3. Скопируйте полученный токен

## Использование

1. Откройте `VkPhotoDownloader.java`
2. Вставьте ваш токен в переменную `ACCESS_TOKEN`
3. Укажите ID группы в переменной `GROUP_ID` (можно использовать короткое имя, например "durov")
4. Запустите скрипт

## Настройки

- `ACCESS_TOKEN` - токен доступа VK API
- `GROUP_ID` - ID или короткое имя группы
- `OUTPUT_FOLDER` - папка для сохранения фотографий (по умолчанию "vk_photos")
- `PHOTO_COUNT` - количество фотографий для скачивания из каждого альбома
- `ALBUM_IDS` - массив ID конкретных альбомов для скачивания (null = все альбомы)
- `ALBUM_NAMES` - массив названий альбомов для скачивания (null = все альбомы)
- `SHOW_ALBUMS_LIST` - показать список всех альбомов перед скачиванием

## Выбор конкретных альбомов

### Вариант 1: По ID альбомов
```java
int[] ALBUM_IDS = {123456, 789012};  // Укажите ID нужных альбомов
String[] ALBUM_NAMES = null;
```

### Вариант 2: По названиям альбомов
```java
int[] ALBUM_IDS = null;
String[] ALBUM_NAMES = {"Фото 2024", "Отдых"};  // Укажите названия нужных альбомов
```

### Просмотр списка альбомов
Чтобы увидеть все доступные альбомы с их ID и названиями:
```java
boolean SHOW_ALBUMS_LIST = true;
```

Или вызовите метод напрямую:
```java
downloader.listAlbums(GROUP_ID);
```

## Примечания

- Если не указать `ALBUM_IDS` и `ALBUM_NAMES` (оставить null), скрипт скачает фотографии из всех альбомов группы
- Фотографии сохраняются в максимальном доступном разрешении
- Для каждого альбома создается отдельная папка: `{id_альбома}_{название_альбома}/`
- Имена файлов: `{id_фото}.jpg`

## Решение проблем

### Ошибка: "javac: command not found"
- Убедитесь, что Java JDK установлена (не только JRE)
- Проверьте, что Java добавлена в PATH

### Ошибка: "mvn: command not found"
- Установите Maven или используйте Вариант 2 (без Maven)
- Проверьте, что Maven добавлен в PATH

### Ошибка: "package org.json does not exist"
- Убедитесь, что библиотека JSON скачана и указана в classpath
- При использовании Maven выполните `mvn clean compile` для загрузки зависимостей

### Ошибка: "Could not find or load main class VkPhotoDownloader"
- Убедитесь, что файл скомпилирован: `javac VkPhotoDownloader.java`
- Проверьте, что вы находитесь в правильной директории

