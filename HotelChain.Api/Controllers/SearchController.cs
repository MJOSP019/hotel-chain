using System.Security.Claims;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/public/search")]
public class SearchController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public SearchController(HotelChainDbContext db)
    {
        _db = db;
    }

    public class SearchRequest
    {
        public int CityId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }

        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? RoomTypeId { get; set; }
        public double? MinRating { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequest req)
    {
        var start = req.CheckIn.Date;
        var end = req.CheckOut.Date;

        if (end <= start)
            return BadRequest("CheckOut debe ser mayor que CheckIn.");

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = null;

        if (!string.IsNullOrWhiteSpace(userIdValue) && Guid.TryParse(userIdValue, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        var searchAudit = new SearchAudit
        {
            CityId = req.CityId,
            UserId = userId,
            CheckIn = start,
            CheckOut = end,
            Guests = req.Guests,
            MinPrice = req.MinPrice,
            MaxPrice = req.MaxPrice,
            RoomTypeId = req.RoomTypeId,
            MinRating = req.MinRating,
            Source = "WEB",
            CreatedAt = DateTime.UtcNow
        };

        _db.SearchAudits.Add(searchAudit);
        await _db.SaveChangesAsync();

        var nights = Enumerable.Range(0, (end - start).Days)
            .Select(i => start.AddDays(i))
            .ToList();

        var query = _db.Rooms
            .Where(r => r.IsActive)
            .Where(r => r.Hotel.IsActive)
            .Where(r => r.MaxGuests >= req.Guests)
            .Where(r => r.Hotel.CityId == req.CityId)
            .Where(r => nights.All(d =>
                _db.RoomInventories.Any(inv =>
                    inv.RoomId == r.Id &&
                    inv.Date == d &&
                    (inv.QuantityTotal - inv.QuantityReserved) > 0
                )
            ));

        if (req.MinPrice.HasValue)
            query = query.Where(r => r.BasePricePerNight >= req.MinPrice.Value);

        if (req.MaxPrice.HasValue)
            query = query.Where(r => r.BasePricePerNight <= req.MaxPrice.Value);

        if (req.RoomTypeId.HasValue && req.RoomTypeId.Value > 0)
            query = query.Where(r => r.RoomTypeId == req.RoomTypeId.Value);

        if (req.MinRating.HasValue)
            query = query.Where(r =>
                r.Hotel.Reviews.Any() &&
                r.Hotel.Reviews.Average(rv => rv.Rating) >= req.MinRating.Value
            );

        var rooms = await query
            .Select(r => new
            {
                r.Id,
                HotelId = r.HotelId,
                r.NameOrNumber,
                r.MaxGuests,
                r.BasePricePerNight,
                RoomType = r.RoomType.Name,
                Hotel = r.Hotel.Name,
                RoomTypeId = r.RoomTypeId,
                ReviewsCount = r.Hotel.Reviews.Count(),
                AverageRating = r.Hotel.Reviews.Any()
                    ? Math.Round(r.Hotel.Reviews.Average(rv => (decimal)rv.Rating), 2)
                    : 0
            })
            .ToListAsync();

        return Ok(rooms);
    }
}