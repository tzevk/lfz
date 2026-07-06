using System.ComponentModel.DataAnnotations;

namespace AssetRequestApi.Models;

public class RequestItem
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? AllocatedToUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
