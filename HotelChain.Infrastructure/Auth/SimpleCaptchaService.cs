using Microsoft.Extensions.Caching.Memory;

namespace HotelChain.Infrastructure.Auth;

public class SimpleCaptchaService : ICaptchaService
{
    private readonly IMemoryCache _cache;
    public SimpleCaptchaService(IMemoryCache cache) => _cache = cache;

    public (string captchaId, string question) Create()
    {
        var a = Random.Shared.Next(1, 10);
        var b = Random.Shared.Next(1, 10);
        var id = Guid.NewGuid().ToString("N");

        _cache.Set($"captcha:{id}", (a + b).ToString(), TimeSpan.FromMinutes(3));
        return (id, $"{a} + {b} = ?");
    }

    public bool Validate(string captchaId, string answer)
    {
        if (!_cache.TryGetValue($"captcha:{captchaId}", out string? expected))
            return false;

        _cache.Remove($"captcha:{captchaId}");
        return expected == answer?.Trim();
    }
}