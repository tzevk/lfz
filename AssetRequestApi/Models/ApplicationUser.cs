using Microsoft.AspNetCore.Identity;

namespace AssetRequestApi.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public string RoleFlag { get; set; } = string.Empty;
}
