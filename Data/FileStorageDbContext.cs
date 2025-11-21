using Microsoft.EntityFrameworkCore;
using FileStorage.Models;

namespace FileStorage.Data;

public class FileStorageDbContext : DbContext
{
    public FileStorageDbContext(DbContextOptions<FileStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<StorageItem> StorageItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StorageItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Path);
            entity.HasIndex(e => e.ParentId);
            
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

