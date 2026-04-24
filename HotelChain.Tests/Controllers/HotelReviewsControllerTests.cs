using HotelChain.Api.Contracts;
using HotelChain.Api.Controllers;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Tests.Controllers;

public class HotelReviewsControllerTests
{
    [Fact]
    public async Task GetByHotel_Should_ReturnNotFound_When_Hotel_DoesNotExist()
    {
        await using var db = TestDbFactory.CreateDbContext();
        var controller = new HotelReviewsController(db);

        var result = await controller.GetByHotel(99);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Hotel no existe.", notFound.Value);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_When_Rating_IsOutOfRange_ForTopLevelReview()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new HotelReviewsController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Create(1, new CreateHotelReviewRequest
        {
            Rating = 6,
            Comment = "Muy bueno"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Rating debe estar entre 1 y 5.", badRequest.Value);
    }

    [Fact]
    public async Task Create_Should_ReturnUnauthorized_When_UserClaim_IsMissing()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new HotelReviewsController(db);
        TestDbFactory.SetAnonymousUser(controller);

        var result = await controller.Create(1, new CreateHotelReviewRequest
        {
            Rating = 5,
            Comment = "Excelente"
        });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Create_Should_SaveTopLevelReview_When_Request_IsValid()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        var controller = new HotelReviewsController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.REGISTERED);

        var result = await controller.Create(1, new CreateHotelReviewRequest
        {
            Rating = 5,
            Comment = " Excelente servicio "
        });

        Assert.IsType<OkObjectResult>(result);
        var review = await db.HotelReviews.SingleAsync();
        Assert.Equal(5, review.Rating);
        Assert.Equal("Excelente servicio", review.Comment);
        Assert.Null(review.ParentReviewId);
    }

    [Fact]
    public async Task Create_Should_SaveReply_WithRatingZero_When_ParentExists()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.HotelReviews.Add(new HotelReview
        {
            Id = 1,
            HotelId = 1,
            UserId = TestDbFactory.KnownUserId,
            Rating = 4,
            Comment = "Buena experiencia",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        var controller = new HotelReviewsController(db);
        TestDbFactory.SetUser(controller, TestDbFactory.KnownUserId, Roles.ADMIN);

        var result = await controller.Create(1, new CreateHotelReviewRequest
        {
            ParentReviewId = 1,
            Comment = "Gracias por comentar"
        });

        Assert.IsType<OkObjectResult>(result);
        var reply = await db.HotelReviews.SingleAsync(x => x.ParentReviewId == 1);
        Assert.Equal(0, reply.Rating);
        Assert.Equal("Gracias por comentar", reply.Comment);
    }

    [Fact]
    public async Task GetByHotel_Should_ReturnReviewTree_WithReplies()
    {
        await using var db = TestDbFactory.CreateDbContext();
        await TestDbFactory.SeedBasicCatalogAsync(db);
        db.HotelReviews.AddRange(
            new HotelReview
            {
                Id = 1,
                HotelId = 1,
                UserId = TestDbFactory.KnownUserId,
                Rating = 5,
                Comment = "Excelente",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new HotelReview
            {
                Id = 2,
                HotelId = 1,
                UserId = TestDbFactory.KnownUserId,
                Rating = 0,
                Comment = "Respuesta del hotel",
                ParentReviewId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        await db.SaveChangesAsync();
        var controller = new HotelReviewsController(db);

        var result = await controller.GetByHotel(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var reviews = Assert.IsAssignableFrom<List<HotelReviewDto>>(ok.Value);
        var root = Assert.Single(reviews);
        Assert.Single(root.Replies);
        Assert.Equal("Respuesta del hotel", root.Replies[0].Comment);
    }
}
