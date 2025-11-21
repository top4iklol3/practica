using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileStorage.Models;

[Table("storage_items")]
public class StorageItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("type")]
    public string Type { get; set; } = string.Empty; // "file" or "folder"

    [MaxLength(1000)]
    [Column("path")]
    public string Path { get; set; } = string.Empty; // Виртуальный путь (например, "folder1/subfolder")

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [ForeignKey("ParentId")]
    public StorageItem? Parent { get; set; }

    [InverseProperty("Parent")]
    public ICollection<StorageItem> Children { get; set; } = new List<StorageItem>();

    [MaxLength(500)]
    [Column("physical_path")]
    public string PhysicalPath { get; set; } = string.Empty; // Физический путь к файлу на диске

    [Column("size")]
    public long? Size { get; set; } // Размер файла в байтах (null для папок)

    [MaxLength(100)]
    [Column("content_type")]
    public string? ContentType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

