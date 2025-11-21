import java.io.*;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Scanner;
import org.json.JSONArray;
import org.json.JSONObject;

/**
 * Скрипт для скачивания фотографий из группы ВКонтакте на Java
 * Требуется: библиотека org.json (добавить в проект)
 */
public class VkPhotoDownloader {
    
    private static final String API_VERSION = "5.131";
    private String accessToken;
    
    public VkPhotoDownloader(String accessToken) {
        this.accessToken = accessToken;
    }
    
    /**
     * Проверяет валидность токена и права доступа
     */
    public void checkTokenAccess(String groupId) {
        System.out.println("\n========================================");
        System.out.println("=== Проверка токена и прав доступа ===");
        System.out.println("========================================\n");
        
        try {
            // Проверка 1: Базовая валидность токена
            System.out.println("1. Проверка валидности токена...");
            String url = "https://api.vk.com/method/users.get" +
                         "?access_token=" + accessToken +
                         "&v=" + API_VERSION;
            
            String response = sendGetRequest(url);
            JSONObject json = new JSONObject(response);
            
            if (json.has("error")) {
                JSONObject error = json.getJSONObject("error");
                int errorCode = error.getInt("error_code");
                String errorMsg = error.getString("error_msg");
                System.out.println("   ✗ ОШИБКА: " + errorMsg + " (код: " + errorCode + ")");
                System.out.println("   Токен недействителен или истек срок действия!");
                return;
            }
            
            JSONArray users = json.getJSONArray("response");
            if (users.length() > 0) {
                JSONObject user = users.getJSONObject(0);
                System.out.println("   ✓ Токен валиден");
                System.out.println("   Пользователь: " + user.optString("first_name") + " " + user.optString("last_name"));
            }
            
            // Проверка 2: Доступ к информации о группе
            System.out.println("\n2. Проверка доступа к группе '" + groupId + "'...");
            JSONObject groupInfo = getGroupInfo(groupId);
            String groupName = groupInfo.getString("name");
            int groupIdNum = groupInfo.getInt("id");
            System.out.println("   ✓ Доступ к группе есть");
            System.out.println("   Группа: " + groupName + " (ID: " + groupIdNum + ")");
            
            // Проверка 3: Доступ к альбомам группы
            System.out.println("\n3. Проверка доступа к альбомам группы...");
            JSONArray albums = getAlbums(-groupIdNum);
            System.out.println("   ✓ Доступ к альбомам есть");
            System.out.println("   Найдено альбомов: " + albums.length());
            
            if (albums.length() > 0) {
                System.out.println("\n   Первые альбомы:");
                int showCount = Math.min(3, albums.length());
                for (int i = 0; i < showCount; i++) {
                    JSONObject album = albums.getJSONObject(i);
                    System.out.println("   - " + album.getString("title") + " (ID: " + album.getInt("id") + ")");
                }
            }
            
            // Проверка 4: Доступ к фотографиям (пробуем получить одну фотографию)
            System.out.println("\n4. Проверка доступа к фотографиям...");
            if (albums.length() > 0) {
                int testAlbumId = albums.getJSONObject(0).getInt("id");
                JSONArray photos = getPhotos(-groupIdNum, testAlbumId, 1);
                
                if (photos.length() > 0) {
                    JSONObject photo = photos.getJSONObject(0);
                    JSONArray sizes = photo.optJSONArray("sizes");
                    boolean hasUrl = false;
                    
                    if (sizes != null && sizes.length() > 0) {
                        JSONObject maxPhoto = getMaxSizePhoto(sizes);
                        if (maxPhoto != null && maxPhoto.has("url")) {
                            hasUrl = true;
                        }
                    }
                    
                    if (hasUrl) {
                        System.out.println("   ✓ Доступ к фотографиям есть");
                        System.out.println("   URL фотографий доступны для скачивания");
                    } else {
                        System.out.println("   ⚠ Доступ к фотографиям ограничен");
                        System.out.println("   URL фотографий недоступны - возможно, нужны дополнительные права");
                    }
                } else {
                    System.out.println("   ⚠ Альбом пуст или нет доступа к фотографиям");
                }
            }
            
            System.out.println("\n=== Проверка завершена ===");
            System.out.println("\nРекомендации:");
            System.out.println("- Если URL фотографий недоступны, получите новый токен с правами:");
            System.out.println("  https://vkhost.github.io/");
            System.out.println("- Выберите права: 'Photos' или 'VK Admin'");
            
        } catch (Exception e) {
            System.err.println("\n✗ ОШИБКА при проверке: " + e.getMessage());
            e.printStackTrace();
        }
    }
    
