using System.Text;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminAnalyticsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public AdminAnalyticsController(HotelChainDbContext db)
    {
        _db = db;
    }

    [HttpGet("searches")]
    public async Task<IActionResult> GetSearches(
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? userId,
        [FromQuery] int? guests,
        [FromQuery] int? roomTypeId,
        [FromQuery] double? minRating,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 25;

        if (pageSize > 100)
            pageSize = 100;

        var query = BuildSearchesQuery(
            source,
            cityId,
            from,
            to,
            userId,
            guests,
            roomTypeId,
            minRating,
            minPrice,
            maxPrice);

        var totalItems = await query.CountAsync();
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        var searches = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.CityId,
                City = x.City.Name,
                x.UserId,
                x.CheckIn,
                x.CheckOut,
                x.Guests,
                x.MinPrice,
                x.MaxPrice,
                x.RoomTypeId,
                RoomType = x.RoomTypeId.HasValue
                    ? _db.RoomTypes
                        .Where(rt => rt.Id == x.RoomTypeId.Value)
                        .Select(rt => rt.Name)
                        .FirstOrDefault()
                    : null,
                x.MinRating,
                x.Source,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages,
            items = searches
        });
    }
    [HttpGet("searches/dashboard")]
public async Task<IActionResult> GetSearchesDashboard(
    [FromQuery] string? source,
    [FromQuery] int? cityId,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] Guid? userId,
    [FromQuery] int? guests,
    [FromQuery] int? roomTypeId,
    [FromQuery] double? minRating,
    [FromQuery] decimal? minPrice,
    [FromQuery] decimal? maxPrice)
{
    var query = BuildSearchesQuery(
        source,
        cityId,
        from,
        to,
        userId,
        guests,
        roomTypeId,
        minRating,
        minPrice,
        maxPrice);

    var totalSearches = await query.CountAsync();

    var bySource = await query
        .GroupBy(x => string.IsNullOrWhiteSpace(x.Source) ? "UNKNOWN" : x.Source)
        .Select(g => new
        {
            Label = g.Key,
            Count = g.Count()
        })
        .OrderByDescending(x => x.Count)
        .ToListAsync();

    var topCities = await query
        .GroupBy(x => new { x.CityId, CityName = x.City.Name })
        .Select(g => new
        {
            CityId = g.Key.CityId,
            Label = g.Key.CityName,
            Count = g.Count()
        })
        .OrderByDescending(x => x.Count)
        .Take(5)
        .ToListAsync();

    var byDay = await query
        .GroupBy(x => x.CreatedAt.Date)
        .Select(g => new
        {
            Date = g.Key,
            Count = g.Count()
        })
        .OrderBy(x => x.Date)
        .ToListAsync();

    return Ok(new
    {
        totalSearches,
        bySource,
        topCities,
        byDay
    });
}

    [HttpGet("searches/export")]
    public async Task<IActionResult> ExportSearches(
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? userId,
        [FromQuery] int? guests,
        [FromQuery] int? roomTypeId,
        [FromQuery] double? minRating,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice)
    {
        var query = BuildSearchesQuery(
            source,
            cityId,
            from,
            to,
            userId,
            guests,
            roomTypeId,
            minRating,
            minPrice,
            maxPrice);

        var searches = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.CityId,
                City = x.City.Name,
                x.UserId,
                x.CheckIn,
                x.CheckOut,
                x.Guests,
                x.MinPrice,
                x.MaxPrice,
                x.RoomTypeId,
                RoomType = x.RoomTypeId.HasValue
                    ? _db.RoomTypes
                        .Where(rt => rt.Id == x.RoomTypeId.Value)
                        .Select(rt => rt.Name)
                        .FirstOrDefault()
                    : null,
                x.MinRating,
                x.Source,
                x.CreatedAt
            })
            .ToListAsync();

        var sb = new StringBuilder();

        sb.AppendLine("Id,Fecha,Origen,Ciudad,CheckIn,CheckOut,Huespedes,MinPrice,MaxPrice,RoomType,MinRating,UserId");

        foreach (var item in searches)
        {
            sb.Append(EscapeCsv(item.Id.ToString())).Append(",");
            sb.Append(EscapeCsv(item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))).Append(",");
            sb.Append(EscapeCsv(item.Source)).Append(",");
            sb.Append(EscapeCsv(item.City)).Append(",");
            sb.Append(EscapeCsv(item.CheckIn.ToString("yyyy-MM-dd"))).Append(",");
            sb.Append(EscapeCsv(item.CheckOut.ToString("yyyy-MM-dd"))).Append(",");
            sb.Append(EscapeCsv(item.Guests.ToString())).Append(",");
            sb.Append(EscapeCsv(item.MinPrice?.ToString() ?? "")).Append(",");
            sb.Append(EscapeCsv(item.MaxPrice?.ToString() ?? "")).Append(",");
            sb.Append(EscapeCsv(item.RoomType ?? "")).Append(",");
            sb.Append(EscapeCsv(item.MinRating?.ToString() ?? "")).Append(",");
            sb.Append(EscapeCsv(item.UserId?.ToString() ?? ""));
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"searches-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        return File(bytes, "text/csv", fileName);
    }

    private IQueryable<HotelChain.Domain.Entities.SearchAudit> BuildSearchesQuery(
        string? source,
        int? cityId,
        DateTime? from,
        DateTime? to,
        Guid? userId,
        int? guests,
        int? roomTypeId,
        double? minRating,
        decimal? minPrice,
        decimal? maxPrice)
    {
        var query = _db.SearchAudits
            .AsNoTracking()
            .Include(x => x.City)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(source))
        {
            var normalizedSource = source.Trim().ToUpper();
            query = query.Where(x => x.Source == normalizedSource);
        }

        if (cityId.HasValue && cityId.Value > 0)
        {
            query = query.Where(x => x.CityId == cityId.Value);
        }

        if (from.HasValue)
        {
            var fromDate = from.Value.Date;
            query = query.Where(x => x.CreatedAt >= fromDate);
        }

        if (to.HasValue)
        {
            var toDateExclusive = to.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < toDateExclusive);
        }

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        if (guests.HasValue && guests.Value > 0)
        {
            query = query.Where(x => x.Guests == guests.Value);
        }

        if (roomTypeId.HasValue && roomTypeId.Value > 0)
        {
            query = query.Where(x => x.RoomTypeId == roomTypeId.Value);
        }

        if (minRating.HasValue)
        {
            query = query.Where(x => x.MinRating.HasValue && x.MinRating.Value >= minRating.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(x => x.MinPrice.HasValue && x.MinPrice.Value >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(x => x.MaxPrice.HasValue && x.MaxPrice.Value <= maxPrice.Value);
        }

        return query;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');

        if (!needsQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}