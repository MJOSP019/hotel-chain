using System.Security.Claims;
using HotelChain.Api.Services;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;

namespace HotelChain.Tests.TestSupport;

public static class TestDbFactory
{
    public static HotelChainDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelChainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new HotelChainDbContext(options);
    }

    public static ILogger<T> NullLogger<T>() => Mock.Of<ILogger<T>>();

    public static Mock<IEmailSender> EmailSenderMock()
    {
        var mock = new Mock<IEmailSender>();
        mock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    public static void SetUser(ControllerBase controller, Guid userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "tester@hotelchain.local"),
            new(ClaimTypes.Name, "tester@hotelchain.local")
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    public static void SetAnonymousUser(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    public static async Task SeedBasicCatalogAsync(HotelChainDbContext db)
    {
        db.Cities.Add(new City
        {
            Id = 1,
            Name = "Guatemala",
            CountryCode = "GT"
        });

        db.RoomTypes.AddRange(
            new RoomType { Id = 1, Name = "Double" },
            new RoomType { Id = 2, Name = "Suite" });

        db.Hotels.Add(new Hotel
        {
            Id = 1,
            Code = "HGT001",
            Name = "Hotel Central",
            Address = "Zona 10",
            Description = "Hotel de prueba",
            CityId = 1,
            IsActive = true
        });

        db.Rooms.AddRange(
            new Room
            {
                Id = 1,
                HotelId = 1,
                RoomTypeId = 1,
                NameOrNumber = "101",
                MaxGuests = 2,
                BasePricePerNight = 350m,
                BedType = "Queen",
                AreaSquareMeters = 25m,
                ShortDescription = "Habitación doble",
                ImageUrl = "/img/101.jpg",
                IsActive = true
            },
            new Room
            {
                Id = 2,
                HotelId = 1,
                RoomTypeId = 1,
                NameOrNumber = "102",
                MaxGuests = 2,
                BasePricePerNight = 375m,
                IsActive = true
            },
            new Room
            {
                Id = 3,
                HotelId = 1,
                RoomTypeId = 2,
                NameOrNumber = "201",
                MaxGuests = 4,
                BasePricePerNight = 700m,
                IsActive = true
            });

        db.Users.Add(new ApplicationUser
        {
            Id = KnownUserId,
            UserName = "tester@hotelchain.local",
            Email = "tester@hotelchain.local",
            FirstName = "Test",
            LastName = "User",
            Age = 25,
            Country = "Guatemala",
            PassportNumber = "A123456"
        });

        await db.SaveChangesAsync();
    }

    public static readonly Guid KnownUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task AddCommercialInventoryAsync(
        HotelChainDbContext db,
        int hotelId,
        int roomTypeId,
        DateTime start,
        DateTime end,
        int quantityTotal = 2,
        int quantityReserved = 0,
        bool isClosed = false,
        bool closedToArrival = false,
        bool closedToDepartureOnEnd = false,
        int? minLos = null,
        int? maxLos = null)
    {
        for (var date = start.Date; date < end.Date; date = date.AddDays(1))
        {
            db.RoomTypeInventories.Add(new RoomTypeInventory
            {
                HotelId = hotelId,
                RoomTypeId = roomTypeId,
                Date = date,
                QuantityTotal = quantityTotal,
                QuantityReserved = quantityReserved,
                IsClosed = isClosed,
                ClosedToArrival = date == start.Date && closedToArrival,
                MinLengthOfStay = minLos,
                MaxLengthOfStay = maxLos
            });
        }

        db.RoomTypeInventories.Add(new RoomTypeInventory
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Date = end.Date,
            QuantityTotal = quantityTotal,
            QuantityReserved = quantityReserved,
            ClosedToDeparture = closedToDepartureOnEnd
        });

        await db.SaveChangesAsync();
    }

    public static async Task AddPhysicalInventoryAsync(
        HotelChainDbContext db,
        int roomId,
        DateTime start,
        DateTime end,
        int quantityTotal = 1,
        int quantityReserved = 0)
    {
        for (var date = start.Date; date < end.Date; date = date.AddDays(1))
        {
            db.RoomInventories.Add(new RoomInventory
            {
                RoomId = roomId,
                Date = date,
                QuantityTotal = quantityTotal,
                QuantityReserved = quantityReserved
            });
        }

        await db.SaveChangesAsync();
    }
}