    /**
     * Выводит список всех альбомов группы
     */
    public void listAlbums(String groupId) {
        try {
            JSONObject groupInfo = getGroupInfo(groupId);
            String groupName = groupInfo.getString("name");
            int groupIdNum = groupInfo.getInt("id");
            
            JSONArray albums = getAlbums(-groupIdNum);
            
            System.out.println("\n=== Альбомы группы '" + groupName + "' ===\n");
            for (int i = 0; i < albums.length(); i++) {
                JSONObject album = albums.getJSONObject(i);
                int albumId = album.getInt("id");
                String albumTitle = album.getString("title");
                int photosCount = album.optInt("size", 0);
                System.out.printf("ID: %6d | Название: %-50s | Фото: %d%n", 
                    albumId, albumTitle, photosCount);
            }
            System.out.println();
        } catch (Exception e) {
            System.err.println("Ошибка при получении списка альбомов: " + e.getMessage());
        }
    }
    
    /**
     * Скачивает фотографии из группы ВКонтакте
     */
    public void downloadPhotosFromGroup(String groupId, String outputFolder, int count, 
                                       int[] albumIds, String[] albumNames) {
        try {
            // Создаем папку для сохранения
            Path folder = Paths.get(outputFolder);
            Files.createDirectories(folder);
            
            System.out.println("Начинаю скачивание фотографий из группы " + groupId + "...");
            
            // Получаем информацию о группе
            JSONObject groupInfo = getGroupInfo(groupId);
            String groupName = groupInfo.getString("name");
            int groupIdNum = groupInfo.getInt("id");
            
            System.out.println("Группа: " + groupName);
            
            // Получаем альбомы группы
            JSONArray albums = getAlbums(-groupIdNum);
            
            // Фильтруем альбомы, если указаны конкретные
            JSONArray albumsToProcess = new JSONArray();
            for (int i = 0; i < albums.length(); i++) {
                JSONObject album = albums.getJSONObject(i);
                int albumId = album.getInt("id");
                String albumTitle = album.getString("title");
                
                boolean shouldInclude = false;
                
                // Если фильтры не указаны - берем все
                if (albumIds == null && albumNames == null) {
                    shouldInclude = true;
                } else {
                    // Проверяем по ID
                    if (albumIds != null) {
                        for (int id : albumIds) {
                            if (albumId == id) {
                                shouldInclude = true;
                                break;
                            }
                        }
                    }
                    // Проверяем по названию
                    if (!shouldInclude && albumNames != null) {
                        for (String name : albumNames) {
                            if (albumTitle.equals(name)) {
                                shouldInclude = true;
                                break;
                            }
                        }
                    }
                }
                
                if (shouldInclude) {
                    albumsToProcess.put(album);
                }
            }
            
            if (albumsToProcess.length() == 0) {
                System.out.println("⚠ Не найдено альбомов по указанным критериям!");
                System.out.println("Используйте метод listAlbums() чтобы увидеть все доступные альбомы");
                return;
            }
            
            int totalDownloaded = 0;
            
            // Проходим по каждому альбому
            for (int i = 0; i < albumsToProcess.length(); i++) {
                JSONObject album = albumsToProcess.getJSONObject(i);
                int albumId = album.getInt("id");
                String albumTitle = album.getString("title");
                
                System.out.println("\nОбрабатываю альбом: " + albumTitle);
                
                // Создаем папку для альбома (очищаем название от недопустимых символов)
                String safeAlbumName = albumTitle.replaceAll("[<>:\"/\\\\|?*]", "_");
                Path albumFolder = folder.resolve(albumId + "_" + safeAlbumName);
                Files.createDirectories(albumFolder);
                System.out.println("Создана папка: " + albumFolder);
                
                // Получаем фотографии из альбома
                JSONArray photos = getPhotos(-groupIdNum, albumId, count);
                System.out.println("Получено фотографий из API: " + photos.length());
                
                // Скачиваем каждую фотографию
                for (int j = 0; j < photos.length(); j++) {
                    JSONObject photo = photos.getJSONObject(j);
                    int photoId = photo.getInt("id");
                    
                    // Берем фото максимального размера
                    String photoUrl = null;
                    
                    // Сначала пробуем получить URL из массива sizes
                    JSONArray sizes = photo.optJSONArray("sizes");
                    if (sizes != null && sizes.length() > 0) {
                        JSONObject maxSizePhoto = getMaxSizePhoto(sizes);
                        if (maxSizePhoto != null) {
                            photoUrl = maxSizePhoto.optString("url");
                            // Отладка для первых 3 фото
                            if (j < 3 && (photoUrl == null || photoUrl.isEmpty())) {
                                System.out.println("    [DEBUG] maxSizePhoto найден, но URL пустой");
                                System.out.println("    [DEBUG] maxSizePhoto keys: " + maxSizePhoto.keySet());
                            }
                        } else if (j < 3) {
                            System.out.println("    [DEBUG] getMaxSizePhoto вернул null");
                        }
                    }
                    
                    // Если не получилось, пробуем прямые поля (photo_2560, photo_1280, и т.д.)
                    if (photoUrl == null || photoUrl.isEmpty()) {
                        String[] sizeFields = {"photo_2560", "photo_1280", "photo_807", "photo_604", "photo_130", "photo_75"};
                        for (String field : sizeFields) {
                            photoUrl = photo.optString(field);
                            if (photoUrl != null && !photoUrl.isEmpty()) {
                                break;
                            }
                        }
                    }
                    
                    // Если все еще нет URL, пробуем получить через метод photos.getById
                    if (photoUrl == null || photoUrl.isEmpty()) {
                        try {
                            photoUrl = getPhotoUrlById(-groupIdNum, photoId);
                        } catch (Exception e) {
                            // Игнорируем ошибку
                        }
                    }
                    
                    // Отладочный вывод для первых 3 фотографий
                    if (j < 3 && (photoUrl == null || photoUrl.isEmpty())) {
                        System.out.println("  [DEBUG] Фото " + photoId + ":");
                        System.out.println("    - sizes: " + (sizes != null ? sizes.length() + " элементов" : "null"));
                        if (sizes != null && sizes.length() > 0) {
                            for (int k = 0; k < Math.min(3, sizes.length()); k++) {
                                JSONObject size = sizes.getJSONObject(k);
                                System.out.println("      [" + k + "] type: " + size.optString("type") + ", url: " + 
                                    (size.has("url") ? "есть" : "нет"));
                            }
                        }
                        System.out.println("    - photoUrl после всех попыток: " + (photoUrl != null ? "найден" : "не найден"));
                    }
                    
                    if (photoUrl == null || photoUrl.isEmpty()) {
                        System.out.println("  ✗ Пропущено фото " + photoId + ": нет доступного URL");
                        continue;
                    }
                    
                    // Формируем имя файла
                    String fileName = photoId + ".jpg";
                    String filePath = albumFolder.resolve(fileName).toString();
                    
                    // Скачиваем фото
                    try {
                        downloadFile(photoUrl, filePath);
                        totalDownloaded++;
                        System.out.println("  ✓ Скачано: " + fileName + " (" + totalDownloaded + ")");
                    } catch (Exception e) {
                        System.out.println("  ✗ Ошибка при скачивании " + photoId + ": " + e.getMessage());
                    }
                }
            }
            
            System.out.println("\n✓ Готово! Скачано фотографий: " + totalDownloaded);
            System.out.println("Фотографии сохранены в папке: " + folder.toAbsolutePath());
            
        } catch (Exception e) {
            System.err.println("Ошибка: " + e.getMessage());
            e.printStackTrace();
            System.out.println("\nВозможные причины:");
            System.out.println("1. Неверный токен доступа");
            System.out.println("2. Неверный ID группы");
            System.out.println("3. Нет доступа к группе");
        }
    }
    
