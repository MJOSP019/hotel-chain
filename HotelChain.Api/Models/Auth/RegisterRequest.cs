namespace HotelChain.Api.Models.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    int Age,
    string Country,
    string PassportNumber,
    string CaptchaId,
    string CaptchaAnswer
);