namespace HotelChain.Api.Contracts;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public string Country { get; set; } = "";
    public string PassportNumber { get; set; } = "";
    public string Role { get; set; } = "";
}