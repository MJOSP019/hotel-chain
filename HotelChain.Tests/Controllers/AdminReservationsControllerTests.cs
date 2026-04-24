using HotelChain.Api.Controllers;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class AdminReservationsControllerTests
{
    private static AdminReservationsController CreateController(HotelChain.Infrastructure.Data.HotelChainDbContext db)
    {
        var controller = new AdminReservationsController(db, TestDbFactory.EmailSenderMock().Object);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.ADMIN);
        return controller;
    }

    private static async Task SeedReservationAsync(HotelChain.Infrastructure.Data.HotelChainDbContext db, string status = "CHECKED_IN")
    {
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Reservations.Add(new Reservation
        {
            Id = 1,
            Code = "R-ADMIN",
            HotelId = 1,
            UserId = TestDbFactory.KnownUserId,
            CheckIn = DateTime.Today,
            CheckOut = DateTime.Today.AddDays(2),
            Guests = 2,
            TotalAmount = 700m,
            Status = status,
            Rooms = new List<ReservationRoom>
            {
                new()
                {
                    Id = 1,
                    RoomTypeId = 1,
                    RoomId = 1,
                    PricePerNight = 350m,
                    Nights = 2,
                    Subtotal = 700m
                }
            }
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AddCharge_Should_ReturnBadRequest_When_Description_IsMissing()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        var controller = CreateController(db);

        var result = await controller.AddCharge(1, new AdminAddReservationChargeRequest
        {
            Description = " ",
            Amount = 50m
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("La descripción del cargo es obligatoria.", badRequest.Value);
    }

    [Fact]
    public async Task AddCharge_Should_ReturnBadRequest_When_Reservation_IsNotCheckedIn()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db, status: "CONFIRMED");
        var controller = CreateController(db);

        var result = await controller.AddCharge(1, new AdminAddReservationChargeRequest
        {
            Category = "restaurant",
            Description = "Cena",
            Amount = 75m
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Solo se pueden registrar cargos en reservas CHECKED_IN.", badRequest.Value);
    }

    [Fact]
    public async Task AddCharge_Should_SaveCharge_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        var controller = CreateController(db);

        var result = await controller.AddCharge(1, new AdminAddReservationChargeRequest
        {
            Category = "restaurant",
            Description = " Cena ",
            Amount = 75.126m
        });

        Assert.IsType<OkObjectResult>(result);
        var charge = await db.ReservationCharges.SingleAsync();
        Assert.Equal("RESTAURANT", charge.Category);
        Assert.Equal("Cena", charge.Description);
        Assert.Equal(75.13m, charge.Amount);
        Assert.False(charge.IsSettled);
    }

    [Fact]
    public async Task SettleCharges_Should_ReturnOk_When_NoPendingCharges()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        var controller = CreateController(db);

        var result = await controller.SettleCharges(1, new AdminSettleReservationChargesRequest());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task SettleCharges_Should_ReturnBadRequest_When_Amount_DoesNotMatchOutstanding()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        db.ReservationCharges.Add(new ReservationCharge
        {
            ReservationId = 1,
            Category = "OTHER",
            Description = "Lavandería",
            Amount = 100m
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.SettleCharges(1, new AdminSettleReservationChargesRequest
        {
            CardNumber = "4111111111111111",
            Cvv = "123",
            CardHolderName = "Test User",
            BillingAddress = "Zona 10",
            Amount = 50m
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("El monto enviado no coincide con el saldo pendiente de la cuenta.", badRequest.Value);
    }

    [Fact]
    public async Task SettleCharges_Should_MarkChargesAsSettled_And_CreateAudit_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        db.ReservationCharges.AddRange(
            new ReservationCharge { ReservationId = 1, Category = "OTHER", Description = "Minibar", Amount = 40m },
            new ReservationCharge { ReservationId = 1, Category = "OTHER", Description = "Lavandería", Amount = 60m });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.SettleCharges(1, new AdminSettleReservationChargesRequest
        {
            CardNumber = "4111111111111111",
            Cvv = "123",
            CardHolderName = "Test User",
            BillingAddress = "Zona 10",
            Amount = 100m
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.All(await db.ReservationCharges.ToListAsync(), c => Assert.True(c.IsSettled));
        Assert.Contains(await db.ReservationAudits.ToListAsync(), a => a.Action == "SETTLE_CHARGES");
    }

    [Fact]
    public async Task CheckOut_Should_ReturnBadRequest_When_HasUnsettledCharges()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        db.ReservationCharges.Add(new ReservationCharge
        {
            ReservationId = 1,
            Category = "OTHER",
            Description = "Minibar",
            Amount = 40m,
            IsSettled = false
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.CheckOut(1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No se puede hacer check-out porque la cuenta de estancia tiene cargos pendientes de liquidar.", badRequest.Value);
    }

    [Fact]
    public async Task CheckOut_Should_SetStatus_When_Account_IsSettled()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedReservationAsync(db);
        db.ReservationCharges.Add(new ReservationCharge
        {
            ReservationId = 1,
            Category = "OTHER",
            Description = "Minibar",
            Amount = 40m,
            IsSettled = true
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.CheckOut(1);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("CHECKED_OUT", (await db.Reservations.FindAsync(1))!.Status);
    }
}
