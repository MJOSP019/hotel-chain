using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using HotelChain.Domain.Entities;
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
    private readonly IConfiguration _configuration;

    public AdminAnalyticsController(HotelChainDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("searches")]
    public async Task<IActionResult> GetSearches(
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
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
            checkInFrom,
            checkInTo,
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

        var pageAudits = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rows = await BuildSearchRowsAsync(pageAudits);

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages,
            items = rows
        });
    }

    [HttpGet("searches/dashboard")]
    public async Task<IActionResult> GetSearchesDashboard(
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
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
            checkInFrom,
            checkInTo,
            userId,
            guests,
            roomTypeId,
            minRating,
            minPrice,
            maxPrice);

        var audits = await query.ToListAsync();
        var totalSearches = audits.Count;

        var roomTypeNames = await LoadRoomTypeNamesAsync(
            audits
                .Select(x => x.RoomTypeId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value));

        var bySource = audits
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Source) ? "UNKNOWN" : x.Source.Trim().ToUpperInvariant())
            .Select(g => new DashboardCountItem(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var topCities = audits
            .GroupBy(x => new { x.CityId, CityName = x.City.Name })
            .Select(g => new DashboardCountItem(g.Key.CityName, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        var bySearchDay = audits
            .GroupBy(x => x.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DashboardDateItem(g.Key, g.Count()))
            .ToList();

        var byCheckInDate = audits
            .GroupBy(x => x.CheckIn.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DashboardDateItem(g.Key, g.Count()))
            .Take(14)
            .ToList();

        var byRoomType = audits
            .GroupBy(x => x.RoomTypeId.HasValue && roomTypeNames.TryGetValue(x.RoomTypeId.Value, out var name)
                ? name
                : "Sin tipo")
            .Select(g => new DashboardCountItem(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var byGuests = audits
            .GroupBy(x => x.Guests)
            .OrderBy(g => g.Key)
            .Select(g => new DashboardCountItem($"{g.Key} huésped{(g.Key == 1 ? "" : "es")}", g.Count()))
            .ToList();

        var byLengthOfStay = audits
            .GroupBy(x => GetNightsBucket(GetNights(x.CheckIn, x.CheckOut)))
            .Select(g => new DashboardCountItem(g.Key, g.Count()))
            .OrderBy(x => GetLengthOfStayOrder(x.Label))
            .ToList();

        var leadTimeBuckets = audits
            .GroupBy(x => GetLeadTimeBucket(GetLeadTimeDays(x.CreatedAt, x.CheckIn)))
            .Select(g => new DashboardCountItem(g.Key, g.Count()))
            .OrderBy(x => GetLeadTimeOrder(x.Label))
            .ToList();

        var priceDemand = audits
            .GroupBy(GetPriceBucket)
            .Select(g => new DashboardCountItem(g.Key, g.Count()))
            .OrderBy(x => GetPriceBucketOrder(x.Label))
            .ToList();

        var averageGuests = totalSearches == 0
            ? 0m
            : Math.Round((decimal)audits.Average(x => x.Guests), 1);

        var averageNights = totalSearches == 0
            ? 0m
            : Math.Round((decimal)audits.Average(x => GetNights(x.CheckIn, x.CheckOut)), 1);

        var averageLeadTimeDays = totalSearches == 0
            ? 0m
            : Math.Round((decimal)audits.Average(x => GetLeadTimeDays(x.CreatedAt, x.CheckIn)), 1);

        var topCity = topCities.FirstOrDefault()?.Label ?? "-";
        var topRoomType = byRoomType.FirstOrDefault()?.Label ?? "-";

        var reservationsQuery = BuildReservationsForConversionQuery(
            cityId,
            from,
            to,
            checkInFrom,
            checkInTo,
            guests,
            roomTypeId);

        var estimatedReservations = await reservationsQuery.CountAsync();
        var estimatedConversionRate = totalSearches == 0
            ? 0m
            : Math.Round(estimatedReservations * 100m / totalSearches, 1);

        var reservationsByCity = await reservationsQuery
            .GroupBy(x => new { x.Hotel.CityId, CityName = x.Hotel.City.Name })
            .Select(g => new
            {
                g.Key.CityId,
                g.Key.CityName,
                Count = g.Count()
            })
            .ToListAsync();

        var reservationCityMap = reservationsByCity.ToDictionary(x => x.CityId, x => x.Count);

        var conversionByCity = audits
            .GroupBy(x => new { x.CityId, CityName = x.City.Name })
            .Select(g =>
            {
                var searches = g.Count();
                var reservations = reservationCityMap.TryGetValue(g.Key.CityId, out var value) ? value : 0;
                var rate = searches == 0 ? 0m : Math.Round(reservations * 100m / searches, 1);

                return new DashboardConversionItem(g.Key.CityName, searches, reservations, rate);
            })
            .OrderByDescending(x => x.Searches)
            .Take(8)
            .ToList();

        return Ok(new
        {
            totalSearches,
            webSearches = bySource.FirstOrDefault(x => x.Label == "WEB")?.Count ?? 0,
            integrationSearches = bySource.FirstOrDefault(x => x.Label == "INTEGRATION")?.Count ?? 0,
            distinctCities = audits.Select(x => x.CityId).Distinct().Count(),
            averageGuests,
            averageNights,
            averageLeadTimeDays,
            topCity,
            topRoomType,
            estimatedReservations,
            estimatedConversionRate,
            bySource,
            topCities,
            bySearchDay,
            byCheckInDate,
            byRoomType,
            byGuests,
            byLengthOfStay,
            leadTimeBuckets,
            priceDemand,
            conversionByCity
        });
    }

    [HttpGet("searches/export")]
    public async Task<IActionResult> ExportSearches(
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
        [FromQuery] Guid? userId,
        [FromQuery] int? guests,
        [FromQuery] int? roomTypeId,
        [FromQuery] double? minRating,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice)
    {
        var rows = await GetFilteredSearchRowsAsync(
            source,
            cityId,
            from,
            to,
            checkInFrom,
            checkInTo,
            userId,
            guests,
            roomTypeId,
            minRating,
            minPrice,
            maxPrice);

        var bytes = BuildSearchesCsv(rows);
        var fileName = $"searches-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        return File(bytes, "text/csv", fileName);
    }

    [HttpPost("searches/export-email")]
    public async Task<IActionResult> ExportSearchesByEmail(
        [FromBody] EmailExportRequest request,
        [FromQuery] string? source,
        [FromQuery] int? cityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
        [FromQuery] Guid? userId,
        [FromQuery] int? guests,
        [FromQuery] int? roomTypeId,
        [FromQuery] double? minRating,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Debe ingresar un correo destino." });

        MailAddress recipient;
        try
        {
            recipient = new MailAddress(request.Email.Trim());
        }
        catch
        {
            return BadRequest(new { message = "El correo destino no tiene un formato válido." });
        }

        var rows = await GetFilteredSearchRowsAsync(
            source,
            cityId,
            from,
            to,
            checkInFrom,
            checkInTo,
            userId,
            guests,
            roomTypeId,
            minRating,
            minPrice,
            maxPrice);

        var bytes = BuildSearchesCsv(rows);
        var fileName = $"searches-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

        try
        {
            await SendCsvEmailAsync(recipient, bytes, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"No se pudo enviar el correo: {ex.Message}" });
        }

        return Ok(new { message = $"Reporte enviado a {recipient.Address}." });
    }

    private async Task<List<SearchRow>> GetFilteredSearchRowsAsync(
        string? source,
        int? cityId,
        DateTime? from,
        DateTime? to,
        DateTime? checkInFrom,
        DateTime? checkInTo,
        Guid? userId,
        int? guests,
        int? roomTypeId,
        double? minRating,
        decimal? minPrice,
        decimal? maxPrice)
    {
        var query = BuildSearchesQuery(
            source,
            cityId,
            from,
            to,
            checkInFrom,
            checkInTo,
            userId,
            guests,
            roomTypeId,
            minRating,
            minPrice,
            maxPrice);

        var audits = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return await BuildSearchRowsAsync(audits);
    }

    private IQueryable<SearchAudit> BuildSearchesQuery(
        string? source,
        int? cityId,
        DateTime? from,
        DateTime? to,
        DateTime? checkInFrom,
        DateTime? checkInTo,
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
            var normalizedSource = source.Trim().ToUpperInvariant();
            query = query.Where(x => x.Source == normalizedSource);
        }

        if (cityId.HasValue && cityId.Value > 0)
            query = query.Where(x => x.CityId == cityId.Value);

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

        if (checkInFrom.HasValue)
        {
            var checkInFromDate = checkInFrom.Value.Date;
            query = query.Where(x => x.CheckIn >= checkInFromDate);
        }

        if (checkInTo.HasValue)
        {
            var checkInToExclusive = checkInTo.Value.Date.AddDays(1);
            query = query.Where(x => x.CheckIn < checkInToExclusive);
        }

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (guests.HasValue && guests.Value > 0)
            query = query.Where(x => x.Guests == guests.Value);

        if (roomTypeId.HasValue && roomTypeId.Value > 0)
            query = query.Where(x => x.RoomTypeId == roomTypeId.Value);

        if (minRating.HasValue)
            query = query.Where(x => x.MinRating.HasValue && x.MinRating.Value >= minRating.Value);

        if (minPrice.HasValue)
            query = query.Where(x => x.MinPrice.HasValue && x.MinPrice.Value >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(x => x.MaxPrice.HasValue && x.MaxPrice.Value <= maxPrice.Value);

        return query;
    }

    private IQueryable<Reservation> BuildReservationsForConversionQuery(
        int? cityId,
        DateTime? from,
        DateTime? to,
        DateTime? checkInFrom,
        DateTime? checkInTo,
        int? guests,
        int? roomTypeId)
    {
        var query = _db.Reservations
            .AsNoTracking()
            .Include(x => x.Hotel)
            .ThenInclude(x => x.City)
            .Include(x => x.Rooms)
            .Where(x => x.Status != "CANCELED" && x.Status != "EXPIRED")
            .AsQueryable();

        if (cityId.HasValue && cityId.Value > 0)
            query = query.Where(x => x.Hotel.CityId == cityId.Value);

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

        if (checkInFrom.HasValue)
        {
            var checkInFromDate = checkInFrom.Value.Date;
            query = query.Where(x => x.CheckIn >= checkInFromDate);
        }

        if (checkInTo.HasValue)
        {
            var checkInToExclusive = checkInTo.Value.Date.AddDays(1);
            query = query.Where(x => x.CheckIn < checkInToExclusive);
        }

        if (guests.HasValue && guests.Value > 0)
            query = query.Where(x => x.Guests == guests.Value);

        if (roomTypeId.HasValue && roomTypeId.Value > 0)
            query = query.Where(x => x.Rooms.Any(rr => rr.RoomTypeId == roomTypeId.Value));

        return query;
    }

    private async Task<List<SearchRow>> BuildSearchRowsAsync(List<SearchAudit> audits)
    {
        var roomTypeNames = await LoadRoomTypeNamesAsync(
            audits
                .Select(x => x.RoomTypeId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value));

        var userIds = audits
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId!.Value)
            .Distinct()
            .ToList();

        var users = userIds.Count == 0
            ? new Dictionary<Guid, UserLookup>()
            : await _db.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .Select(x => new UserLookup
                {
                    Id = x.Id,
                    Name = ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(),
                    Email = x.Email ?? "",
                    Country = x.Country ?? "",
                    PassportNumber = x.PassportNumber ?? ""
                })
                .ToDictionaryAsync(x => x.Id);

        return audits.Select(x =>
        {
            users.TryGetValue(x.UserId ?? Guid.Empty, out var user);

            var roomType = x.RoomTypeId.HasValue && roomTypeNames.TryGetValue(x.RoomTypeId.Value, out var typeName)
                ? typeName
                : null;

            return new SearchRow
            {
                Id = x.Id,
                CityId = x.CityId,
                City = x.City.Name,
                UserId = x.UserId,
                UserDisplay = string.IsNullOrWhiteSpace(user?.Name)
                    ? (x.UserId.HasValue ? "Usuario registrado" : "Anónimo / WebService")
                    : user!.Name,
                UserEmail = user?.Email ?? "",
                UserCountry = user?.Country ?? "",
                UserPassportNumber = user?.PassportNumber ?? "",
                CheckIn = x.CheckIn,
                CheckOut = x.CheckOut,
                Nights = GetNights(x.CheckIn, x.CheckOut),
                LeadTimeDays = GetLeadTimeDays(x.CreatedAt, x.CheckIn),
                Guests = x.Guests,
                MinPrice = x.MinPrice,
                MaxPrice = x.MaxPrice,
                PriceRangeLabel = FormatPriceRange(x.MinPrice, x.MaxPrice),
                RoomTypeId = x.RoomTypeId,
                RoomType = roomType,
                MinRating = x.MinRating,
                Source = string.IsNullOrWhiteSpace(x.Source) ? "UNKNOWN" : x.Source.Trim().ToUpperInvariant(),
                CreatedAt = x.CreatedAt
            };
        }).ToList();
    }

    private async Task<Dictionary<int, string>> LoadRoomTypeNamesAsync(IEnumerable<int> roomTypeIds)
    {
        var ids = roomTypeIds.Distinct().ToList();

        if (ids.Count == 0)
            return new Dictionary<int, string>();

        return await _db.RoomTypes
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    private byte[] BuildSearchesCsv(IEnumerable<SearchRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,FechaBusqueda,Origen,Usuario,Email,Ciudad,CheckIn,CheckOut,Noches,AnticipacionDias,Huespedes,MinPrice,MaxPrice,RangoPrecio,TipoHabitacion,RatingMinimo,UserId");

        foreach (var item in rows)
        {
            sb.Append(EscapeCsv(item.Id.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
            sb.Append(EscapeCsv(item.Source)).Append(',');
            sb.Append(EscapeCsv(item.UserDisplay)).Append(',');
            sb.Append(EscapeCsv(item.UserEmail)).Append(',');
            sb.Append(EscapeCsv(item.City)).Append(',');
            sb.Append(EscapeCsv(item.CheckIn.ToString("yyyy-MM-dd"))).Append(',');
            sb.Append(EscapeCsv(item.CheckOut.ToString("yyyy-MM-dd"))).Append(',');
            sb.Append(EscapeCsv(item.Nights.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(item.LeadTimeDays.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(item.Guests.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(item.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? "")).Append(',');
            sb.Append(EscapeCsv(item.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? "")).Append(',');
            sb.Append(EscapeCsv(item.PriceRangeLabel)).Append(',');
            sb.Append(EscapeCsv(item.RoomType ?? "")).Append(',');
            sb.Append(EscapeCsv(item.MinRating?.ToString(CultureInfo.InvariantCulture) ?? "")).Append(',');
            sb.Append(EscapeCsv(item.UserId?.ToString() ?? ""));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task SendCsvEmailAsync(MailAddress recipient, byte[] csvBytes, string fileName)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        var host = emailSettings["Host"];

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("No hay Host configurado en EmailSettings.");

        var port = int.TryParse(emailSettings["Port"], out var configuredPort) ? configuredPort : 587;
        var enableSsl = bool.TryParse(emailSettings["EnableSsl"], out var configuredSsl) && configuredSsl;
        var username = emailSettings["UserName"];
        var password = emailSettings["Password"] ?? emailSettings["AppPassword"];
        var fromEmail = emailSettings["FromEmail"] ?? username;
        var fromName = emailSettings["FromName"] ?? "HotelChain Analytics";

        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("No hay FromEmail o UserName configurado en EmailSettings.");

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Reporte de analytics de búsquedas - HotelChain",
            Body = "Adjunto se incluye el reporte CSV de búsquedas solicitado desde el panel administrativo.",
            IsBodyHtml = false
        };

        message.To.Add(recipient);

        using var stream = new MemoryStream(csvBytes);
        message.Attachments.Add(new Attachment(stream, fileName, "text/csv"));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');

        if (!needsQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static int GetNights(DateTime checkIn, DateTime checkOut)
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        return nights <= 0 ? 1 : nights;
    }

    private static int GetLeadTimeDays(DateTime createdAt, DateTime checkIn)
    {
        var days = (checkIn.Date - createdAt.Date).Days;
        return days < 0 ? 0 : days;
    }

    private static string FormatPriceRange(decimal? minPrice, decimal? maxPrice)
    {
        if (!minPrice.HasValue && !maxPrice.HasValue)
            return "Sin filtro";

        if (minPrice.HasValue && maxPrice.HasValue)
            return $"Q {minPrice.Value:0.00} - Q {maxPrice.Value:0.00}";

        if (minPrice.HasValue)
            return $"Desde Q {minPrice.Value:0.00}";

        return $"Hasta Q {maxPrice!.Value:0.00}";
    }

    private static string GetNightsBucket(int nights)
    {
        return nights switch
        {
            <= 1 => "1 noche",
            <= 2 => "2 noches",
            <= 3 => "3 noches",
            <= 5 => "4-5 noches",
            _ => "6+ noches"
        };
    }

    private static int GetLengthOfStayOrder(string label)
    {
        return label switch
        {
            "1 noche" => 1,
            "2 noches" => 2,
            "3 noches" => 3,
            "4-5 noches" => 4,
            _ => 5
        };
    }

    private static string GetLeadTimeBucket(int leadTimeDays)
    {
        return leadTimeDays switch
        {
            <= 2 => "0-2 días",
            <= 7 => "3-7 días",
            <= 14 => "8-14 días",
            <= 30 => "15-30 días",
            _ => "31+ días"
        };
    }

    private static int GetLeadTimeOrder(string label)
    {
        return label switch
        {
            "0-2 días" => 1,
            "3-7 días" => 2,
            "8-14 días" => 3,
            "15-30 días" => 4,
            _ => 5
        };
    }

    private static string GetPriceBucket(SearchAudit audit)
    {
        decimal? representativePrice = null;

        if (audit.MinPrice.HasValue && audit.MaxPrice.HasValue)
            representativePrice = (audit.MinPrice.Value + audit.MaxPrice.Value) / 2m;
        else if (audit.MinPrice.HasValue)
            representativePrice = audit.MinPrice.Value;
        else if (audit.MaxPrice.HasValue)
            representativePrice = audit.MaxPrice.Value;

        if (!representativePrice.HasValue)
            return "Sin filtro";

        return representativePrice.Value switch
        {
            <= 300m => "≤ Q300",
            <= 600m => "Q301-Q600",
            <= 1000m => "Q601-Q1000",
            _ => "> Q1000"
        };
    }

    private static int GetPriceBucketOrder(string label)
    {
        return label switch
        {
            "Sin filtro" => 0,
            "≤ Q300" => 1,
            "Q301-Q600" => 2,
            "Q601-Q1000" => 3,
            _ => 4
        };
    }

    public class EmailExportRequest
    {
        public string Email { get; set; } = "";
    }

    public class UserLookup
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Country { get; set; } = "";
        public string PassportNumber { get; set; } = "";
    }

    public record DashboardCountItem(string Label, int Count);
    public record DashboardDateItem(DateTime Date, int Count);
    public record DashboardConversionItem(string Label, int Searches, int Reservations, decimal ConversionRate);

    public class SearchRow
    {
        public int Id { get; set; }
        public int CityId { get; set; }
        public string City { get; set; } = "";
        public Guid? UserId { get; set; }
        public string UserDisplay { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string UserCountry { get; set; } = "";
        public string UserPassportNumber { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Nights { get; set; }
        public int LeadTimeDays { get; set; }
        public int Guests { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string PriceRangeLabel { get; set; } = "";
        public int? RoomTypeId { get; set; }
        public string? RoomType { get; set; }
        public double? MinRating { get; set; }
        public string Source { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
