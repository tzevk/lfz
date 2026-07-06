using System.ComponentModel.DataAnnotations;

namespace LFZ.Domain.Entities;

/// <summary>Key/value application settings editable from the Admin UI (feature flags, palette).</summary>
public class AppSetting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ValueType { get; set; } = "String";

    [MaxLength(500)]
    public string? Description { get; set; }
}
