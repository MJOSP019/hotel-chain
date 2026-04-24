using System.Security.Claims;
using HotelChain.Api.Contracts;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/public/hotels/{hotelId:int}/reviews")]
public class HotelReviewsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public HotelReviewsController(HotelChainDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetByHotel(int hotelId)
    {
        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == hotelId);
        if (!hotelExists)
            return NotFound("Hotel no existe.");

        var flatReviews = await (
            from review in _db.HotelReviews.AsNoTracking()
            join user in _db.Users.AsNoTracking() on review.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where review.HotelId == hotelId
            orderby review.CreatedAt descending
            select new HotelReviewDto
            {
                Id = review.Id,
                HotelId = review.HotelId,
                UserId = review.UserId,
                UserName = user != null
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : "Usuario",
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt,
                ParentReviewId = review.ParentReviewId
            })
            .ToListAsync();

        foreach (var review in flatReviews)
        {
            if (string.IsNullOrWhiteSpace(review.UserName))
                review.UserName = "Usuario";
        }

        var reviewsTree = BuildReviewTree(flatReviews);

        return Ok(reviewsTree);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.REGISTERED},{Roles.ADMIN}")]
    public async Task<IActionResult> Create(int hotelId, [FromBody] CreateHotelReviewRequest req)
    {
        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == hotelId);
        if (!hotelExists)
            return NotFound("Hotel no existe.");

        var isReply = req.ParentReviewId.HasValue;

        if (!isReply && (req.Rating < 1 || req.Rating > 5))
            return BadRequest("Rating debe estar entre 1 y 5.");

        if (string.IsNullOrWhiteSpace(req.Comment) && !isReply && req.Rating <= 0)
            return BadRequest("Debe enviar comentario o calificación.");

        if (string.IsNullOrWhiteSpace(req.Comment) && isReply)
            return BadRequest("La respuesta debe incluir un comentario.");

        HotelReview? parentReview = null;

        if (isReply)
        {
            parentReview = await _db.HotelReviews
                .FirstOrDefaultAsync(x => x.Id == req.ParentReviewId!.Value);

            if (parentReview is null)
                return BadRequest("El comentario padre no existe.");

            if (parentReview.HotelId != hotelId)
                return BadRequest("El comentario padre no pertenece a este hotel.");
        }

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var review = new HotelReview
        {
            HotelId = hotelId,
            UserId = userId,
            Rating = isReply ? 0 : req.Rating,
            Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(),
            CreatedAt = DateTime.UtcNow,
            ParentReviewId = req.ParentReviewId
        };

        _db.HotelReviews.Add(review);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = isReply
                ? "Respuesta creada correctamente."
                : "Reseña creada correctamente."
        });
    }

    private static List<HotelReviewDto> BuildReviewTree(List<HotelReviewDto> flatReviews)
    {
        var byId = flatReviews.ToDictionary(x => x.Id);

        foreach (var review in flatReviews)
        {
            review.Replies = new List<HotelReviewDto>();
        }

        var roots = new List<HotelReviewDto>();

        foreach (var review in flatReviews.OrderBy(x => x.CreatedAt))
        {
            if (review.ParentReviewId.HasValue &&
                byId.TryGetValue(review.ParentReviewId.Value, out var parent))
            {
                parent.Replies.Add(review);
            }
            else
            {
                roots.Add(review);
            }
        }

        return roots
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }
}