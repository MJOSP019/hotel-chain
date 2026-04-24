using System.Text.Json;
using HotelChain.Api.Controllers;
using HotelChain.Infrastructure.Auth;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Tests.Controllers;

public class WsControllersTests
{
    [Fact]
    public async Task WsSearch_Should_ReturnBadRequest_When_DatesAreInvalid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new WsSearchController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.WEBSERVICE);
        var date = DateTime.Today.AddDays(10);

        var result = await controller.Search(new WsSearchController.SearchRequest
        {
            CityId = 1,
            CheckIn = date,
            CheckOut = date,
            Guests = 2
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("CheckOut debe ser mayor que CheckIn.", badRequest.Value);
    }


    [Fact]
    public async Task WsReservations_Create_Should_ReturnAgencyUserId_FromJwt()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new WsReservationsController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.WEBSERVICE);

        var result = await controller.Create(new WsReservationsController.CreateWsReservationRequest
        {
            HotelId = 1,
            CheckIn = DateTime.Today.AddDays(10),
            CheckOut = DateTime.Today.AddDays(12),
            Guests = 2,
            RoomIds = new List<int> { 1 },
            CustomerFirstName = "Ana",
            CustomerLastName = "Pérez",
            CustomerNationality = "GT",
            CustomerBirthDate = new DateTime(1995, 1, 1)
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains(TestDbFactory.KnownUserId.ToString(), json);
        Assert.Contains("Implement", json);
    }
}