    /**
     * Получает информацию о группе
     */
    private JSONObject getGroupInfo(String groupId) throws Exception {
        String url = "https://api.vk.com/method/groups.getById" +
                     "?group_id=" + groupId +
                     "&access_token=" + accessToken +
                     "&v=" + API_VERSION;
        
        String response = sendGetRequest(url);
        JSONObject json = new JSONObject(response);
        
        if (json.has("error")) {
            throw new Exception("Ошибка API: " + json.getJSONObject("error").getString("error_msg"));
        }
        
        return json.getJSONArray("response").getJSONObject(0);
    }
    
    /**
     * Получает альбомы группы
     */
    private JSONArray getAlbums(int ownerId) throws Exception {
        String url = "https://api.vk.com/method/photos.getAlbums" +
                     "?owner_id=" + ownerId +
                     "&access_token=" + accessToken +
                     "&v=" + API_VERSION;
        
        String response = sendGetRequest(url);
        JSONObject json = new JSONObject(response);
        
        if (json.has("error")) {
            throw new Exception("Ошибка API: " + json.getJSONObject("error").getString("error_msg"));
        }
        
        return json.getJSONObject("response").getJSONArray("items");
    }
    
    /**
     * Получает фотографии из альбома
     */
    private JSONArray getPhotos(int ownerId, int albumId, int count) throws Exception {
        String url = "https://api.vk.com/method/photos.get" +
                     "?owner_id=" + ownerId +
                     "&album_id=" + albumId +
                     "&count=" + count +
                     "&extended=1" +
                     "&photo_sizes=1" +
                     "&access_token=" + accessToken +
                     "&v=" + API_VERSION;
        
        String response = sendGetRequest(url);
        JSONObject json = new JSONObject(response);
        
        if (json.has("error")) {
            throw new Exception("Ошибка API: " + json.getJSONObject("error").getString("error_msg"));
        }
        
        return json.getJSONObject("response").getJSONArray("items");
    }
    
