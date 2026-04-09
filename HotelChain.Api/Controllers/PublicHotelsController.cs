using HotelChain.Api.Contracts;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/public/hotels")]
public class PublicHotelsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public PublicHotelsController(HotelChainDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HotelDetailDto>> GetById(int id)
    {
        var hotel = await _db.Hotels
            .AsNoTracking()
            .Where(h => h.Id == id && h.IsActive)
            .Select(h => new
            {
                h.Id,
                h.Code,
                h.Name,
                h.Address,
                h.Description,
                City = h.City.Name
            })
            .FirstOrDefaultAsync();

        if (hotel is null)
            return NotFound(new { message = "Hotel no encontrado." });

        var reviews = await (
            from r in _db.HotelReviews.AsNoTracking()
            join u in _db.Users.AsNoTracking() on r.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            where r.HotelId == id
            orderby r.CreatedAt descending
            select new HotelReviewDto
            {
                Id = r.Id,
                HotelId = r.HotelId,
                UserId = r.UserId,
                UserName = u != null
                    ? $"{u.FirstName} {u.LastName}".Trim()
                    : "Usuario",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        foreach (var review in reviews)
        {
            if (string.IsNullOrWhiteSpace(review.UserName))
                review.UserName = "Usuario";
        }

        var roomOptions = await _db.Rooms
            .AsNoTracking()
            .Where(r => r.HotelId == id && r.IsActive)
            .OrderBy(r => r.BasePricePerNight)
            .Select(r => new HotelRoomOptionDto
            {
                RoomId = r.Id,
                NameOrNumber = r.NameOrNumber,
                MaxGuests = r.MaxGuests,
                BasePricePerNight = r.BasePricePerNight,
                RoomTypeId = r.RoomTypeId,
                RoomType = r.RoomType.Name
            })
            .ToListAsync();

        var dto = new HotelDetailDto
        {
            Id = hotel.Id,
            Code = hotel.Code,
            Name = hotel.Name,
            Address = hotel.Address,
            Description = hotel.Description,
            City = hotel.City,
            ReviewsCount = reviews.Count,
            AverageRating = reviews.Count > 0
                ? Math.Round(reviews.Average(x => (decimal)x.Rating), 2)
                : 0,
            Reviews = reviews,
            RoomOptions = roomOptions
        };

        return Ok(dto);
    }
}