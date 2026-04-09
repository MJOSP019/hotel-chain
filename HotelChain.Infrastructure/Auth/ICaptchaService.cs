namespace HotelChain.Infrastructure.Auth;

public interface ICaptchaService
{
    (string captchaId, string question) Create();
    bool Validate(string captchaId, string answer);
}