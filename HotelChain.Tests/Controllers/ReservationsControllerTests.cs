using HotelChain.Api.Controllers;
using HotelChain.Infrastructure.Auth;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class ReservationsControllerTests
{
    private static ReservationsController CreateController(HotelChain.Infrastructure.Data.HotelChainDbContext db, Guid? userId = null, params string[] roles)
    {
        var email = TestDbFactory.EmailSenderMock();
        var controller = new ReservationsController(db, email.Object, TestDbFactory.NullLogger<ReservationsController>());
        if (userId.HasValue)
            TestDbFactory.SetUser(controller, userId.Value, roles);
        else
            TestDbFactory.SetAnonymousUser(controller);
        return controller;
    }

    [Fact]
    public async Task Create_Should_ReturnUnauthorized_When_UserClaim_IsMissing()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.Create(new ReservationsController.CreateReservationRequest
        {
            HotelId = 1,
            RoomTypeId = 1,
            CheckIn = DateTime.Today.AddDays(1),
            CheckOut = DateTime.Today.AddDays(2),
            Guests = 2
        });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_Dates_AreInvalid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = CreateController(db, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Create(new ReservationsController.CreateReservationRequest
        {
            HotelId = 1,
            RoomTypeId = 1,
            CheckIn = DateTime.Today.AddDays(3),
            CheckOut = DateTime.Today.AddDays(3),
            Guests = 2
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Fechas inválidas.", badRequest.Value);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_NoCommercialOptionExists()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = CreateController(db, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Create(new ReservationsController.CreateReservationRequest
        {
            HotelId = 1,
            RoomTypeId = 1,
            CheckIn = DateTime.Today.AddDays(5),
            CheckOut = DateTime.Today.AddDays(7),
            Guests = 99
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No existe una opción activa para ese tipo de habitación con la capacidad solicitada.", badRequest.Value);
    }

    [Fact]
    public async Task Create_Should_CreatePendingReservation_And_ReserveCommercialInventory_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var start = DateTime.Today.AddDays(7);
        var end = start.AddDays(2);
        await TestDbFactory.AddCommercialInventoryAsync(db, 1, 1, start, end, quantityTotal: 2);
        var controller = CreateController(db, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Create(new ReservationsController.CreateReservationRequest
        {
            HotelId = 1,
            RoomTypeId = 1,
            CheckIn = start,
            CheckOut = end,
            Guests = 2
        });

        Assert.IsType<OkObjectResult>(result);
        var reservation = await db.Reservations.Include(r => r.Rooms).SingleAsync();
        Assert.Equal("PENDING", reservation.Status);
        Assert.Equal(700m, reservation.TotalAmount);
        Assert.NotNull(reservation.ExpiresAt);
        Assert.Single(reservation.Rooms);
        Assert.All(await db.RoomTypeInventories.Where(x => x.Date >= start && x.Date < end).ToListAsync(), x => Assert.Equal(1, x.QuantityReserved));
    }

    [Fact]
    public async Task GetByCode_Should_ReturnNotFound_When_Reservation_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.GetByCode("R-NOPE");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Checkout_Should_ReturnBadRequest_When_Card_FailsLuhn()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Reservations.Add(new()
        {
            Id = 1,
            Code = "R-TEST",
            HotelId = 1,
            UserId = TestDbFactory.KnownUserId,
            CheckIn = DateTime.Today.AddDays(7),
            CheckOut = DateTime.Today.AddDays(9),
            Guests = 2,
            TotalAmount = 700m,
            Status = "PENDING"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Checkout("R-TEST", new ReservationsController.CheckoutRequest
        {
            CardNumber = "4111111111111112",
            Cvv = "123",
            CardHolderName = "Test User",
            BillingAddress = "Zona 10"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Número de tarjeta inválido (Luhn).", badRequest.Value);
    }

    [Fact]
    public async Task Checkout_Should_ConfirmReservation_And_CreatePayment_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Reservations.Add(new()
        {
            Id = 1,
            Code = "R-TEST",
            HotelId = 1,
            UserId = TestDbFactory.KnownUserId,
            CheckIn = DateTime.Today.AddDays(7),
            CheckOut = DateTime.Today.AddDays(9),
            Guests = 2,
            TotalAmount = 700m,
            Status = "PENDING"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Checkout("R-TEST", new ReservationsController.CheckoutRequest
        {
            CardNumber = "4111111111111111",
            Cvv = "123",
            CardHolderName = "Test User",
            BillingAddress = "Zona 10"
        });

        Assert.IsType<OkObjectResult>(result);
        var reservation = await db.Reservations.Include(r => r.Payment).SingleAsync();
        Assert.Equal("CONFIRMED", reservation.Status);
        Assert.NotNull(reservation.Payment);
        Assert.Equal("1111", reservation.Payment!.Last4);
    }
}
