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

        var reviews = await _db.HotelReviews
            .Where(x => x.HotelId == hotelId)
            .Join(_db.Users,
                review => review.UserId,
                user => user.Id,
                (review, user) => new HotelReviewDto
                {
                    Id = review.Id,
                    HotelId = review.HotelId,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    UserName = user.UserName ?? user.Email ?? "Usuario",
                    CreatedAt = review.CreatedAt
                })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.REGISTERED},{Roles.ADMIN}")]
    public async Task<IActionResult> Create(int hotelId, [FromBody] CreateHotelReviewRequest req)
    {
        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == hotelId);
        if (!hotelExists)
            return NotFound("Hotel no existe.");

        if (req.Rating < 1 || req.Rating > 5)
            return BadRequest("Rating debe estar entre 1 y 5.");

        if (string.IsNullOrWhiteSpace(req.Comment) && req.Rating <= 0)
            return BadRequest("Debe enviar comentario o calificación.");

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var review = new HotelReview
        {
            HotelId = hotelId,
            UserId = userId,
            Rating = req.Rating,
            Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.HotelReviews.Add(review);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Reseña creada correctamente."
        });
    }
}