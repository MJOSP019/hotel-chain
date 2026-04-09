using HotelChain.Infrastructure.Data;
using HotelChain.Infrastructure.Auth;
using HotelChain.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/integration")]
[Authorize(Roles = Roles.WEBSERVICE)]
public class IntegrationController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public IntegrationController(HotelChainDbContext db)
    {
        _db = db;
    }

    public class IntegrationSearchRequest
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

    public class IntegrationCreateReservationRequest
    {
        public int RoomId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities()
    {
        var cities = await _db.Cities
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name
            })
            .ToListAsync();

        return Ok(cities);
    }

    [HttpGet("hotels")]
    public async Task<IActionResult> GetHotels([FromQuery] int cityId)
    {
        var hotels = await _db.Hotels
            .Where(h => h.IsActive && h.CityId == cityId)
            .OrderBy(h => h.Name)
            .Select(h => new
            {
                h.Id,
                h.Code,
                h.Name,
                h.Address,
                h.Description,
                h.CityId
            })
            .ToListAsync();

        return Ok(hotels);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] IntegrationSearchRequest req)
    {
        var start = req.CheckIn.Date;
        var end = req.CheckOut.Date;

        if (end <= start)
            return BadRequest("CheckOut debe ser mayor que CheckIn.");

        var searchAudit = new SearchAudit
        {
            CityId = req.CityId,
            UserId = null,
            CheckIn = start,
            CheckOut = end,
            Guests = req.Guests,
            MinPrice = req.MinPrice,
            MaxPrice = req.MaxPrice,
            RoomTypeId = req.RoomTypeId,
            MinRating = req.MinRating,
            Source = "INTEGRATION",
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
                Hotel = r.Hotel.Name,
                r.NameOrNumber,
                RoomType = r.RoomType.Name,
                r.RoomTypeId,
                r.MaxGuests,
                r.BasePricePerNight
            })
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpPost("reservations")]
    public async Task<IActionResult> CreateReservation([FromBody] IntegrationCreateReservationRequest req)
    {
        var start = req.CheckIn.Date;
        var end = req.CheckOut.Date;

        if (end <= start)
            return BadRequest("Fechas inválidas.");

        if (req.Guests <= 0)
            return BadRequest("Guests inválido.");

        var room = await _db.Rooms
            .FirstOrDefaultAsync(r => r.Id == req.RoomId && r.IsActive);

        if (room == null)
            return NotFound("Room no existe.");

        if (room.MaxGuests < req.Guests)
            return BadRequest("Excede MaxGuests.");

        var nights = (end - start).Days;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var inv = await _db.RoomInventories
            .Where(x => x.RoomId == room.Id && x.Date >= start && x.Date < end)
            .ToListAsync();

        if (inv.Count != nights)
            return BadRequest("No hay inventario completo para esas fechas.");

        if (inv.Any(x => (x.QuantityTotal - x.QuantityReserved) <= 0))
            return BadRequest("No hay disponibilidad.");

        foreach (var row in inv)
            row.QuantityReserved += 1;

        var total = room.BasePricePerNight * nights;
        var code = $"R-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var reservation = new Reservation
        {
            Code = code,
            HotelId = room.HotelId,
            UserId = null,
            CheckIn = start,
            CheckOut = end,
            Guests = req.Guests,
            TotalAmount = total,
            Status = "PENDING",
            Rooms = new List<ReservationRoom>
            {
                new()
                {
                    RoomId = room.Id,
                    PricePerNight = room.BasePricePerNight,
                    Nights = nights,
                    Subtotal = total
                }
            }
        };

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            reservation.Id,
            reservation.Code,
            reservation.Status,
            reservation.TotalAmount
        });
    }

    [HttpGet("reservations/{code}")]
    public async Task<IActionResult> GetReservationByCode(string code)
    {
        var res = await _db.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.Room)
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null)
            return NotFound("Reserva no existe.");

        return Ok(new
        {
            res.Id,
            res.Code,
            Hotel = res.Hotel.Name,
            res.CheckIn,
            res.CheckOut,
            res.Guests,
            res.TotalAmount,
            res.Status,
            IsPaid = res.Payment != null,
            PaymentStatus = res.Payment?.Status,
            CardLast4 = res.Payment != null ? $"**** {res.Payment.Last4}" : null,
            Rooms = res.Rooms.Select(x => new
            {
                x.RoomId,
                x.Room.NameOrNumber,
                x.PricePerNight,
                x.Nights,
                x.Subtotal
            })
        });
    }

    [HttpPost("reservations/{code}/cancel")]
    public async Task<IActionResult> CancelReservation(string code)
    {
        var res = await _db.Reservations
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status == "PENDING" || res.Status == "CONFIRMED")
        {
            // ok
        }
        else if (res.Status == "CANCELED")
        {
            return Ok(new { res.Id, res.Code, res.Status });
        }
        else
        {
            return BadRequest("No se puede cancelar en este estado.");
        }

        var cancelDeadlineUtc = res.CheckIn.AddHours(-24);
        if (DateTime.UtcNow > cancelDeadlineUtc)
            return BadRequest("No se puede cancelar: faltan menos de 24 horas para el check-in.");

        var oldStatus = res.Status;

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var rr in res.Rooms)
        {
            var invRows = await _db.RoomInventories
                .Where(x => x.RoomId == rr.RoomId && x.Date >= res.CheckIn && x.Date < res.CheckOut)
                .ToListAsync();

            var expectedNights = (res.CheckOut.Date - res.CheckIn.Date).Days;
            if (invRows.Count != expectedNights)
                return StatusCode(500, "Inventario incompleto para liberar la reserva.");

            foreach (var row in invRows)
            {
                if (row.QuantityReserved <= 0)
                    return StatusCode(500, "Inventario inconsistente (QuantityReserved <= 0).");

                row.QuantityReserved -= 1;
            }
        }

        res.Status = "CANCELED";

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "CANCEL",
            OldStatus = oldStatus,
            NewStatus = "CANCELED",
            Reason = "Cancelación desde integración empresarial",
            Actor = "WEBSERVICE",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            res.Id,
            res.Code,
            res.Status
        });
    }
}