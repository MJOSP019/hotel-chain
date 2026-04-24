using HotelChain.Api.Contracts;
using HotelChain.Api.Controllers;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Tests.Controllers;

public class PublicReadControllersTests
{
    [Fact]
    public async Task Cities_Get_Should_ReturnCities_OrderedByName()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Cities.Add(new() { Id = 2, Name = "Antigua", CountryCode = "GT" });
        await db.SaveChangesAsync();
        var controller = new CitiesController(db);

        var result = await controller.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RoomTypes_Get_Should_ReturnRoomTypes_OrderedByName()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new RoomTypesController(db);

        var actionResult = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var roomTypes = Assert.IsAssignableFrom<List<RoomTypeDto>>(ok.Value);
        Assert.Equal(new[] { "Double", "Suite" }, roomTypes.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task PublicHotels_GetById_Should_ReturnNotFound_When_Hotel_IsInactive()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var hotel = await db.Hotels.FindAsync(1);
        hotel!.IsActive = false;
        await db.SaveChangesAsync();
        var controller = new PublicHotelsController(db);

        var result = await controller.GetById(1);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task PublicHotels_GetById_Should_ReturnRoomOptions_When_NoStayContext()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new PublicHotelsController(db);

        var result = await controller.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HotelDetailDto>(ok.Value);
        Assert.Equal("Hotel Central", dto.Name);
        Assert.Equal(2, dto.RoomOptions.Count);
        Assert.Contains(dto.RoomOptions, x => x.RoomType == "Double" && x.AvailableUnits == 2);
    }

    [Fact]
    public async Task PublicHotels_GetById_Should_ApplyAvailability_When_StayContext_IsProvided()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(10);
        var end = start.AddDays(2);
        await TestDbFactory.AddCommercialInventoryAsync(db, 1, 1, start, end, quantityTotal: 2, quantityReserved: 1);
        var controller = new PublicHotelsController(db);

        var result = await controller.GetById(1, start, end, guests: 2);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HotelDetailDto>(ok.Value);
        var option = Assert.Single(dto.RoomOptions);
        Assert.Equal("Double", option.RoomType);
        Assert.Equal(1, option.AvailableUnits);
    }
}