    /**
     * Получает URL фотографии по ID через photos.getById
     */
    private String getPhotoUrlById(int ownerId, int photoId) throws Exception {
        // Для photos.getById нужно передать строку вида "-owner_id_photo_id" для групп
        String photosParam = ownerId + "_" + photoId;
        String url = "https://api.vk.com/method/photos.getById" +
                     "?photos=" + photosParam +
                     "&photo_sizes=1" +
                     "&access_token=" + accessToken +
                     "&v=" + API_VERSION;
        
        String response = sendGetRequest(url);
        JSONObject json = new JSONObject(response);
        
        if (json.has("error")) {
            return null;
        }
        
        JSONArray photos = json.getJSONArray("response");
        if (photos.length() == 0) {
            return null;
        }
        
        JSONObject photo = photos.getJSONObject(0);
        JSONArray sizes = photo.optJSONArray("sizes");
        if (sizes != null && sizes.length() > 0) {
            JSONObject maxSizePhoto = getMaxSizePhoto(sizes);
            if (maxSizePhoto != null) {
                return maxSizePhoto.optString("url");
            }
        }
        
        return null;
    }
    
    /**
     * Находит фотографию максимального размера
     * Приоритет размеров в VK API: w > z > y > x > m > s
     */
    private JSONObject getMaxSizePhoto(JSONArray sizes) {
        if (sizes == null || sizes.length() == 0) {
            return null;
        }
        
        // Приоритет размеров (больше число = выше приоритет)
        java.util.Map<String, Integer> sizePriority = new java.util.HashMap<>();
        sizePriority.put("w", 6);  // 2560px
        sizePriority.put("z", 5);  // 1280px
        sizePriority.put("y", 4);  // 807px
        sizePriority.put("x", 3);  // 604px
        sizePriority.put("m", 2);  // 130px
        sizePriority.put("s", 1);  // 75px
        
        JSONObject maxPhoto = null;
        int maxPriority = 0;
        int maxPixelSize = 0;
        
        for (int i = 0; i < sizes.length(); i++) {
            try {
                JSONObject photo = sizes.getJSONObject(i);
                
                // Проверяем, есть ли URL
                if (!photo.has("url") || photo.optString("url").isEmpty()) {
                    continue;
                }
                
                String type = photo.optString("type", "");
                int width = photo.optInt("width", 0);
                int height = photo.optInt("height", 0);
                int pixelSize = width * height;
                
                // Получаем приоритет по типу
                int priority = sizePriority.getOrDefault(type, 0);
                
                // Если есть width и height, используем их для сравнения
                if (width > 0 && height > 0) {
                    // Выбираем фото с максимальным количеством пикселей
                    if (pixelSize > maxPixelSize) {
                        maxPixelSize = pixelSize;
                        maxPhoto = photo;
                    }
                } else if (priority > maxPriority) {
                    // Если нет размеров, используем приоритет типа
                    maxPriority = priority;
                    maxPhoto = photo;
                } else if (priority == maxPriority && maxPhoto == null) {
                    // Если приоритет одинаковый и еще не выбрали фото
                    maxPhoto = photo;
                }
            } catch (Exception e) {
                // Пропускаем некорректные записи
                continue;
            }
        }
        
        // Если не нашли по пикселям или приоритету, берем последний элемент с URL
        if (maxPhoto == null) {
            for (int i = sizes.length() - 1; i >= 0; i--) {
                try {
                    JSONObject photo = sizes.getJSONObject(i);
                    if (photo.has("url") && !photo.optString("url").isEmpty()) {
                        maxPhoto = photo;
                        break;
                    }
                } catch (Exception e) {
                    continue;
                }
            }
        }
        
        return maxPhoto;
    }
    
