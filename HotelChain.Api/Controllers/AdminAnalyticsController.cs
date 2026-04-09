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
        [FromQuery] DateTime? to)
    {
        var query = BuildSearchesQuery(source, cityId, from, to);

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

        return Ok(searches);
    }

    [HttpGet("searches/export")]
    public async Task<IActionResult> ExportSearches(
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = BuildSearchesQuery(source, cityId, from, to);

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
        DateTime? to)
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