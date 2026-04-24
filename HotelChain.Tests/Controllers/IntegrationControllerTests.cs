using System.Text.Json;
using HotelChain.Api.Controllers;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class IntegrationControllerTests
{
    private static IntegrationController CreateController(HotelChain.Infrastructure.Data.HotelChainDbContext db)
    {
        var controller = new IntegrationController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.WEBSERVICE);
        return controller;
    }

    [Fact]
    public async Task GetHotels_Should_Return_OnlyActiveHotels_FromCity()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Hotels.Add(new Hotel { Id = 2, Code = "HGT002", Name = "Hotel Inactivo", Address = "Zona 1", CityId = 1, IsActive = false });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetHotels(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Hotel Central", json);
        Assert.DoesNotContain("Hotel Inactivo", json);
    }

    [Fact]
    public async Task GetCities_Should_Return_CatalogCities()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = CreateController(db);

        var result = await controller.GetCities();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Guatemala", json);
    }

    [Fact]
    public async Task GetReservationByCode_Should_ReturnNotFound_When_CodeDoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = CreateController(db);

        var result = await controller.GetReservationByCode("R-NO-EXISTE");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Reserva no existe.", notFound.Value);
    }

    [Fact]
    public async Task Search_Should_ReturnBadRequest_When_CheckOut_IsNotAfterCheckIn()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = CreateController(db);
        var date = DateTime.Today.AddDays(10);

        var result = await controller.Search(new IntegrationController.IntegrationSearchRequest
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
    public async Task CreateReservation_Should_ReturnBadRequest_When_Inventory_IsIncomplete()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(10);
        var end = start.AddDays(2);
        await TestDbFactory.AddPhysicalInventoryAsync(db, 1, start, start.AddDays(1));
        var controller = CreateController(db);

        var result = await controller.CreateReservation(new IntegrationController.IntegrationCreateReservationRequest
        {
            RoomId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 2
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No hay inventario completo para esas fechas.", badRequest.Value);
    }

    [Fact]
    public async Task CreateReservation_Should_SaveReservation_And_ReserveInventory_When_RequestIsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(10);
        var end = start.AddDays(2);
        await TestDbFactory.AddPhysicalInventoryAsync(db, 1, start, end);
        var controller = CreateController(db);

        var result = await controller.CreateReservation(new IntegrationController.IntegrationCreateReservationRequest
        {
            RoomId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 2
        });

        Assert.IsType<OkObjectResult>(result);
        var reservation = await db.Reservations.Include(r => r.Rooms).SingleAsync();
        Assert.Equal("PENDING", reservation.Status);
        Assert.Equal(700m, reservation.TotalAmount);
        Assert.All(await db.RoomInventories.ToListAsync(), inv => Assert.Equal(1, inv.QuantityReserved));
    }

    [Fact]
    public async Task CancelReservation_Should_BeIdempotent_When_AlreadyCanceled()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Reservations.Add(new Reservation
        {
            Id = 1,
            Code = "R-CANCELED",
            HotelId = 1,
            CheckIn = DateTime.Today.AddDays(10),
            CheckOut = DateTime.Today.AddDays(11),
            Guests = 2,
            TotalAmount = 350m,
            Status = "CANCELED"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.CancelReservation("R-CANCELED");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("CANCELED", json);
    }

    [Fact]
    public async Task CancelReservation_Should_FreeInventory_And_CreateAudit_When_Allowed()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(10);
        var end = start.AddDays(2);
        await TestDbFactory.AddPhysicalInventoryAsync(db, 1, start, end, quantityReserved: 1);
        db.Reservations.Add(new Reservation
        {
            Id = 1,
            Code = "R-WS-CANCEL",
            HotelId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 2,
            TotalAmount = 700m,
            Status = "CONFIRMED",
            Rooms = new List<ReservationRoom>
            {
                new() { Id = 1, RoomTypeId = 1, RoomId = 1, PricePerNight = 350m, Nights = 2, Subtotal = 700m }
            }
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.CancelReservation("R-WS-CANCEL");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("CANCELED", (await db.Reservations.FindAsync(1))!.Status);
        Assert.All(await db.RoomInventories.ToListAsync(), inv => Assert.Equal(0, inv.QuantityReserved));
        Assert.Contains(await db.ReservationAudits.ToListAsync(), a => a.Action == "CANCEL" && a.Actor == "WEBSERVICE");
    }
}
