using System.Net;
using System.Net.Http;
using System.Text;
using Bunit;
using HotelChain.Web.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotelChain.Tests.Frontend;

public class SearchTests : BunitContext
{
    [Fact]
    public void Search_Should_Render_Main_Title()
    {
        // Arrange
        Services.AddSingleton(CreateHttpClient(new Dictionary<string, string>
        {
            ["/api/public/cities"] = """
                [
                  { "id": 1, "name": "Guatemala", "countryCode": "GT" }
                ]
                """,
            ["/api/public/room-types"] = """
                [
                  { "id": 1, "name": "Suite" }
                ]
                """
        }));

        // Act
        var cut = Render<Search>();

        // Assert
        Assert.Contains("Encuentra el hotel ideal para tu próxima estadía", cut.Markup);
    }

    [Fact]
    public void Search_Should_Render_Search_Button()
    {
        // Arrange
        Services.AddSingleton(CreateHttpClient(new Dictionary<string, string>
        {
            ["/api/public/cities"] = """
                [
                  { "id": 1, "name": "Guatemala", "countryCode": "GT" }
                ]
                """,
            ["/api/public/room-types"] = """
                [
                  { "id": 1, "name": "Suite" }
                ]
                """
        }));

        // Act
        var cut = Render<Search>();

        // Assert
        Assert.Contains("Buscar hoteles", cut.Markup);
    }

    private static HttpClient CreateHttpClient(Dictionary<string, string> responses)
    {
        var handler = new FakeHttpMessageHandler(responses);

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses;

        public FakeHttpMessageHandler(Dictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (_responses.TryGetValue(path, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("")
            });
        }
    }
}