using System.Security.Claims;
using HotelChain.Api.DTOs;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/user/reservations")]
[Authorize]
public class UserReservationsController : ControllerBase
{
    private readonly HotelChainDbContext _context;

    public UserReservationsController(HotelChainDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyReservations()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var expiredReservations = await _context.Reservations
            .Include(r => r.Rooms)
            .Where(r => r.UserId == userId)
            .Where(r => r.Status == "PENDING" && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= DateTime.UtcNow)
            .ToListAsync();

        if (expiredReservations.Count > 0)
        {
            foreach (var reservation in expiredReservations)
                await ExpireReservationAsync(reservation);

            await _context.SaveChangesAsync();
        }

        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Payment)
            .Include(r => r.Charges)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReservationHistoryDto
            {
                HotelId = r.HotelId,
                Code = r.Code,
                Hotel = r.Hotel.Name,
                CheckIn = r.CheckIn,
                CheckOut = r.CheckOut,
                Nights = EF.Functions.DateDiffDay(r.CheckIn, r.CheckOut),
                Guests = r.Guests,
                BaseAmount = r.TotalAmount,
                ChargesTotal = r.Charges.Where(c => !c.IsVoided).Sum(c => (decimal?)c.Amount) ?? 0m,
                GrandTotal = r.TotalAmount + (r.Charges.Where(c => !c.IsVoided).Sum(c => (decimal?)c.Amount) ?? 0m),
                TotalAmount = r.TotalAmount + (r.Charges.Where(c => !c.IsVoided).Sum(c => (decimal?)c.Amount) ?? 0m),
                Status = r.Status,
                IsPaid = r.Payment != null,
                PaymentStatus = r.Payment != null ? r.Payment.Status : null,
                CardLast4 = r.Payment != null ? r.Payment.Last4 : null,
                CreatedAt = r.CreatedAt,
                ExpiresAt = r.ExpiresAt
            })
            .ToListAsync();

        return Ok(reservations);
    }

    private async Task ExpireReservationAsync(Reservation reservation)
    {
        if (reservation.Status != "PENDING" || !reservation.ExpiresAt.HasValue || reservation.ExpiresAt.Value > DateTime.UtcNow)
            return;

        foreach (var room in reservation.Rooms)
        {
            var typeRows = await _context.RoomTypeInventories
                .Where(x => x.HotelId == reservation.HotelId
                         && x.RoomTypeId == room.RoomTypeId
                         && x.Date >= reservation.CheckIn
                         && x.Date < reservation.CheckOut)
                .ToListAsync();

            foreach (var row in typeRows)
            {
                if (row.QuantityReserved > 0)
                    row.QuantityReserved -= 1;
            }

            if (!room.RoomId.HasValue)
                continue;

            var physicalRows = await _context.RoomInventories
                .Where(x => x.RoomId == room.RoomId.Value
                         && x.Date >= reservation.CheckIn
                         && x.Date < reservation.CheckOut)
                .ToListAsync();

            foreach (var row in physicalRows)
            {
                if (row.QuantityReserved > 0)
                    row.QuantityReserved -= 1;
            }

            room.RoomId = null;
        }

        _context.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = reservation.Id,
            Action = "EXPIRE",
            OldStatus = reservation.Status,
            NewStatus = "EXPIRED",
            Reason = "Expiración automática por pago no completado dentro del tiempo límite.",
            Actor = "SYSTEM",
            CreatedAt = DateTime.UtcNow
        });

        reservation.Status = "EXPIRED";
        reservation.ExpiresAt = null;
    }
}
