using Microsoft.AspNetCore.Identity;

namespace DotArchitect.Modules.Identity.Domain;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
