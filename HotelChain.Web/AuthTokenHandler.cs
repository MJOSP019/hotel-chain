using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace HotelChain.Web;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IJSRuntime _js;

    public AuthTokenHandler(IJSRuntime js)
    {
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _js.InvokeAsync<string>("auth.getToken");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}