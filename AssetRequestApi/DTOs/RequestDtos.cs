using System.ComponentModel.DataAnnotations;

namespace AssetRequestApi.DTOs;

public class CreateRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }
}

public class AllocateRequestDto
{
    [Required]
    public string AllocatedToUserId { get; set; } = string.Empty;
}
