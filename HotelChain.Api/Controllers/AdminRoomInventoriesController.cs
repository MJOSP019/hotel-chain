using HotelChain.Api.Contracts;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/admin/room-inventories")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminRoomInventoriesController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public AdminRoomInventoriesController(HotelChainDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetByRoom(
        [FromQuery] int roomId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        if (roomId <= 0)
            return BadRequest("roomId es requerido.");

        var roomExists = await _db.Rooms.AnyAsync(r => r.Id == roomId);
        if (!roomExists)
            return BadRequest("La habitación no existe.");

        var query = _db.RoomInventories
            .Include(x => x.Room)
                .ThenInclude(r => r.Hotel)
            .Where(x => x.RoomId == roomId);

        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(x => x.Date >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.Date;
            query = query.Where(x => x.Date <= end);
        }

        var items = await query
            .OrderBy(x => x.Date)
            .Select(x => new AdminRoomInventoryDto
            {
                Id = x.Id,
                RoomId = x.RoomId,
                RoomNameOrNumber = x.Room.NameOrNumber,
                HotelId = x.Room.HotelId,
                HotelName = x.Room.Hotel.Name,
                Date = x.Date,
                QuantityTotal = x.QuantityTotal,
                QuantityReserved = x.QuantityReserved,
                QuantityAvailable = x.QuantityTotal - x.QuantityReserved
            })
            .ToListAsync();

        return Ok(items);
    }


    [HttpGet("commercial-calendar")]
    public async Task<IActionResult> GetCommercialCalendar([FromQuery] int hotelId, [FromQuery] int roomTypeId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        if (hotelId <= 0)
            return BadRequest("hotelId es requerido.");
        if (roomTypeId <= 0)
            return BadRequest("roomTypeId es requerido.");

        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
            return BadRequest("endDate no puede ser menor que startDate.");

        var rows = await _db.RoomTypeInventories
            .Where(x => x.HotelId == hotelId && x.RoomTypeId == roomTypeId && x.Date >= start && x.Date <= end)
            .OrderBy(x => x.Date)
            .Select(x => new CommercialInventoryCalendarDto
            {
                Id = x.Id,
                HotelId = x.HotelId,
                RoomTypeId = x.RoomTypeId,
                Date = x.Date,
                QuantityTotal = x.QuantityTotal,
                QuantityReserved = x.QuantityReserved,
                QuantityAvailable = x.QuantityTotal - x.QuantityReserved,
                IsClosed = x.IsClosed,
                ClosedToArrival = x.ClosedToArrival,
                ClosedToDeparture = x.ClosedToDeparture,
                MinLengthOfStay = x.MinLengthOfStay,
                MaxLengthOfStay = x.MaxLengthOfStay
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("generate-horizon")]
    public async Task<IActionResult> GenerateHorizon([FromBody] GenerateInventoryHorizonRequest req)
    {
        if (req.HotelId <= 0)
            return BadRequest("HotelId es requerido.");

        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == req.HotelId);
        if (!hotelExists)
            return BadRequest("El hotel no existe.");

        var startDate = (req.StartDate ?? DateTime.Today).Date;
        var endDate = req.EndDate?.Date ?? startDate.AddMonths(req.MonthsAhead <= 0 ? 12 : req.MonthsAhead);

        if (endDate < startDate)
            return BadRequest("EndDate no puede ser menor que StartDate.");

        var dates = GetDates(startDate, endDate);
        var activeRooms = await _db.Rooms
            .Where(r => r.HotelId == req.HotelId && r.IsActive)
            .Select(r => new { r.Id, r.HotelId, r.RoomTypeId })
            .ToListAsync();

        if (activeRooms.Count == 0)
            return BadRequest("El hotel no tiene habitaciones activas para generar inventario.");

        var roomIds = activeRooms.Select(r => r.Id).ToList();
        var existingRows = await _db.RoomInventories
            .Where(x => roomIds.Contains(x.RoomId) && x.Date >= startDate && x.Date <= endDate)
            .Select(x => new { x.RoomId, x.Date })
            .ToListAsync();

        var existingKeys = existingRows
            .Select(x => $"{x.RoomId}|{x.Date:yyyyMMdd}")
            .ToHashSet();

        var rowsToInsert = new List<RoomInventory>();
        foreach (var room in activeRooms)
        {
            foreach (var date in dates)
            {
                var key = $"{room.Id}|{date:yyyyMMdd}";
                if (existingKeys.Contains(key))
                    continue;

                rowsToInsert.Add(new RoomInventory
                {
                    RoomId = room.Id,
                    Date = date,
                    QuantityTotal = 1,
                    QuantityReserved = 0
                });
            }
        }

        if (rowsToInsert.Count > 0)
            _db.RoomInventories.AddRange(rowsToInsert);

        await _db.SaveChangesAsync();

        var roomTypeIds = activeRooms.Select(x => x.RoomTypeId).Distinct().ToList();
        foreach (var roomTypeId in roomTypeIds)
            await RebuildCommercialInventoryForRange(req.HotelId, roomTypeId, dates);

        return Ok(new
        {
            message = "Horizonte de inventario generado correctamente.",
            hotelId = req.HotelId,
            startDate,
            endDate,
            roomRowsInserted = rowsToInsert.Count,
            roomTypesSynced = roomTypeIds.Count
        });
    }

    [HttpPost("commercial-rules")]
    public async Task<IActionResult> UpsertCommercialRules([FromBody] UpsertCommercialRulesRequest req)
    {
        if (req.HotelId <= 0)
            return BadRequest("HotelId es requerido.");
        if (req.RoomTypeId <= 0)
            return BadRequest("RoomTypeId es requerido.");

        var startDate = req.StartDate.Date;
        var endDate = req.EndDate.Date;
        if (endDate < startDate)
            return BadRequest("EndDate no puede ser menor que StartDate.");

        if (req.MinLengthOfStay.HasValue && req.MinLengthOfStay <= 0)
            return BadRequest("MinLengthOfStay debe ser mayor que cero.");
        if (req.MaxLengthOfStay.HasValue && req.MaxLengthOfStay <= 0)
            return BadRequest("MaxLengthOfStay debe ser mayor que cero.");
        if (req.MinLengthOfStay.HasValue && req.MaxLengthOfStay.HasValue && req.MinLengthOfStay > req.MaxLengthOfStay)
            return BadRequest("MinLengthOfStay no puede ser mayor que MaxLengthOfStay.");

        var dates = GetDates(startDate, endDate);
        await RebuildCommercialInventoryForRange(req.HotelId, req.RoomTypeId, dates);

        var rows = await _db.RoomTypeInventories
            .Where(x => x.HotelId == req.HotelId && x.RoomTypeId == req.RoomTypeId && x.Date >= startDate && x.Date <= endDate)
            .ToListAsync();

        foreach (var row in rows)
        {
            row.IsClosed = req.IsClosed;
            row.ClosedToArrival = req.ClosedToArrival;
            row.ClosedToDeparture = req.ClosedToDeparture;
            row.MinLengthOfStay = req.MinLengthOfStay;
            row.MaxLengthOfStay = req.MaxLengthOfStay;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Reglas comerciales actualizadas correctamente." });
    }

    [HttpPost("upsert-range")]
    public async Task<IActionResult> UpsertRange([FromBody] UpsertRoomInventoryRangeRequest req)
    {
        if (req.RoomId <= 0)
            return BadRequest("RoomId es requerido.");
        if (req.QuantityTotal < 0)
            return BadRequest("QuantityTotal no puede ser negativo.");
        if (req.QuantityTotal > 1)
            return BadRequest("Para una habitación individual, la cantidad total no puede ser mayor que 1.");

        var startDate = req.StartDate.Date;
        var endDate = req.EndDate.Date;
        if (endDate < startDate)
            return BadRequest("EndDate no puede ser menor que StartDate.");

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == req.RoomId);
        if (room == null)
            return BadRequest("La habitación no existe.");

        var dates = GetDates(startDate, endDate);
        var existingInventories = await _db.RoomInventories
            .Where(x => x.RoomId == req.RoomId && x.Date >= startDate && x.Date <= endDate)
            .ToListAsync();

        foreach (var date in dates)
        {
            var existing = existingInventories.FirstOrDefault(x => x.Date == date);
            if (existing == null)
            {
                _db.RoomInventories.Add(new RoomInventory
                {
                    RoomId = req.RoomId,
                    Date = date,
                    QuantityTotal = req.QuantityTotal,
                    QuantityReserved = 0
                });
                continue;
            }

            if (req.QuantityTotal < existing.QuantityReserved)
            {
                return BadRequest(
                    $"No se puede poner QuantityTotal={req.QuantityTotal} para la fecha {date:yyyy-MM-dd} porque ya hay {existing.QuantityReserved} reservadas.");
            }

            existing.QuantityTotal = req.QuantityTotal;
        }

        await _db.SaveChangesAsync();
        await RebuildCommercialInventoryForRange(room.HotelId, room.RoomTypeId, dates);

        return Ok(new
        {
            message = "Inventario físico actualizado correctamente y sincronizado con el calendario comercial.",
            hotelId = room.HotelId,
            roomTypeId = room.RoomTypeId
        });
    }

    private async Task RebuildCommercialInventoryForRange(int hotelId, int roomTypeId, List<DateTime> dates)
    {
        if (dates.Count == 0)
            return;

        var normalizedDates = dates.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();

        var physicalRows = await _db.RoomInventories
            .Where(ri => normalizedDates.Contains(ri.Date)
                && ri.Room.HotelId == hotelId
                && ri.Room.RoomTypeId == roomTypeId
                && ri.Room.IsActive)
            .GroupBy(ri => ri.Date)
            .Select(g => new
            {
                Date = g.Key,
                QuantityTotal = g.Sum(x => x.QuantityTotal),
                QuantityReserved = g.Sum(x => x.QuantityReserved)
            })
            .ToListAsync();

        var aggregatesByDate = physicalRows.ToDictionary(x => x.Date.Date);

        var existingCommercialRows = await _db.RoomTypeInventories
            .Where(x => x.HotelId == hotelId && x.RoomTypeId == roomTypeId && normalizedDates.Contains(x.Date))
            .ToListAsync();

        var existingByDate = existingCommercialRows.ToDictionary(x => x.Date.Date);

        foreach (var date in normalizedDates)
        {
            aggregatesByDate.TryGetValue(date, out var aggregate);
            var quantityTotal = aggregate?.QuantityTotal ?? 0;
            var quantityReserved = aggregate?.QuantityReserved ?? 0;

            if (existingByDate.TryGetValue(date, out var existing))
            {
                existing.QuantityTotal = quantityTotal;
                existing.QuantityReserved = quantityReserved;
            }
            else
            {
                _db.RoomTypeInventories.Add(new RoomTypeInventory
                {
                    HotelId = hotelId,
                    RoomTypeId = roomTypeId,
                    Date = date,
                    QuantityTotal = quantityTotal,
                    QuantityReserved = quantityReserved,
                    IsClosed = false,
                    ClosedToArrival = false,
                    ClosedToDeparture = false,
                    MinLengthOfStay = null,
                    MaxLengthOfStay = null
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private static List<DateTime> GetDates(DateTime startDate, DateTime endDate)
    {
        var dates = new List<DateTime>();
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            dates.Add(date);
        return dates;
    }

    public class GenerateInventoryHorizonRequest
    {
        public int HotelId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int MonthsAhead { get; set; } = 12;
    }

    public class UpsertCommercialRulesRequest
    {
        public int HotelId { get; set; }
        public int RoomTypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
        public bool ClosedToArrival { get; set; }
        public bool ClosedToDeparture { get; set; }
        public int? MinLengthOfStay { get; set; }
        public int? MaxLengthOfStay { get; set; }
    }

    private sealed class CommercialInventoryCalendarDto
    {
        public int Id { get; set; }
        public int HotelId { get; set; }
        public int RoomTypeId { get; set; }
        public DateTime Date { get; set; }
        public int QuantityTotal { get; set; }
        public int QuantityReserved { get; set; }
        public int QuantityAvailable { get; set; }
        public bool IsClosed { get; set; }
        public bool ClosedToArrival { get; set; }
        public bool ClosedToDeparture { get; set; }
        public int? MinLengthOfStay { get; set; }
        public int? MaxLengthOfStay { get; set; }
    }
}
