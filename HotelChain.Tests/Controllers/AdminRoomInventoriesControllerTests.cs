using HotelChain.Api.Controllers;
using HotelChain.Api.Contracts;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;


namespace HotelChain.Tests.Controllers;

public class AdminRoomInventoriesControllerTests
{
    private static HotelChainDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelChainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HotelChainDbContext(options);
    }

    [Fact]
    public async Task UpsertRange_Should_ReturnBadRequest_When_QuantityTotal_IsNegative()
    {
        await using var db = CreateDbContext();
        var controller = new AdminRoomInventoriesController(db);

        var request = new UpsertRoomInventoryRangeRequest
        {
            RoomId = 1,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            QuantityTotal = -1
        };

        var result = await controller.UpsertRange(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("QuantityTotal no puede ser negativo.", badRequest.Value);
    }

    [Fact]
    public async Task UpsertRange_Should_ReturnBadRequest_When_QuantityTotal_IsGreaterThanOne()
    {
        await using var db = CreateDbContext();
        var controller = new AdminRoomInventoriesController(db);

        var request = new UpsertRoomInventoryRangeRequest
        {
            RoomId = 1,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            QuantityTotal = 2
        };

        var result = await controller.UpsertRange(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Para una habitación individual, la cantidad total no puede ser mayor que 1.", badRequest.Value);
    }

    [Fact]
    public async Task UpsertRange_Should_ReturnBadRequest_When_EndDate_IsBefore_StartDate()
    {
        await using var db = CreateDbContext();
        var controller = new AdminRoomInventoriesController(db);

        var request = new UpsertRoomInventoryRangeRequest
        {
            RoomId = 1,
            StartDate = DateTime.Today.AddDays(2),
            EndDate = DateTime.Today,
            QuantityTotal = 1
        };

        var result = await controller.UpsertRange(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("EndDate no puede ser menor que StartDate.", badRequest.Value);
    }

    [Fact]
    public async Task UpsertRange_Should_ReturnBadRequest_When_RoomId_IsInvalid()
    {
        await using var db = CreateDbContext();
        var controller = new AdminRoomInventoriesController(db);

        var request = new UpsertRoomInventoryRangeRequest
        {
            RoomId = 0,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            QuantityTotal = 1
        };

        var result = await controller.UpsertRange(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("RoomId es requerido.", badRequest.Value);
    }

    [Fact]
    public async Task UpsertRange_Should_ReturnBadRequest_When_Room_DoesNotExist()
    {
        await using var db = CreateDbContext();
        var controller = new AdminRoomInventoriesController(db);

        var request = new UpsertRoomInventoryRangeRequest
        {
            RoomId = 99,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            QuantityTotal = 1
        };

        var result = await controller.UpsertRange(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("La habitación no existe.", badRequest.Value);
    }

    [Fact]
    public async Task UpsertRange_Should_CreateInventory_When_Request_IsValid()
    {
        await using var db = CreateDbContext();

        db.Rooms.Add(new Room
        {
            Id = 1,
            HotelId = 1,
            RoomTypeId = 1,
            NameOrNumber = "101",
            MaxGuests = 2,
            BasePricePerNight = 350,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var controller = new AdminRoomInventoriesController(db);

        var request = new UpsertRoomInventoryRangeRequest
        {
            RoomId = 1,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(2),
            QuantityTotal = 1
        };

        var result = await controller.UpsertRange(request);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var inventoryCount = await db.RoomInventories.CountAsync();
        Assert.Equal(3, inventoryCount);
        Assert.NotNull(okResult.Value);
    }
}