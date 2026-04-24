using System.Text.Json;
using HotelChain.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Tests.Controllers;

public class PublicControllerTests
{
    [Fact]
    public void Ping_Should_ReturnPongMessage()
    {
        var controller = new PublicController();

        var result = controller.Ping();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("pong", json);
    }
}
