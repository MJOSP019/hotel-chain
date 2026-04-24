using System.Text;
using System.Text.Json;
using HotelChain.Api.Controllers;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Tests.Controllers;

public class AdminAnalyticsControllerTests
{
    private static AdminAnalyticsController CreateController(HotelChain.Infrastructure.Data.HotelChainDbContext db)
    {
        var controller = new AdminAnalyticsController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.ADMIN);
        return controller;
    }

    private static async Task SeedSearchAuditsAsync(HotelChain.Infrastructure.Data.HotelChainDbContext db)
    {
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.SearchAudits.AddRange(
            new SearchAudit
            {
                Id = 1,
                CityId = 1,
                UserId = TestDbFactory.KnownUserId,
                CheckIn = DateTime.Today.AddDays(10),
                CheckOut = DateTime.Today.AddDays(12),
                Guests = 2,
                Source = "WEB",
                RoomTypeId = 1,
                MinPrice = 200m,
                MaxPrice = 500m,
                MinRating = 4,
                CreatedAt = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new SearchAudit
            {
                Id = 2,
                CityId = 1,
                CheckIn = DateTime.Today.AddDays(20),
                CheckOut = DateTime.Today.AddDays(21),
                Guests = 4,
                Source = "INTEGRATION",
                RoomTypeId = 2,
                MinPrice = 600m,
                MaxPrice = 900m,
                MinRating = 5,
                CreatedAt = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSearches_Should_Filter_By_Source_And_Paginate()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedSearchAuditsAsync(db);
        var controller = CreateController(db);

        var result = await controller.GetSearches(
            source: "web",
            cityId: null,
            from: null,
            to: null,
            userId: null,
            guests: null,
            roomTypeId: null,
            minRating: null,
            minPrice: null,
            maxPrice: null,
            page: 0,
            pageSize: 500);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"page\":1", json);
        Assert.Contains("\"pageSize\":100", json);
        Assert.Contains("\"totalItems\":1", json);
        Assert.Contains("WEB", json);
        Assert.DoesNotContain("INTEGRATION", json);
    }

    [Fact]
    public async Task GetSearchesDashboard_Should_Return_Aggregated_Counts()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedSearchAuditsAsync(db);
        var controller = CreateController(db);

        var result = await controller.GetSearchesDashboard(
            source: null,
            cityId: 1,
            from: null,
            to: null,
            userId: null,
            guests: null,
            roomTypeId: null,
            minRating: null,
            minPrice: null,
            maxPrice: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"totalSearches\":2", json);
        Assert.Contains("WEB", json);
        Assert.Contains("INTEGRATION", json);
        Assert.Contains("Guatemala", json);
    }

    [Fact]
    public async Task ExportSearches_Should_Return_Csv_File_With_FilteredRows()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await SeedSearchAuditsAsync(db);
        var controller = CreateController(db);

        var result = await controller.ExportSearches(
            source: "integration",
            cityId: null,
            from: null,
            to: null,
            userId: null,
            guests: null,
            roomTypeId: null,
            minRating: null,
            minPrice: null,
            maxPrice: null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("searches-report-", file.FileDownloadName);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Id,Fecha,Origen,Ciudad", csv);
        Assert.Contains("INTEGRATION", csv);
        Assert.DoesNotContain("WEB,Guatemala", csv);
    }
}