    /**
     * Отправляет GET запрос
     */
    private String sendGetRequest(String urlString) throws Exception {
        URL url = new URL(urlString);
        HttpURLConnection conn = (HttpURLConnection) url.openConnection();
        conn.setRequestMethod("GET");
        
        int responseCode = conn.getResponseCode();
        if (responseCode != 200) {
            throw new Exception("HTTP ошибка: " + responseCode);
        }
        
        StringBuilder response = new StringBuilder();
        try (BufferedReader in = new BufferedReader(new InputStreamReader(conn.getInputStream()))) {
            String line;
            while ((line = in.readLine()) != null) {
                response.append(line);
            }
        }
        
        return response.toString();
    }
    
    /**
     * Скачивает файл по URL
     */
    private void downloadFile(String fileUrl, String filePath) throws Exception {
        URL url = new URL(fileUrl);
        HttpURLConnection conn = (HttpURLConnection) url.openConnection();
        
        try (InputStream in = conn.getInputStream();
             FileOutputStream out = new FileOutputStream(filePath)) {
            
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = in.read(buffer)) != -1) {
                out.write(buffer, 0, bytesRead);
            }
        }
    }
    
    public static void main(String[] args) {
        // ===== НАСТРОЙКИ =====
        // Получите токен здесь: https://vkhost.github.io/
        String ACCESS_TOKEN = "vk1.a.OyGcHZvbo3dklCENdz8fA_PUNhbnl9_bhaK6pUIQo7CsDFkCW9lIe3iOpMmQzMeff3bhqKy16xm8jkbTpiTHQUqXyxqm78mTwMi-alKYX1bOIppCWK0bpJBPilJp0I2wXGDgq9kSzFHhyGmvQWXrPtTl2kBNNeNj-K9mFAfYMmlMu_D_XbrIMzHtBX-AQfg4H8b9zOOD1X7h6fgHL3riqA";
        
        // ID группы (можно использовать короткое имя, например "durov" или числовой ID)
        String GROUP_ID = "sport_irz";  // Пример: группа Павла Дурова
        
        // Папка для сохранения
        String OUTPUT_FOLDER = "vk_photos";
        
        // Количество фотографий для скачивания
        int PHOTO_COUNT = 470;
        
        // ===== ВЫБОР АЛЬБОМОВ =====
        // Вариант 1: Указать конкретные ID альбомов (null = все альбомы)
        // Пример: int[] ALBUM_IDS = {123456, 789012};
        int[] ALBUM_IDS = {268951427
        };
        
        // Вариант 2: Указать названия альбомов (null = все альбомы)
        // Пример: String[] ALBUM_NAMES = {"Фото 2024", "Отдых"};
        String[] ALBUM_NAMES = null;
        
        // Если хотите сначала посмотреть список альбомов, установите true
        boolean SHOW_ALBUMS_LIST = false;
        
        // Если хотите проверить токен и права доступа, установите true
        boolean CHECK_TOKEN_ACCESS = false;
        // =====================
        
        if (ACCESS_TOKEN.equals("YOUR_ACCESS_TOKEN_HERE")) {
            System.out.println("⚠ ВНИМАНИЕ: Укажите ваш ACCESS_TOKEN в коде!");
            System.out.println("\nКак получить токен:");
            System.out.println("1. Перейдите на https://vkhost.github.io/");
            System.out.println("2. Выберите 'VK Admin' или нужные права");
            System.out.println("3. Скопируйте токен и вставьте в ACCESS_TOKEN");
        } else {
            VkPhotoDownloader downloader = new VkPhotoDownloader(ACCESS_TOKEN);
            
            // Проверка токена и прав доступа
            if (CHECK_TOKEN_ACCESS) {
                System.out.println("Запуск проверки токена...\n");
                downloader.checkTokenAccess(GROUP_ID);
                System.out.println("\nПроверка завершена. Нажмите Enter для выхода...");
                try {
                    System.in.read();
                } catch (Exception e) {
                    // Игнорируем
                }
                return;
            }
            
            // Показываем список альбомов, если нужно
            if (SHOW_ALBUMS_LIST) {
                downloader.listAlbums(GROUP_ID);
                // Если хотите только посмотреть список без скачивания, раскомментируйте следующую строку:
                // return;
            }
            
            downloader.downloadPhotosFromGroup(GROUP_ID, OUTPUT_FOLDER, PHOTO_COUNT, 
                                             ALBUM_IDS, ALBUM_NAMES);
        }
    }
}

