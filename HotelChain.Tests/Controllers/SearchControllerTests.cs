using HotelChain.Api.Controllers;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class SearchControllerTests
{
    [Fact]
    public async Task Search_Should_ReturnBadRequest_When_CheckOut_IsNotAfter_CheckIn()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new SearchController(db);
        TestDbFactory.SetAnonymousUser(controller);

        var result = await controller.Search(new SearchController.SearchRequest
        {
            CityId = 1,
            CheckIn = DateTime.Today.AddDays(2),
            CheckOut = DateTime.Today.AddDays(2),
            Guests = 2
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("CheckOut debe ser mayor que CheckIn.", badRequest.Value);
    }

    [Fact]
    public async Task Search_Should_ReturnBadRequest_When_Guests_IsInvalid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new SearchController(db);
        TestDbFactory.SetAnonymousUser(controller);

        var result = await controller.Search(new SearchController.SearchRequest
        {
            CityId = 1,
            CheckIn = DateTime.Today.AddDays(1),
            CheckOut = DateTime.Today.AddDays(3),
            Guests = 0
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Guests inválido.", badRequest.Value);
    }

    [Fact]
    public async Task Search_Should_RegisterAudit_And_ReturnAvailableOptions_When_InventoryExists()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(5);
        var end = start.AddDays(2);
        await TestDbFactory.AddCommercialInventoryAsync(db, 1, 1, start, end, quantityTotal: 2);
        var controller = new SearchController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId);

        var result = await controller.Search(new SearchController.SearchRequest
        {
            CityId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 2,
            RoomTypeId = 1
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SearchController.SearchResponseDto>(ok.Value);
        Assert.Single(response.Results);
        Assert.Equal(2, response.Results[0].AvailableUnits);
        Assert.Empty(response.RestrictionMessages);
        var audit = await db.SearchAudits.SingleAsync();
        Assert.Equal("WEB", audit.Source);
        Assert.Equal(TestDbFactory.KnownUserId, audit.UserId);
    }

    [Fact]
    public async Task Search_Should_ReturnRestrictionMessage_When_Inventory_IsClosed()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(5);
        var end = start.AddDays(2);
        await TestDbFactory.AddCommercialInventoryAsync(db, 1, 1, start, end, quantityTotal: 2, isClosed: true);
        var controller = new SearchController(db);
        TestDbFactory.SetAnonymousUser(controller);

        var result = await controller.Search(new SearchController.SearchRequest
        {
            CityId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 2,
            RoomTypeId = 1
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SearchController.SearchResponseDto>(ok.Value);
        Assert.Empty(response.Results);
        Assert.Contains(response.RestrictionMessages, x => x.Code == "IS_CLOSED");
    }

    [Fact]
    public async Task Search_Should_Filter_By_MinPrice_MaxPrice_And_RoomType()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(5);
        var end = start.AddDays(2);
        await TestDbFactory.AddCommercialInventoryAsync(db, 1, 2, start, end, quantityTotal: 1);
        var controller = new SearchController(db);
        TestDbFactory.SetAnonymousUser(controller);

        var result = await controller.Search(new SearchController.SearchRequest
        {
            CityId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 4,
            MinPrice = 650m,
            MaxPrice = 750m,
            RoomTypeId = 2
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SearchController.SearchResponseDto>(ok.Value);
        var option = Assert.Single(response.Results);
        Assert.Equal(2, option.RoomTypeId);
        Assert.Equal(700m, option.BasePricePerNight);
    }
}
