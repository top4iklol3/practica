using FileStorage.Services;
using FileStorage.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace FileStorage.Controllers;

/// <summary>
/// API для работы с галереей фотографий из хранилища.
/// Фотографии организованы по годам в папках: gallery/{year}/
/// </summary>
[ApiController]
[Route("api/gallery")]
public class GalleryController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly ILogger<GalleryController> _logger;
    private const string GalleryFolder = "gallery";

    // Поддерживаемые форматы изображений
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg"
    };

    // Поддерживаемые форматы документов
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    // Проверяет, является ли файл изображением или документом
    private static bool IsMediaFile(string filename)
    {
        var extension = Path.GetExtension(filename);
        return ImageExtensions.Contains(extension) || DocumentExtensions.Contains(extension);
    }

    public GalleryController(IStorageService storageService, ILogger<GalleryController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Возвращает список всех годов, для которых есть фотографии в галерее.
    /// </summary>
    [HttpGet("years")]
    public async Task<IActionResult> GetYears(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _storageService.ListAsync(GalleryFolder, cancellationToken);
            
            // Фильтруем только папки (года) и сортируем по убыванию
            var years = result.Items
                .Where(item => item.Type == 0 && int.TryParse(item.Filename, out _))
                .Select(item => int.Parse(item.Filename))
                .OrderByDescending(year => year)
                .ToList();

            return Ok(new { years });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка годов");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Возвращает список фотографий для указанного года.
    /// Если год не указан, возвращает все фотографии из всех годов.
    /// </summary>
    [HttpGet("photos")]
    public async Task<IActionResult> GetPhotos([FromQuery] int? year, CancellationToken cancellationToken)
    {
        try
        {
            if (year.HasValue)
            {
                // Получаем фотографии и PDF для конкретного года
                var result = await _storageService.ListAsync($"{GalleryFolder}/{year.Value}", cancellationToken);
                var photos = result.Items
                    .Where(item => item.Type == 1 && IsMediaFile(item.Filename))
                    .Select(item => new
                    {
                        item.Filename,
                        item.Path,
                        item.FilenameWithoutExtension,
                        Year = year.Value,
                        Url = $"/storage/download?path={Uri.EscapeDataString(string.IsNullOrEmpty(result.CurrentPath) ? $"{GalleryFolder}/{year.Value}/{item.Filename}" : $"{GalleryFolder}/{result.CurrentPath}/{item.Filename}")}",
                        Type = IsImageFile(item.Filename) ? "image" : "pdf"
                    })
                    .ToList();

                return Ok(new { photos, year = year.Value });
            }
            else
            {
                // Получаем все фотографии из всех годов
                var rootResult = await _storageService.ListAsync(GalleryFolder, cancellationToken);
                var allPhotos = new List<object>();

                // Проходим по всем папкам-годам
                foreach (var yearFolder in rootResult.Items.Where(item => item.Type == 0 && int.TryParse(item.Filename, out _)))
                {
                    var folderYear = int.Parse(yearFolder.Filename);
                    var yearResult = await _storageService.ListAsync($"{GalleryFolder}/{yearFolder.Path}", cancellationToken);
                    
                    var photos = yearResult.Items
                        .Where(item => item.Type == 1 && IsMediaFile(item.Filename))
                        .Select(item => new
                        {
                            item.Filename,
                            item.Path,
                            item.FilenameWithoutExtension,
                            Year = folderYear,
                            Url = $"/storage/download?path={Uri.EscapeDataString(string.IsNullOrEmpty(yearResult.CurrentPath) ? $"{GalleryFolder}/{folderYear}/{item.Filename}" : $"{GalleryFolder}/{yearResult.CurrentPath}/{item.Filename}")}",
                            Type = IsImageFile(item.Filename) ? "image" : "pdf"
                        });

                    allPhotos.AddRange(photos);
                }

                return Ok(new { photos = allPhotos });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении фотографий");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Проверяет наличие фотографий для указанного года.
    /// Возвращает true, если есть хотя бы одна фотография.
    /// </summary>
    [HttpGet("has-photos")]
    public async Task<IActionResult> HasPhotos([FromQuery] int year, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _storageService.ListAsync($"{GalleryFolder}/{year}", cancellationToken);
            var hasPhotos = result.Items.Any(item => item.Type == 1 && IsMediaFile(item.Filename));
            return Ok(new { hasPhotos, year });
        }
        catch (DirectoryNotFoundException)
        {
            // Папка не существует - фотографий нет
            return Ok(new { hasPhotos = false, year });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке наличия фотографий");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Возвращает список годов, для которых есть фотографии.
    /// </summary>
    [HttpGet("years-with-photos")]
    public async Task<IActionResult> GetYearsWithPhotos(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _storageService.ListAsync(GalleryFolder, cancellationToken);
            var yearsWithPhotos = new List<int>();

            // Проходим по всем папкам-годам и проверяем наличие фотографий
            foreach (var yearFolder in result.Items.Where(item => item.Type == 0 && int.TryParse(item.Filename, out _)))
            {
                var year = int.Parse(yearFolder.Filename);
                try
                {
                    var yearResult = await _storageService.ListAsync($"{GalleryFolder}/{yearFolder.Path}", cancellationToken);
                    var hasPhotos = yearResult.Items.Any(item => item.Type == 1 && IsMediaFile(item.Filename));
                    if (hasPhotos)
                    {
                        yearsWithPhotos.Add(year);
                    }
                }
                catch
                {
                    // Игнорируем ошибки для отдельных папок
                }
            }

            return Ok(new { years = yearsWithPhotos.OrderByDescending(y => y).ToList() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении годов с фотографиями");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static bool IsImageFile(string filename)
    {
        var extension = Path.GetExtension(filename);
        return ImageExtensions.Contains(extension);
    }
}

