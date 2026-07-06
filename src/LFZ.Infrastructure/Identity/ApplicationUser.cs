using Microsoft.AspNetCore.Identity;

namespace LFZ.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public string RoleFlag { get; set; } = string.Empty;
}
