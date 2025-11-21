using System.IO;
using Microsoft.EntityFrameworkCore;
using FileStorage.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add PostgreSQL database
builder.Services.AddDbContext<FileStorageDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Enable CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Create storage directory if it doesn't exist
var storagePath = Path.Combine(builder.Environment.ContentRootPath, "Storage");
if (!Directory.Exists(storagePath))
{
    Directory.CreateDirectory(storagePath);
}

var app = builder.Build();

// Ensure database is created
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FileStorageDbContext>();
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("⚠️  ВНИМАНИЕ: Строка подключения к БД не настроена!");
            Console.WriteLine("   Настройте ConnectionStrings:DefaultConnection в appsettings.json");
        }
        else
        {
            Console.WriteLine("🔌 Попытка подключения к PostgreSQL...");
            Console.WriteLine($"   Host: {ExtractHost(connectionString)}");
            Console.WriteLine($"   Database: {ExtractDatabase(connectionString)}");
            
            // Проверяем доступность БД
            try
            {
                if (dbContext.Database.CanConnect())
                {
                    Console.WriteLine("✅ Подключение к БД успешно!");
                    dbContext.Database.EnsureCreated();
                    Console.WriteLine("✅ База данных готова к работе!");
                }
                else
                {
                    Console.WriteLine("❌ Не удалось подключиться к БД!");
                }
            }
            catch (Exception dbEx)
            {
                Console.WriteLine($"❌ Ошибка подключения: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"   Детали: {dbEx.InnerException.Message}");
                }
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("❌ ОШИБКА ПОДКЛЮЧЕНИЯ К БАЗЕ ДАННЫХ:");
    Console.WriteLine($"   {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("📋 Проверьте:");
    Console.WriteLine("   1. PostgreSQL запущен и работает");
    Console.WriteLine("   2. Строка подключения в appsettings.json правильная");
    Console.WriteLine("   3. База данных 'filestorage' создана (или будет создана автоматически)");
    Console.WriteLine("   4. Пользователь и пароль указаны верно");
    Console.WriteLine();
    Console.WriteLine("⚠️  Приложение продолжит работу, но функции БД могут не работать!");
    Console.WriteLine();
}

string ExtractHost(string connectionString)
{
    var parts = connectionString.Split(';');
    var hostPart = parts.FirstOrDefault(p => p.StartsWith("Host=", StringComparison.OrdinalIgnoreCase));
    return hostPart?.Substring(5) ?? "не указан";
}

string ExtractDatabase(string connectionString)
{
    var parts = connectionString.Split(';');
    var dbPart = parts.FirstOrDefault(p => p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
    return dbPart?.Substring(9) ?? "не указана";
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles(); // Serve static files (frontend)
app.UseAuthorization();
app.MapControllers();

// Serve index.html for root path
app.MapFallbackToFile("index.html");

app.Run();

