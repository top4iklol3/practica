"""
Скрипт для скачивания фотографий из группы ВКонтакте
Требуется: pip install vk_api requests
"""

import vk_api
import requests
import os
import re
from pathlib import Path

def list_albums(group_id, access_token):
    """
    Выводит список всех альбомов группы
    
    Args:
        group_id: ID группы (можно использовать короткое имя или числовой ID)
        access_token: Токен доступа VK API
    
    Returns:
        Список альбомов с их ID и названиями
    """
    vk_session = vk_api.VkApi(token=access_token)
    vk = vk_session.get_api()
    
    try:
        group_info = vk.groups.getById(group_id=group_id)[0]
        group_name = group_info['name']
        albums = vk.photos.getAlbums(owner_id=-int(group_info['id']))
        
        print(f"\n=== Альбомы группы '{group_name}' ===\n")
        album_list = []
        for album in albums['items']:
            album_id = album['id']
            album_title = album['title']
            photos_count = album.get('size', 0)
            album_list.append({'id': album_id, 'title': album_title, 'size': photos_count})
            print(f"ID: {album_id:6} | Название: {album_title:50} | Фото: {photos_count}")
        
        print()
        return album_list
    except Exception as e:
        print(f"Ошибка при получении списка альбомов: {e}")
        return []


def download_photos_from_group(group_id, access_token, output_folder="photos", count=100, album_ids=None, album_names=None):
    """
    Скачивает фотографии из группы ВКонтакте
    
    Args:
        group_id: ID группы (можно использовать короткое имя или числовой ID)
        access_token: Токен доступа VK API
        output_folder: Папка для сохранения фотографий
        count: Количество фотографий для скачивания (максимум 200 за запрос)
        album_ids: Список ID альбомов для скачивания (None = все альбомы)
        album_names: Список названий альбомов для скачивания (None = все альбомы)
    """
    # Инициализация VK API
    vk_session = vk_api.VkApi(token=access_token)
    vk = vk_session.get_api()
    
    # Создаем папку для сохранения
    Path(output_folder).mkdir(exist_ok=True)
    
    print(f"Начинаю скачивание фотографий из группы {group_id}...")
    
    # Получаем фотографии из альбомов группы
    try:
        # Получаем информацию о группе
        group_info = vk.groups.getById(group_id=group_id)[0]
        group_name = group_info['name']
        print(f"Группа: {group_name}")
        
        # Получаем альбомы группы
        albums = vk.photos.getAlbums(owner_id=-int(group_info['id']))
        
        # Фильтруем альбомы, если указаны конкретные
        albums_to_process = albums['items']
        if album_ids or album_names:
            filtered_albums = []
            for album in albums['items']:
                album_id = album['id']
                album_title = album['title']
                
                # Проверяем по ID
                if album_ids and album_id in album_ids:
                    filtered_albums.append(album)
                # Проверяем по названию
                elif album_names and album_title in album_names:
                    filtered_albums.append(album)
                # Если указаны фильтры, но альбом не подходит - пропускаем
                elif album_ids or album_names:
                    continue
                else:
                    filtered_albums.append(album)
            
            albums_to_process = filtered_albums
            
            if not albums_to_process:
                print("⚠ Не найдено альбомов по указанным критериям!")
                print("Используйте функцию list_albums() чтобы увидеть все доступные альбомы")
                return
        
        total_downloaded = 0
        
        # Проходим по каждому альбому
        for album in albums_to_process:
            album_id = album['id']
            album_title = album['title']
            print(f"\nОбрабатываю альбом: {album_title}")
            
            # Создаем папку для альбома (очищаем название от недопустимых символов)
            safe_album_name = re.sub(r'[<>:"/\\|?*]', '_', album_title)
            album_folder = os.path.join(output_folder, f"{album_id}_{safe_album_name}")
            Path(album_folder).mkdir(parents=True, exist_ok=True)
            print(f"Создана папка: {album_folder}")
            
            # Получаем фотографии из альбома
            photos = vk.photos.get(
                owner_id=-int(group_info['id']),
                album_id=album_id,
                count=count,
                extended=1
            )
            
            # Скачиваем каждую фотографию
            for photo in photos['items']:
                # Берем фото максимального размера
                sizes = photo['sizes']
                max_size_photo = max(sizes, key=lambda x: x['width'] * x['height'])
                photo_url = max_size_photo['url']
                
                # Формируем имя файла
                photo_id = photo['id']
                file_name = f"{photo_id}.jpg"
                file_path = os.path.join(album_folder, file_name)
                
                # Скачиваем фото
                try:
                    response = requests.get(photo_url, stream=True)
                    response.raise_for_status()
                    
                    with open(file_path, 'wb') as f:
                        for chunk in response.iter_content(chunk_size=8192):
                            f.write(chunk)
                    
                    total_downloaded += 1
                    print(f"  ✓ Скачано: {file_name} ({total_downloaded})")
                    
                except Exception as e:
                    print(f"  ✗ Ошибка при скачивании {photo_id}: {e}")
        
        print(f"\n✓ Готово! Скачано фотографий: {total_downloaded}")
        print(f"Фотографии сохранены в папке: {os.path.abspath(output_folder)}")
        
    except Exception as e:
        print(f"Ошибка: {e}")
        print("\nВозможные причины:")
        print("1. Неверный токен доступа")
        print("2. Неверный ID группы")
        print("3. Нет доступа к группе")


if __name__ == "__main__":
    # ===== НАСТРОЙКИ =====
    # Получите токен здесь: https://vkhost.github.io/
    # Или создайте приложение: https://vk.com/apps?act=manage
    ACCESS_TOKEN = "YOUR_ACCESS_TOKEN_HERE"
    
    # ID группы (можно использовать короткое имя, например "durov" или числовой ID)
    GROUP_ID = "durov"  # Пример: группа Павла Дурова
    
    # Папка для сохранения
    OUTPUT_FOLDER = "vk_photos"
    
    # Количество фотографий для скачивания
    PHOTO_COUNT = 100
    
    # ===== ВЫБОР АЛЬБОМОВ =====
    # Вариант 1: Указать конкретные ID альбомов (None = все альбомы)
    # Пример: ALBUM_IDS = [123456, 789012]
    ALBUM_IDS = None
    
    # Вариант 2: Указать названия альбомов (None = все альбомы)
    # Пример: ALBUM_NAMES = ["Фото 2024", "Отдых"]
    ALBUM_NAMES = None
    
    # Если хотите сначала посмотреть список альбомов, установите True
    SHOW_ALBUMS_LIST = False
    # =====================
    
    if ACCESS_TOKEN == "YOUR_ACCESS_TOKEN_HERE":
        print("⚠ ВНИМАНИЕ: Укажите ваш ACCESS_TOKEN в скрипте!")
        print("\nКак получить токен:")
        print("1. Перейдите на https://vkhost.github.io/")
        print("2. Выберите 'VK Admin' или нужные права")
        print("3. Скопируйте токен и вставьте в ACCESS_TOKEN")
    else:
        # Показываем список альбомов, если нужно
        if SHOW_ALBUMS_LIST:
            list_albums(GROUP_ID, ACCESS_TOKEN)
        
        download_photos_from_group(
            group_id=GROUP_ID,
            access_token=ACCESS_TOKEN,
            output_folder=OUTPUT_FOLDER,
            count=PHOTO_COUNT,
            album_ids=ALBUM_IDS,
            album_names=ALBUM_NAMES
        )

