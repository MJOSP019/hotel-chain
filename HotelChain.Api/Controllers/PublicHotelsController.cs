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
    public async Task<ActionResult<HotelDetailDto>> GetById(
        int id,
        [FromQuery] DateTime? checkIn = null,
        [FromQuery] DateTime? checkOut = null,
        [FromQuery] int? guests = null)
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
                h.MainImageUrl,
                h.ZoneInfo,
                h.Amenities,
                City = h.City.Name
            })
            .FirstOrDefaultAsync();

        if (hotel is null)
            return NotFound(new { message = "Hotel no encontrado." });

        var flatReviews = await (
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
                Rating = r.ParentReviewId.HasValue ? 0 : r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                ParentReviewId = r.ParentReviewId
            })
            .ToListAsync();

        foreach (var review in flatReviews)
        {
            if (string.IsNullOrWhiteSpace(review.UserName))
                review.UserName = "Usuario";
        }

        var reviewsTree = BuildReviewTree(flatReviews);

        var topLevelReviews = flatReviews
            .Where(x => !x.ParentReviewId.HasValue)
            .ToList();

        var roomOptions = await BuildRoomOptionsAsync(id, checkIn, checkOut, guests);

        var dto = new HotelDetailDto
        {
            Id = hotel.Id,
            Code = hotel.Code,
            Name = hotel.Name,
            Address = hotel.Address,
            Description = hotel.Description,
            MainImageUrl = hotel.MainImageUrl,
            ZoneInfo = hotel.ZoneInfo,
            Amenities = hotel.Amenities,
            City = hotel.City,
            ReviewsCount = topLevelReviews.Count(),
            AverageRating = topLevelReviews.Count() > 0
                ? Math.Round(topLevelReviews.Average(x => (decimal)x.Rating), 2)
                : 0,
            Reviews = reviewsTree,
            RoomOptions = roomOptions
        };

        return Ok(dto);
    }

    private async Task<List<HotelRoomOptionDto>> BuildRoomOptionsAsync(
        int hotelId,
        DateTime? checkIn,
        DateTime? checkOut,
        int? guests)
    {
        var query = _db.Rooms
            .AsNoTracking()
            .Where(r => r.HotelId == hotelId && r.IsActive);

        if (guests.HasValue && guests.Value > 0)
            query = query.Where(r => r.MaxGuests >= guests.Value);

        var rooms = await query
            .OrderBy(r => r.BasePricePerNight)
            .Select(r => new
            {
                r.Id,
                r.RoomTypeId,
                RoomType = r.RoomType.Name,
                r.MaxGuests,
                r.BasePricePerNight,
                r.BedType,
                r.AreaSquareMeters,
                r.ShortDescription,
                r.ImageUrl
            })
            .ToListAsync();

        if (rooms.Count == 0)
            return new List<HotelRoomOptionDto>();

        var groupedRoomMetadata = rooms
            .GroupBy(r => new { r.RoomTypeId, r.RoomType })
            .Select(g =>
            {
                var representative = g
                    .OrderBy(x => x.BasePricePerNight)
                    .ThenByDescending(x => x.MaxGuests)
                    .First();

                return new
                {
                    g.Key.RoomTypeId,
                    g.Key.RoomType,
                    PhysicalUnits = g.Count(),
                    Representative = representative
                };
            })
            .ToList();

        var hasStayContext = checkIn.HasValue && checkOut.HasValue && checkOut.Value.Date > checkIn.Value.Date;

        if (!hasStayContext)
        {
            return groupedRoomMetadata
                .Select(option => new HotelRoomOptionDto
                {
                    RoomTypeId = option.RoomTypeId,
                    RoomType = option.RoomType,
                    MaxGuests = option.Representative.MaxGuests,
                    BasePricePerNight = option.Representative.BasePricePerNight,
                    AvailableUnits = option.PhysicalUnits,
                    BedType = option.Representative.BedType,
                    AreaSquareMeters = option.Representative.AreaSquareMeters,
                    ShortDescription = option.Representative.ShortDescription,
                    ImageUrl = option.Representative.ImageUrl
                })
                .OrderBy(x => x.BasePricePerNight)
                .ThenBy(x => x.RoomType)
                .ToList();
        }

        var start = checkIn!.Value.Date;
        var end = checkOut!.Value.Date;
        var nights = (end - start).Days;
        var roomTypeIds = groupedRoomMetadata.Select(x => x.RoomTypeId).ToList();

        var inventoryRows = await _db.RoomTypeInventories
            .AsNoTracking()
            .Where(inv => inv.HotelId == hotelId)
            .Where(inv => roomTypeIds.Contains(inv.RoomTypeId))
            .Where(inv => inv.Date >= start && inv.Date <= end)
            .ToListAsync();

        var inventoryByRoomType = inventoryRows
            .GroupBy(x => x.RoomTypeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Date).ToList());

        return groupedRoomMetadata
            .Select(option =>
            {
                if (!inventoryByRoomType.TryGetValue(option.RoomTypeId, out var rows))
                    return null;

                var stayRows = rows.Where(x => x.Date >= start && x.Date < end).OrderBy(x => x.Date).ToList();
                if (stayRows.Count != nights)
                    return null;

                if (stayRows.Any(x => x.IsClosed))
                    return null;

                var arrivalRow = stayRows.FirstOrDefault(x => x.Date == start);
                if (arrivalRow?.ClosedToArrival == true)
                    return null;

                var departureRow = rows.FirstOrDefault(x => x.Date == end);
                if (departureRow?.ClosedToDeparture == true)
                    return null;

                var minLos = stayRows.Where(x => x.MinLengthOfStay.HasValue)
                    .Select(x => x.MinLengthOfStay!.Value)
                    .DefaultIfEmpty(0)
                    .Max();
                if (minLos > 0 && nights < minLos)
                    return null;

                var maxLos = stayRows.Where(x => x.MaxLengthOfStay.HasValue)
                    .Select(x => x.MaxLengthOfStay!.Value)
                    .DefaultIfEmpty(0)
                    .Where(x => x > 0)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
                if (maxLos != int.MaxValue && nights > maxLos)
                    return null;

                var availableUnits = stayRows.Min(x => x.QuantityTotal - x.QuantityReserved);
                if (availableUnits <= 0)
                    return null;

                return new HotelRoomOptionDto
                {
                    RoomTypeId = option.RoomTypeId,
                    RoomType = option.RoomType,
                    MaxGuests = option.Representative.MaxGuests,
                    BasePricePerNight = option.Representative.BasePricePerNight,
                    AvailableUnits = availableUnits,
                    BedType = option.Representative.BedType,
                    AreaSquareMeters = option.Representative.AreaSquareMeters,
                    ShortDescription = option.Representative.ShortDescription,
                    ImageUrl = option.Representative.ImageUrl
                };
            })
            .Where(x => x != null)
            .Cast<HotelRoomOptionDto>()
            .OrderBy(x => x.BasePricePerNight)
            .ThenBy(x => x.RoomType)
            .ToList();
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