using Microsoft.AspNetCore.Identity;

namespace Glasstrut.Api.Models;

public class User : IdentityUser
{
    public string? DisplayName { get; set; }
}
