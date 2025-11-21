using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileStorage.Data;
using FileStorage.Models;
using System.IO;

namespace FileStorage.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileStorageController : ControllerBase
{
    private readonly FileStorageDbContext _context;
    private readonly string _storagePath;
    private readonly IWebHostEnvironment _environment;

    public FileStorageController(FileStorageDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
        _storagePath = Path.Combine(_environment.ContentRootPath, "Storage");
        
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    // Получить список файлов и папок в текущей директории
    [HttpGet("list")]
    public async Task<IActionResult> GetFiles([FromQuery] string? path = "")
    {
        try
        {
            var currentPath = NormalizePath(path ?? "");
            
            // Находим родительскую папку по пути
            StorageItem? parentFolder = null;
            if (!string.IsNullOrEmpty(currentPath))
            {
                parentFolder = await _context.StorageItems
                    .FirstOrDefaultAsync(x => x.Path == currentPath && x.Type == "folder");
                
                if (parentFolder == null)
                {
                    return NotFound(new { error = "Папка не найдена" });
                }
            }

            // Получаем все элементы в текущей папке
            var items = await _context.StorageItems
                .Where(x => x.ParentId == (parentFolder != null ? parentFolder.Id : (int?)null))
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .ToListAsync();

            var result = items.Select(item => new
            {
                id = item.Id,
                name = item.Name,
                type = item.Type,
                path = item.Path,
                size = item.Size,
                created = item.CreatedAt,
                modified = item.UpdatedAt
            }).ToList();

            return Ok(new
            {
                currentPath = currentPath,
                items = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Загрузить файл
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromQuery] string? path = "")
    {
        try
        {
            if (Request.Form.Files.Count == 0)
            {
                return BadRequest(new { error = "Файл не был загружен" });
            }

            var currentPath = NormalizePath(path ?? "");
            
            // Находим или создаем родительскую папку
            StorageItem? parentFolder = null;
            if (!string.IsNullOrEmpty(currentPath))
            {
                parentFolder = await _context.StorageItems
                    .FirstOrDefaultAsync(x => x.Path == currentPath && x.Type == "folder");
                
                if (parentFolder == null)
                {
                    return NotFound(new { error = "Папка не найдена" });
                }
            }

            var uploadedFiles = new List<object>();

            foreach (var file in Request.Form.Files)
            {
                if (file.Length > 0)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    
                    // Проверяем, существует ли файл с таким именем
                    var existingFile = await _context.StorageItems
                        .FirstOrDefaultAsync(x => 
                            x.ParentId == (parentFolder != null ? parentFolder.Id : (int?)null) &&
                            x.Name == fileName &&
                            x.Type == "file");

                    // Если файл существует, добавляем номер
                    var counter = 1;
                    var originalFileName = fileName;
                    while (existingFile != null)
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                        var ext = Path.GetExtension(originalFileName);
                        fileName = $"{nameWithoutExt} ({counter}){ext}";
                        existingFile = await _context.StorageItems
                            .FirstOrDefaultAsync(x => 
                                x.ParentId == (parentFolder != null ? parentFolder.Id : (int?)null) &&
                                x.Name == fileName &&
                                x.Type == "file");
                        counter++;
                    }

                    // Сохраняем файл на диск
                    var physicalPath = GetPhysicalPath(parentFolder, fileName);
                    var directory = Path.GetDirectoryName(physicalPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (var stream = new FileStream(physicalPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Сохраняем метаданные в БД
                    var filePath = string.IsNullOrEmpty(currentPath) 
                        ? fileName 
                        : $"{currentPath}/{fileName}";
                    
                    var storageItem = new StorageItem
                    {
                        Name = fileName,
                        Type = "file",
                        Path = filePath,
                        ParentId = parentFolder?.Id,
                        PhysicalPath = physicalPath,
                        Size = file.Length,
                        ContentType = GetContentType(fileName),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.StorageItems.Add(storageItem);
                    await _context.SaveChangesAsync();

                    uploadedFiles.Add(new
                    {
                        id = storageItem.Id,
                        name = fileName,
                        path = filePath,
                        size = file.Length
                    });
                }
            }

            return Ok(new { message = "Файл(ы) успешно загружены", files = uploadedFiles });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Скачать файл
    [HttpGet("download")]
    public async Task<IActionResult> DownloadFile([FromQuery] string path)
    {
        try
        {
            var normalizedPath = NormalizePath(path);
            
            var storageItem = await _context.StorageItems
                .FirstOrDefaultAsync(x => x.Path == normalizedPath && x.Type == "file");

            if (storageItem == null || !System.IO.File.Exists(storageItem.PhysicalPath))
            {
                return NotFound(new { error = "Файл не найден" });
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(storageItem.PhysicalPath);
            var contentType = storageItem.ContentType ?? GetContentType(storageItem.Name);

            return File(fileBytes, contentType, storageItem.Name);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Создать папку
    [HttpPost("folder")]
    public async Task<IActionResult> CreateFolder([FromQuery] string? path = "", [FromBody] CreateFolderRequest? request = null)
    {
        try
        {
            var folderName = request?.Name ?? "Новая папка";
            
            // Удаляем недопустимые символы из имени папки
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                folderName = folderName.Replace(c, '_');
            }

            var currentPath = NormalizePath(path ?? "");
            
            // Находим родительскую папку
            StorageItem? parentFolder = null;
            if (!string.IsNullOrEmpty(currentPath))
            {
                parentFolder = await _context.StorageItems
                    .FirstOrDefaultAsync(x => x.Path == currentPath && x.Type == "folder");
                
                if (parentFolder == null)
                {
                    return NotFound(new { error = "Родительская папка не найдена" });
                }
            }

            // Проверяем, существует ли папка с таким именем
            var existingFolder = await _context.StorageItems
                .FirstOrDefaultAsync(x => 
                    x.ParentId == (parentFolder != null ? parentFolder.Id : (int?)null) &&
                    x.Name == folderName &&
                    x.Type == "folder");

            // Если папка существует, добавляем номер
            var counter = 1;
            var originalFolderName = folderName;
            while (existingFolder != null)
            {
                folderName = $"{originalFolderName} ({counter})";
                existingFolder = await _context.StorageItems
                    .FirstOrDefaultAsync(x => 
                        x.ParentId == (parentFolder != null ? parentFolder.Id : (int?)null) &&
                        x.Name == folderName &&
                        x.Type == "folder");
                counter++;
            }

            // Создаем папку на диске
            var physicalPath = GetPhysicalPath(parentFolder, folderName);
            if (!Directory.Exists(physicalPath))
            {
                Directory.CreateDirectory(physicalPath);
            }

            // Сохраняем метаданные в БД
            var folderPath = string.IsNullOrEmpty(currentPath) 
                ? folderName 
                : $"{currentPath}/{folderName}";

            var storageItem = new StorageItem
            {
                Name = folderName,
                Type = "folder",
                Path = folderPath,
                ParentId = parentFolder?.Id,
                PhysicalPath = physicalPath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StorageItems.Add(storageItem);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Папка успешно создана",
                id = storageItem.Id,
                name = folderName,
                path = folderPath
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Удалить файл или папку
    [HttpDelete("item")]
    public async Task<IActionResult> DeleteItem([FromQuery] string path)
    {
        try
        {
            var normalizedPath = NormalizePath(path);
            
            var storageItem = await _context.StorageItems
                .Include(x => x.Children)
                .FirstOrDefaultAsync(x => x.Path == normalizedPath);

            if (storageItem == null)
            {
                return NotFound(new { error = "Элемент не найден" });
            }

            // Рекурсивно удаляем все дочерние элементы
            await DeleteItemRecursive(storageItem);

            // Удаляем физический файл или папку
            if (storageItem.Type == "file" && System.IO.File.Exists(storageItem.PhysicalPath))
            {
                System.IO.File.Delete(storageItem.PhysicalPath);
            }
            else if (storageItem.Type == "folder" && Directory.Exists(storageItem.PhysicalPath))
            {
                Directory.Delete(storageItem.PhysicalPath, true);
            }

            // Удаляем из БД
            _context.StorageItems.Remove(storageItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Элемент успешно удален" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Рекурсивное удаление дочерних элементов
    private async Task DeleteItemRecursive(StorageItem item)
    {
        var children = await _context.StorageItems
            .Where(x => x.ParentId == item.Id)
            .ToListAsync();

        foreach (var child in children)
        {
            await DeleteItemRecursive(child);
            
            // Удаляем физический файл или папку
            if (child.Type == "file" && System.IO.File.Exists(child.PhysicalPath))
            {
                System.IO.File.Delete(child.PhysicalPath);
            }
            else if (child.Type == "folder" && Directory.Exists(child.PhysicalPath))
            {
                Directory.Delete(child.PhysicalPath, true);
            }
            
            _context.StorageItems.Remove(child);
        }
    }

    // Получить физический путь к файлу/папке
    private async Task<string> GetPhysicalPathAsync(StorageItem? parent, string name)
    {
        if (parent == null)
        {
            return Path.Combine(_storagePath, name);
        }

        var pathParts = new List<string> { _storagePath };
        var current = parent;
        var parents = new List<StorageItem>();

        // Собираем всех родителей
        while (current != null)
        {
            parents.Insert(0, current);
            if (current.ParentId.HasValue)
            {
                current = await _context.StorageItems.FindAsync(current.ParentId.Value);
            }
            else
            {
                current = null;
            }
        }

        // Строим путь
        foreach (var p in parents)
        {
            pathParts.Add(p.Name);
        }
        pathParts.Add(name);

        return Path.Combine(pathParts.ToArray());
    }
    
    // Синхронная версия для случаев, когда родитель уже загружен
    private string GetPhysicalPath(StorageItem? parent, string name)
    {
        if (parent == null)
        {
            return Path.Combine(_storagePath, name);
        }

        // Используем PhysicalPath родителя, если он есть
        if (!string.IsNullOrEmpty(parent.PhysicalPath))
        {
            return Path.Combine(parent.PhysicalPath, name);
        }

        // Иначе строим путь по имени
        var pathParts = new List<string> { _storagePath };
        if (parent != null)
        {
            // Простое построение пути на основе имени родителя
            pathParts.Add(parent.Name);
        }
        pathParts.Add(name);

        return Path.Combine(pathParts.ToArray());
    }

    // Нормализация пути
    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        return path.TrimStart('/', '\\').Replace("\\", "/");
    }

    // Определение типа контента
    private string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }
}

public class CreateFolderRequest
{
    public string Name { get; set; } = "";
}
