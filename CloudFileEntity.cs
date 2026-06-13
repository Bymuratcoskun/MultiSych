using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MultiSych.Services.Data.Entities;

[Index(nameof(AccountId), nameof(Path), IsUnique = true)]
[Index(nameof(AccountId), nameof(ParentId))]
public class CloudFileEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    public string FileId { get; set; } = string.Empty; // Provider's unique ID for the file

    public string? ParentId { get; set; } // Provider's unique ID for the parent folder

    [Required]
    public string Path { get; set; } = string.Empty; // Full path, e.g., "/Documents/report.pdf"

    [Required]
    public string FileName { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
