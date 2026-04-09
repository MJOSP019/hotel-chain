namespace HotelChain.Infrastructure.Auth;

public interface IJwtTokenService
{
    Task<string> CreateTokenAsync(ApplicationUser user);
}