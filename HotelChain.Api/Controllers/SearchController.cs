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

    public class SearchResultDto
    {
        public int HotelId { get; set; }
        public string Hotel { get; set; } = "";
        public int RoomTypeId { get; set; }
        public string RoomType { get; set; } = "";
        public int MaxGuests { get; set; }
        public decimal BasePricePerNight { get; set; }
        public int AvailableUnits { get; set; }
        public string? BedType { get; set; }
        public decimal? AreaSquareMeters { get; set; }
        public string? ShortDescription { get; set; }
        public string? ImageUrl { get; set; }
        public decimal AverageRating { get; set; }
        public int ReviewsCount { get; set; }
    }

    public class SearchRestrictionMessageDto
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public int AffectedOptions { get; set; }
    }

    public class SearchResponseDto
    {
        public List<SearchResultDto> Results { get; set; } = new();
        public List<SearchRestrictionMessageDto> RestrictionMessages { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequest req)
    {
        var start = req.CheckIn.Date;
        var end = req.CheckOut.Date;

        if (end <= start)
            return BadRequest("CheckOut debe ser mayor que CheckIn.");

        if (req.Guests <= 0)
            return BadRequest("Guests inválido.");

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = null;

        if (!string.IsNullOrWhiteSpace(userIdValue) && Guid.TryParse(userIdValue, out var parsedUserId))
            userId = parsedUserId;

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

        var nights = (end - start).Days;

        var roomTypeOptions = await _db.Rooms
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Where(r => r.Hotel.IsActive)
            .Where(r => r.Hotel.CityId == req.CityId)
            .Where(r => r.MaxGuests >= req.Guests)
            .Where(r => !req.MinPrice.HasValue || r.BasePricePerNight >= req.MinPrice.Value)
            .Where(r => !req.MaxPrice.HasValue || r.BasePricePerNight <= req.MaxPrice.Value)
            .Where(r => !req.RoomTypeId.HasValue || req.RoomTypeId.Value <= 0 || r.RoomTypeId == req.RoomTypeId.Value)
            .Where(r => !req.MinRating.HasValue ||
                (r.Hotel.Reviews.Any() && r.Hotel.Reviews.Average(rv => rv.Rating) >= req.MinRating.Value))
            .GroupBy(r => new
            {
                r.HotelId,
                Hotel = r.Hotel.Name,
                r.RoomTypeId,
                RoomType = r.RoomType.Name,
                ReviewsCount = r.Hotel.Reviews.Count(),
                AverageRating = r.Hotel.Reviews.Any()
                    ? Math.Round(r.Hotel.Reviews.Average(rv => (decimal)rv.Rating), 2)
                    : 0
            })
            .Select(g => new
            {
                g.Key.HotelId,
                g.Key.Hotel,
                g.Key.RoomTypeId,
                g.Key.RoomType,
                g.Key.ReviewsCount,
                g.Key.AverageRating,
                Representative = g
                    .OrderBy(x => x.BasePricePerNight)
                    .ThenByDescending(x => x.MaxGuests)
                    .Select(x => new
                    {
                        x.MaxGuests,
                        x.BasePricePerNight,
                        x.BedType,
                        x.AreaSquareMeters,
                        x.ShortDescription,
                        x.ImageUrl
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        if (roomTypeOptions.Count == 0)
        {
            return Ok(new SearchResponseDto
            {
                Results = new(),
                RestrictionMessages = new()
            });
        }

        var hotelIds = roomTypeOptions.Select(x => x.HotelId).Distinct().ToList();
        var roomTypeIds = roomTypeOptions.Select(x => x.RoomTypeId).Distinct().ToList();

        var inventoryRows = await _db.RoomTypeInventories
            .AsNoTracking()
            .Where(inv => inv.Date >= start && inv.Date <= end)
            .Where(inv => hotelIds.Contains(inv.HotelId))
            .Where(inv => roomTypeIds.Contains(inv.RoomTypeId))
            .ToListAsync();

        var inventoryByKey = inventoryRows
            .GroupBy(x => (x.HotelId, x.RoomTypeId))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Date).ToList());

        var restrictionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var results = new List<SearchResultDto>();

        foreach (var option in roomTypeOptions)
        {
            if (!inventoryByKey.TryGetValue((option.HotelId, option.RoomTypeId), out var rows))
            {
                IncrementRestriction(restrictionCounts, "NO_INVENTORY");
                continue;
            }

            var stayRows = rows
                .Where(x => x.Date >= start && x.Date < end)
                .OrderBy(x => x.Date)
                .ToList();

            if (stayRows.Count != nights)
            {
                IncrementRestriction(restrictionCounts, "NO_INVENTORY");
                continue;
            }

            if (stayRows.Any(x => x.IsClosed))
            {
                IncrementRestriction(restrictionCounts, "IS_CLOSED");
                continue;
            }

            var arrivalRow = stayRows.FirstOrDefault(x => x.Date == start);
            if (arrivalRow?.ClosedToArrival == true)
            {
                IncrementRestriction(restrictionCounts, "CLOSED_TO_ARRIVAL");
                continue;
            }

            var departureRow = rows.FirstOrDefault(x => x.Date == end);
            if (departureRow?.ClosedToDeparture == true)
            {
                IncrementRestriction(restrictionCounts, "CLOSED_TO_DEPARTURE");
                continue;
            }

            var minLos = stayRows
                .Where(x => x.MinLengthOfStay.HasValue)
                .Select(x => x.MinLengthOfStay!.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (minLos > 0 && nights < minLos)
            {
                IncrementRestriction(restrictionCounts, "MIN_LENGTH_OF_STAY");
                continue;
            }

            var maxLos = stayRows
                .Where(x => x.MaxLengthOfStay.HasValue)
                .Select(x => x.MaxLengthOfStay!.Value)
                .DefaultIfEmpty(0)
                .Where(x => x > 0)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            if (maxLos != int.MaxValue && nights > maxLos)
            {
                IncrementRestriction(restrictionCounts, "MAX_LENGTH_OF_STAY");
                continue;
            }

            var minAvailable = stayRows.Min(x => x.QuantityTotal - x.QuantityReserved);
            if (minAvailable <= 0)
            {
                IncrementRestriction(restrictionCounts, "NO_AVAILABILITY");
                continue;
            }

            var rep = option.Representative;
            if (rep == null)
            {
                IncrementRestriction(restrictionCounts, "NO_INVENTORY");
                continue;
            }

            results.Add(new SearchResultDto
            {
                HotelId = option.HotelId,
                Hotel = option.Hotel,
                RoomTypeId = option.RoomTypeId,
                RoomType = option.RoomType,
                MaxGuests = rep.MaxGuests,
                BasePricePerNight = rep.BasePricePerNight,
                AvailableUnits = minAvailable,
                BedType = rep.BedType,
                AreaSquareMeters = rep.AreaSquareMeters,
                ShortDescription = rep.ShortDescription,
                ImageUrl = rep.ImageUrl,
                AverageRating = option.AverageRating,
                ReviewsCount = option.ReviewsCount
            });
        }

        results = results
            .OrderBy(x => x.BasePricePerNight)
            .ThenByDescending(x => x.AverageRating)
            .ToList();

        var restrictionMessages = BuildRestrictionMessages(restrictionCounts);

        return Ok(new SearchResponseDto
        {
            Results = results,
            RestrictionMessages = restrictionMessages
        });
    }

    private static void IncrementRestriction(IDictionary<string, int> counts, string code)
    {
        counts[code] = counts.TryGetValue(code, out var current) ? current + 1 : 1;
    }

    private static List<SearchRestrictionMessageDto> BuildRestrictionMessages(Dictionary<string, int> counts)
    {
        return counts
            .OrderByDescending(x => x.Value)
            .Select(x => CreateRestrictionMessage(x.Key, x.Value))
            .Where(x => x is not null)
            .Cast<SearchRestrictionMessageDto>()
            .ToList();
    }

    private static SearchRestrictionMessageDto? CreateRestrictionMessage(string code, int affectedOptions)
    {
        return code switch
        {
            "IS_CLOSED" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Venta cerrada",
                Message = "Algunas opciones no se muestran porque la venta está cerrada para una o más noches de la estancia seleccionada.",
                AffectedOptions = affectedOptions
            },
            "CLOSED_TO_ARRIVAL" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Llegada cerrada",
                Message = "Hay opciones ocultas porque no permiten check-in en la fecha de llegada seleccionada.",
                AffectedOptions = affectedOptions
            },
            "CLOSED_TO_DEPARTURE" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Salida cerrada",
                Message = "Hay opciones ocultas porque no permiten check-out en la fecha de salida seleccionada.",
                AffectedOptions = affectedOptions
            },
            "MIN_LENGTH_OF_STAY" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Estadía mínima",
                Message = "Algunas tarifas requieren una estadía mínima mayor a la seleccionada.",
                AffectedOptions = affectedOptions
            },
            "MAX_LENGTH_OF_STAY" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Estadía máxima",
                Message = "Algunas tarifas tienen una estadía máxima menor a la seleccionada.",
                AffectedOptions = affectedOptions
            },
            "NO_AVAILABILITY" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Sin disponibilidad",
                Message = "Existen opciones que no se muestran porque ya no tienen cupo comercial para todas las noches de la estancia.",
                AffectedOptions = affectedOptions
            },
            "NO_INVENTORY" => new SearchRestrictionMessageDto
            {
                Code = code,
                Title = "Inventario incompleto",
                Message = "Algunas opciones no pudieron mostrarse porque el inventario comercial no está abierto para todas las fechas consultadas.",
                AffectedOptions = affectedOptions
            },
            _ => null
        };
    }
}
