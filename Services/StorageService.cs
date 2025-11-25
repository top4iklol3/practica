using System.Text.RegularExpressions;
using FileStorage.Options;
using FileStorage.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FileStorage.Services;

public class StorageService : IStorageService
{
    private const long DefaultMaxUploadSize = 1_610_612_736; // ~1.5 GiB

    private readonly IWebHostEnvironment _environment;
    private readonly StorageOptions _storageOptions;
    private readonly FileIconOptions _iconOptions;
    private readonly ILogger<StorageService> _logger;
    private readonly string _absoluteBasePath;

    public StorageService(
        IWebHostEnvironment environment,
        IOptions<StorageOptions> storageOptions,
        IOptions<FileIconOptions> iconOptions,
        ILogger<StorageService> logger)
    {
        _environment = environment;
        _storageOptions = storageOptions.Value;
        _iconOptions = iconOptions.Value;
        _logger = logger;
        _absoluteBasePath = ResolveBasePath();
    }

    public Task<StorageListResponse> ListAsync(string resourceKey, string? path, CancellationToken cancellationToken)
    {
        var (resourceRoot, sanitizedResourceKey) = GetResourceRoot(resourceKey);
        var relativePath = NormalizePath(path);
        var absolutePath = CombineAbsolute(resourceRoot, relativePath);

        if (!Directory.Exists(absolutePath))
        {
            throw new DirectoryNotFoundException("Папка не найдена.");
        }

        var directory = new DirectoryInfo(absolutePath);

        var dirs = directory.EnumerateDirectories()
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => ToFolderResponse(relativePath, sanitizedResourceKey, d))
            .ToList();

