using HotelChain.Api.Controllers;
using HotelChain.Api.DTOs.Admin;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class AdminRoomsControllerTests
{
    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_Hotel_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminRoomsController(db);

        var result = await controller.Create(new SaveRoomRequest
        {
            HotelId = 99,
            RoomTypeId = 1,
            NameOrNumber = "301",
            MaxGuests = 2,
            BasePricePerNight = 350m
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_RoomType_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminRoomsController(db);

        var result = await controller.Create(new SaveRoomRequest
        {
            HotelId = 1,
            RoomTypeId = 99,
            NameOrNumber = "301",
            MaxGuests = 2,
            BasePricePerNight = 350m
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_RoomName_AlreadyExists_InSameHotel()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminRoomsController(db);

        var result = await controller.Create(new SaveRoomRequest
        {
            HotelId = 1,
            RoomTypeId = 1,
            NameOrNumber = "101",
            MaxGuests = 2,
            BasePricePerNight = 350m
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_Should_SaveRoom_WithTrimmedOptionalFields_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminRoomsController(db);

        var result = await controller.Create(new SaveRoomRequest
        {
            HotelId = 1,
            RoomTypeId = 2,
            NameOrNumber = " 301 ",
            MaxGuests = 4,
            BasePricePerNight = 800m,
            BedType = " King ",
            ShortDescription = " Suite amplia ",
            ImageUrl = " /img/301.jpg ",
            IsActive = true
        });

        Assert.IsType<OkObjectResult>(result);
        var room = await db.Rooms.SingleAsync(r => r.NameOrNumber == "301");
        Assert.Equal("King", room.BedType);
        Assert.Equal("Suite amplia", room.ShortDescription);
        Assert.Equal("/img/301.jpg", room.ImageUrl);
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_When_Room_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new AdminRoomsController(db);

        var result = await controller.Update(404, new SaveRoomRequest
        {
            HotelId = 1,
            RoomTypeId = 1,
            NameOrNumber = "404",
            MaxGuests = 2,
            BasePricePerNight = 400m
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Deactivate_And_Reactivate_Should_Update_IsActive()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminRoomsController(db);

        var deactivateResult = await controller.Deactivate(1);
        Assert.IsType<OkObjectResult>(deactivateResult);
        Assert.False((await db.Rooms.FindAsync(1))!.IsActive);

        var reactivateResult = await controller.Reactivate(1);
        Assert.IsType<OkObjectResult>(reactivateResult);
        Assert.True((await db.Rooms.FindAsync(1))!.IsActive);
    }
}
