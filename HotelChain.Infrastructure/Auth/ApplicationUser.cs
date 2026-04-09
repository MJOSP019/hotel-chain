using Microsoft.AspNetCore.Identity;

namespace HotelChain.Infrastructure.Auth;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public int Age { get; set; }
    public string Country { get; set; } = default!;
    public string PassportNumber { get; set; } = default!;
}