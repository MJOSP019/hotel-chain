using HotelChain.Api.Contracts;
using HotelChain.Api.Controllers;
using HotelChain.Domain.Entities;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class AdminHotelsControllerTests
{
    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_Code_IsMissing()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminHotelsController(db);

        var result = await controller.Create(new SaveHotelRequest
        {
            Code = " ",
            Name = "Nuevo Hotel",
            Address = "Zona 1",
            CityId = 1
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Code es requerido.", badRequest.Value);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_City_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminHotelsController(db);

        var result = await controller.Create(new SaveHotelRequest
        {
            Code = "HGT999",
            Name = "Nuevo Hotel",
            Address = "Zona 1",
            CityId = 99
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("CityId inválido.", badRequest.Value);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_Code_AlreadyExists()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminHotelsController(db);

        var result = await controller.Create(new SaveHotelRequest
        {
            Code = "HGT001",
            Name = "Hotel Duplicado",
            Address = "Zona 1",
            CityId = 1
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Ya existe un hotel con ese código.", badRequest.Value);
    }

    [Fact]
    public async Task Create_Should_SaveHotel_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminHotelsController(db);

        var result = await controller.Create(new SaveHotelRequest
        {
            Code = " HGT002 ",
            Name = " Hotel Norte ",
            Address = " Zona 15 ",
            Description = "  Ejecutivo  ",
            CityId = 1
        });

        Assert.IsType<OkObjectResult>(result);
        var hotel = await db.Hotels.SingleAsync(h => h.Code == "HGT002");
        Assert.Equal("Hotel Norte", hotel.Name);
        Assert.Equal("Zona 15", hotel.Address);
        Assert.True(hotel.IsActive);
    }

    [Fact]
    public async Task GetById_Should_ReturnNotFound_When_Hotel_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new AdminHotelsController(db);

        var result = await controller.GetById(404);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Hotel no existe.", notFound.Value);
    }

    [Fact]
    public async Task Deactivate_And_Reactivate_Should_Update_IsActive()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new AdminHotelsController(db);

        var deactivateResult = await controller.Deactivate(1);
        Assert.IsType<OkObjectResult>(deactivateResult);
        Assert.False((await db.Hotels.FindAsync(1))!.IsActive);

        var reactivateResult = await controller.Reactivate(1);
        Assert.IsType<OkObjectResult>(reactivateResult);
        Assert.True((await db.Hotels.FindAsync(1))!.IsActive);
    }

    [Fact]
    public async Task Update_Should_ReturnBadRequest_When_NewCode_Belongs_To_OtherHotel()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.Hotels.Add(new Hotel
        {
            Id = 2,
            Code = "HGT002",
            Name = "Otro Hotel",
            Address = "Zona 4",
            CityId = 1,
            IsActive = true
        });
        await db.SaveChangesAsync();
        var controller = new AdminHotelsController(db);

        var result = await controller.Update(2, new SaveHotelRequest
        {
            Code = "HGT001",
            Name = "Otro Hotel",
            Address = "Zona 4",
            CityId = 1
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Ya existe otro hotel con ese código.", badRequest.Value);
    }
}