        var files = directory.EnumerateFiles()
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => ToFileResponse(relativePath, sanitizedResourceKey, f))
            .ToList();

        var combined = dirs.Concat(files).ToList();
        return Task.FromResult(new StorageListResponse(relativePath, combined));
    }

    public async Task<UploadResponse> UploadAsync(string resourceKey, string? path, IFormFileCollection files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            throw new InvalidOperationException("Файлы для загрузки не предоставлены.");
        }

        var (resourceRoot, sanitizedResourceKey) = GetResourceRoot(resourceKey);
        var relativePath = NormalizePath(path);
        var absoluteDirectory = CombineAbsolute(resourceRoot, relativePath);
        Directory.CreateDirectory(absoluteDirectory);

        var responses = new List<StorageItemResponse>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length == 0)
            {
                continue;
            }

            if (file.Length > DefaultMaxUploadSize)
            {
                throw new InvalidOperationException($"Файл {file.FileName} превышает максимально допустимый размер 1.5 ГБ.");
            }

            var safeName = SanitizeName(file.FileName);
            var uniqueName = EnsureUniqueName(absoluteDirectory, safeName);
            var destinationPath = Path.Combine(absoluteDirectory, uniqueName);

            await using (var destinationStream = new FileStream(
                             destinationPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await file.CopyToAsync(destinationStream, cancellationToken);
            }

            responses.Add(ToFileResponse(relativePath, sanitizedResourceKey, new FileInfo(destinationPath)));
        }

        return new UploadResponse("Файлы успешно загружены", responses);
    }

    public Task<FileDownloadResult?> DownloadAsync(string resourceKey, string path, CancellationToken cancellationToken)
    {
        var (resourceRoot, _) = GetResourceRoot(resourceKey);
        var normalizedPath = NormalizePath(path, required: true);
        var absolutePath = CombineAbsolute(resourceRoot, normalizedPath);

        if (!System.IO.File.Exists(absolutePath))
        {
            return Task.FromResult<FileDownloadResult?>(null);
        }

        var fileInfo = new FileInfo(absolutePath);
        var stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var contentType = GetContentType(fileInfo.Extension) ?? "application/octet-stream";
        return Task.FromResult<FileDownloadResult?>(new FileDownloadResult(stream, contentType, fileInfo.Name));
    }

    public Task<CreateFolderResponse> CreateFolderAsync(string resourceKey, string? path, string folderName, CancellationToken cancellationToken)
    {
        var (resourceRoot, sanitizedResourceKey) = GetResourceRoot(resourceKey);
        var relativePath = NormalizePath(path);
        var absoluteDirectory = CombineAbsolute(resourceRoot, relativePath);
        Directory.CreateDirectory(absoluteDirectory);

        var safeFolderName = SanitizeName(folderName);
        if (string.IsNullOrWhiteSpace(safeFolderName))
        {
            safeFolderName = "New Folder";
        }

        var uniqueFolderName = EnsureUniqueName(absoluteDirectory, safeFolderName);
        var newDirectoryPath = Path.Combine(absoluteDirectory, uniqueFolderName);
        Directory.CreateDirectory(newDirectoryPath);

        var response = ToFolderResponse(relativePath, sanitizedResourceKey, new DirectoryInfo(newDirectoryPath));
        return Task.FromResult(new CreateFolderResponse("Папка успешно создана", response));
    }

    public async Task<CreateUrlResponse> CreateUrlAsync(string resourceKey, string? path, string urlName, string url, CancellationToken cancellationToken)
    {
        var (resourceRoot, sanitizedResourceKey) = GetResourceRoot(resourceKey);
        var relativePath = NormalizePath(path);
        var absoluteDirectory = CombineAbsolute(resourceRoot, relativePath);
        Directory.CreateDirectory(absoluteDirectory);

        var safeName = SanitizeName(urlName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "New URL";
        }

        if (!safeName.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".url";
        }

        var uniqueName = EnsureUniqueName(absoluteDirectory, safeName);
        var urlFilePath = Path.Combine(absoluteDirectory, uniqueName);

        var urlContent = $"[InternetShortcut]\r\nURL={url}\r\n";
        await System.IO.File.WriteAllTextAsync(urlFilePath, urlContent, cancellationToken);

        var response = ToFileResponse(relativePath, sanitizedResourceKey, new FileInfo(urlFilePath));
        return new CreateUrlResponse("URL успешно создан", response);
    }

    public Task DeleteAsync(string resourceKey, string path, CancellationToken cancellationToken)
    {
        var (resourceRoot, _) = GetResourceRoot(resourceKey);
        var normalizedPath = NormalizePath(path, required: true);
        var absolutePath = CombineAbsolute(resourceRoot, normalizedPath);

        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
            return Task.CompletedTask;
        }

        if (Directory.Exists(absolutePath))
        {
            Directory.Delete(absolutePath, recursive: true);
            return Task.CompletedTask;
        }

        throw new FileNotFoundException("Элемент не найден.");
    }

    private StorageItemResponse ToFolderResponse(string currentPath, string resourceFolder, DirectoryInfo dir)
    {
        var relative = CombineRelative(currentPath, dir.Name);
        return new StorageItemResponse(
            Type: 0,
            Filename: dir.Name,
            FilenameWithoutExtension: dir.Name,
            Path: relative,
            Icon: string.IsNullOrWhiteSpace(_iconOptions.Folder) ? "📁" : _iconOptions.Folder);
    }

    private StorageItemResponse ToFileResponse(string currentPath, string resourceFolder, FileInfo file)
    {
        var relative = CombineRelative(currentPath, file.Name);
        var extension = file.Extension.ToLowerInvariant();
        var isUrl = extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
        var typeCode = isUrl ? 2 : 1;
        var icon = ResolveIcon(extension, isUrl);

        return new StorageItemResponse(
            Type: typeCode,
            Filename: file.Name,
            FilenameWithoutExtension: Path.GetFileNameWithoutExtension(file.Name),
            Path: relative,
            Icon: icon);
    }

    private string ResolveIcon(string extension, bool isUrl)
    {
        if (isUrl)
        {
            return string.IsNullOrWhiteSpace(_iconOptions.Url) ? "🔗" : _iconOptions.Url;
        }

        if (!string.IsNullOrWhiteSpace(extension) &&
            _iconOptions.Extensions.TryGetValue(extension, out var icon))
        {
            return icon;
        }

        return string.IsNullOrWhiteSpace(_iconOptions.Default) ? "📄" : _iconOptions.Default;
    }

    private (string Root, string SanitizedKey) GetResourceRoot(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            throw new ArgumentException("Ключ ресурса не может быть пустым.", nameof(resourceKey));
        }

        var sanitized = SanitizeResourceKey(resourceKey);
        var root = Path.Combine(_absoluteBasePath, sanitized);
        Directory.CreateDirectory(root);
        return (root, sanitized);
    }

    private string NormalizePath(string? path, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (required)
            {
                throw new ArgumentException("Путь не может быть пустым.");
            }

            return string.Empty;
        }

        var sanitized = path.Replace("\\", "/").Trim();
        sanitized = sanitized.Trim('/');

        if (sanitized.Contains("..", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Недопустимый путь.");
        }

        return sanitized;
    }

    private string CombineAbsolute(string root, string relative)
    {
        return string.IsNullOrWhiteSpace(relative)
            ? root
            : Path.Combine(root, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    private string CombineRelative(string current, string child)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return child;
        }

        return $"{current.TrimEnd('/')}/{child}";
    }

    private string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }

    private string SanitizeResourceKey(string key)
    {
        var sanitized = Regex.Replace(key, @"[^a-zA-Z0-9-_]", "_");
        return string.IsNullOrWhiteSpace(sanitized) ? $"resource_{Guid.NewGuid():N}" : sanitized;
    }

    private string EnsureUniqueName(string directory, string desiredName)
    {
        var candidate = desiredName;
        var counter = 1;
        var baseName = Path.GetFileNameWithoutExtension(desiredName);
        var extension = Path.GetExtension(desiredName);

        while (System.IO.File.Exists(Path.Combine(directory, candidate)) ||
               Directory.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{baseName} ({counter}){extension}";
            counter++;
        }

        return candidate;
    }

    private string ResolveBasePath()
    {
        var basePath = string.IsNullOrWhiteSpace(_storageOptions.BasePath)
            ? "Storage"
            : _storageOptions.BasePath;

        if (!Path.IsPathRooted(basePath))
        {
            basePath = Path.Combine(_environment.ContentRootPath, basePath);
        }

        Directory.CreateDirectory(basePath);
        return basePath;
    }

    private static string? GetContentType(string extension)
    {
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".csv" => "text/csv",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            _ => null
        };
    }
}

