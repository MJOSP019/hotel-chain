using System.Net;
using HotelChain.Api.Contracts;
using HotelChain.Api.Services;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

public class AdminCancelReservationRequest
{
    public string Reason { get; set; } = "";
}

public class AdminChangeReservationDatesRequest
{
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public string Reason { get; set; } = "";
}

public class AdminAddReservationChargeRequest
{
    public string Category { get; set; } = "OTHER";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public class AdminSettleReservationChargesRequest
{
    public string CardNumber { get; set; } = "";
    public string Cvv { get; set; } = "";
    public string CardHolderName { get; set; } = "";
    public string BillingAddress { get; set; } = "";
    public decimal Amount { get; set; }
}

[ApiController]
[Route("api/admin/reservations")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminReservationsController : ControllerBase
{
    private readonly HotelChainDbContext _db;
    private readonly IEmailSender _emailSender;

    public AdminReservationsController(
        HotelChainDbContext db,
        IEmailSender emailSender)
    {
        _db = db;
        _emailSender = emailSender;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminReservationDto>>> GetAll()
    {
        await ExpirePendingReservationsAsync();

        var reservations = await _db.Reservations
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AdminReservationDto
            {
                Id = r.Id,
                HotelId = r.HotelId,
                Code = r.Code,
                Hotel = r.Hotel.Name,
                RoomType = r.Rooms.OrderBy(x => x.Id).Select(x => x.RoomType.Name).FirstOrDefault() ?? "-",
                AssignedRoomName = r.Rooms.OrderBy(x => x.Id).Select(x => x.Room != null ? x.Room.NameOrNumber : "Sin asignar").FirstOrDefault() ?? "-",
                CheckIn = r.CheckIn,
                CheckOut = r.CheckOut,
                Nights = EF.Functions.DateDiffDay(r.CheckIn, r.CheckOut),
                Guests = r.Guests,
                BaseAmount = r.TotalAmount,
                ChargesTotal = r.Charges.Where(c => !c.IsVoided).Sum(c => (decimal?)c.Amount) ?? 0m,
                SettledChargesTotal = r.Charges.Where(c => !c.IsVoided && c.IsSettled).Sum(c => (decimal?)c.Amount) ?? 0m,
                OutstandingChargesTotal = r.Charges.Where(c => !c.IsVoided && !c.IsSettled).Sum(c => (decimal?)c.Amount) ?? 0m,
                GrandTotal = r.TotalAmount + (r.Charges.Where(c => !c.IsVoided).Sum(c => (decimal?)c.Amount) ?? 0m),
                IsStayAccountSettled = !(r.Charges.Any(c => !c.IsVoided && !c.IsSettled)),
                ChargeCount = r.Charges.Count(c => !c.IsVoided),
                TotalAmount = r.TotalAmount + (r.Charges.Where(c => !c.IsVoided).Sum(c => (decimal?)c.Amount) ?? 0m),
                Status = r.Status,
                PaymentStatus = r.Payment != null ? r.Payment.Status : "PENDING",
                IsPaid = r.Payment != null && r.Payment.Status == "APPROVED",
                CardLast4 = r.Payment != null ? r.Payment.Last4 : null,
                UserId = r.UserId,
                CreatedAt = r.CreatedAt,
                ExpiresAt = r.ExpiresAt
            })
            .ToListAsync();

        return Ok(reservations);
    }

    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var res = await _db.Reservations
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status == "CHECKED_IN")
            return Ok(new { res.Id, res.Code, res.Status });

        if (res.Status != "CONFIRMED")
            return BadRequest("Solo se puede hacer check-in a reservas CONFIRMED.");

        var today = DateTime.Now.Date;
        if (today < res.CheckIn.Date)
            return BadRequest("El check-in solo puede registrarse desde la fecha de llegada.");

        if (today >= res.CheckOut.Date)
            return BadRequest("No se puede hacer check-in en una reserva ya finalizada.");

        if (res.Rooms.Count == 0)
            return BadRequest("La reserva no tiene habitaciones asociadas.");

        var assignStart = today > res.CheckIn.Date ? today : res.CheckIn.Date;
        var assignNights = (res.CheckOut.Date - assignStart).Days;
        if (assignNights <= 0)
            return BadRequest("No queda estancia pendiente para asignar habitación.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var rr in res.Rooms)
        {
            if (rr.RoomId.HasValue)
                continue;

            var candidateRooms = await _db.Rooms
                .Where(r => r.IsActive)
                .Where(r => r.HotelId == res.HotelId)
                .Where(r => r.RoomTypeId == rr.RoomTypeId)
                .Where(r => r.MaxGuests >= res.Guests)
                .OrderBy(r => r.BasePricePerNight)
                .ThenBy(r => r.Id)
                .Select(r => new { r.Id })
                .ToListAsync();

            if (candidateRooms.Count == 0)
                return BadRequest("No existe una habitación física elegible para el tipo reservado.");

            int? selectedRoomId = null;
            List<RoomInventory>? selectedInventory = null;

            foreach (var room in candidateRooms)
            {
                var inventoryRows = await _db.RoomInventories
                    .Where(x => x.RoomId == room.Id && x.Date >= assignStart && x.Date < res.CheckOut)
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                if (inventoryRows.Count != assignNights)
                    continue;

                if (inventoryRows.Any(x => (x.QuantityTotal - x.QuantityReserved) <= 0))
                    continue;

                selectedRoomId = room.Id;
                selectedInventory = inventoryRows;
                break;
            }

            if (!selectedRoomId.HasValue || selectedInventory == null)
                return BadRequest("No hay habitación física disponible para realizar el check-in.");

            foreach (var row in selectedInventory)
                row.QuantityReserved += 1;

            rr.RoomId = selectedRoomId.Value;
        }

        var oldStatus = res.Status;
        res.Status = "CHECKED_IN";

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "CHECK_IN",
            OldStatus = oldStatus,
            NewStatus = res.Status,
            Reason = "Check-in administrativo",
            Actor = "ADMIN",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { res.Id, res.Code, res.Status });
    }

    [HttpPost("{id:int}/check-out")]
    public async Task<IActionResult> CheckOut(int id)
    {
        var res = await _db.Reservations
            .Include(r => r.Charges)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status == "CHECKED_OUT")
            return Ok(new { res.Id, res.Code, res.Status });

        if (res.Status != "CHECKED_IN")
            return BadRequest("Solo se puede hacer check-out a reservas CHECKED_IN.");

        var hasUnsettledCharges = res.Charges.Any(c => !c.IsVoided && !c.IsSettled);
        if (hasUnsettledCharges)
            return BadRequest("No se puede hacer check-out porque la cuenta de estancia tiene cargos pendientes de liquidar.");

        var oldStatus = res.Status;
        res.Status = "CHECKED_OUT";

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "CHECK_OUT",
            OldStatus = oldStatus,
            NewStatus = res.Status,
            Reason = "Check-out administrativo",
            Actor = "ADMIN",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new { res.Id, res.Code, res.Status });
    }

    [HttpPost("{id:int}/no-show")]
    public async Task<IActionResult> NoShow(int id)
    {
        var res = await _db.Reservations
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status == "NO_SHOW")
            return Ok(new { res.Id, res.Code, res.Status });

        if (res.Status != "CONFIRMED")
            return BadRequest("Solo se puede marcar no-show en reservas CONFIRMED.");

        var today = DateTime.Now.Date;
        if (today < res.CheckIn.Date)
            return BadRequest("No se puede marcar no-show antes de la fecha de check-in.");

        if (today >= res.CheckOut.Date)
            return BadRequest("No se puede marcar no-show en una reserva ya finalizada.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var rr in res.Rooms)
        {
            var typeInvRows = await _db.RoomTypeInventories
                .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == rr.RoomTypeId && x.Date >= today && x.Date < res.CheckOut)
                .ToListAsync();

            foreach (var row in typeInvRows)
            {
                if (row.QuantityReserved > 0)
                    row.QuantityReserved -= 1;
            }

            if (rr.RoomId.HasValue)
            {
                var invRows = await _db.RoomInventories
                    .Where(x => x.RoomId == rr.RoomId.Value && x.Date >= today && x.Date < res.CheckOut)
                    .ToListAsync();

                foreach (var row in invRows)
                {
                    if (row.QuantityReserved > 0)
                        row.QuantityReserved -= 1;
                }

                rr.RoomId = null;
            }
        }

        var oldStatus = res.Status;
        res.Status = "NO_SHOW";

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "NO_SHOW",
            OldStatus = oldStatus,
            NewStatus = res.Status,
            Reason = "Marcada como no-show por operación hotelera",
            Actor = "ADMIN",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { res.Id, res.Code, res.Status });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] AdminCancelReservationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("El motivo de cancelación es obligatorio.");

        var res = await _db.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status != "PENDING" && res.Status != "CONFIRMED")
        {
            if (res.Status == "CANCELED")
            {
                return Ok(new { res.Id, res.Code, res.Status });
            }

            return BadRequest("No se puede cancelar en este estado.");
        }

        var oldStatus = res.Status;
        var reason = request.Reason.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var rr in res.Rooms)
        {
            var typeInvRows = await _db.RoomTypeInventories
                .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == rr.RoomTypeId && x.Date >= res.CheckIn && x.Date < res.CheckOut)
                .ToListAsync();

            var expectedNights = (res.CheckOut.Date - res.CheckIn.Date).Days;
            if (typeInvRows.Count != expectedNights)
                return StatusCode(500, "Inventario comercial incompleto para liberar la reserva.");

            foreach (var row in typeInvRows)
            {
                if (row.QuantityReserved <= 0)
                    return StatusCode(500, "Inventario comercial inconsistente (QuantityReserved <= 0).");

                row.QuantityReserved -= 1;
            }

            if (rr.RoomId.HasValue)
            {
                var physicalInvRows = await _db.RoomInventories
                    .Where(x => x.RoomId == rr.RoomId.Value && x.Date >= res.CheckIn && x.Date < res.CheckOut)
                    .ToListAsync();

                foreach (var row in physicalInvRows)
                {
                    if (row.QuantityReserved > 0)
                        row.QuantityReserved -= 1;
                }

                rr.RoomId = null;
            }
        }

        res.Status = "CANCELED";

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "CANCEL",
            OldStatus = oldStatus,
            NewStatus = "CANCELED",
            Reason = reason,
            Actor = "ADMIN",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var emailResult = await SendCancellationEmailSafeAsync(res, reason);

        return Ok(new
        {
            res.Id,
            res.Code,
            res.Status,
            emailSent = emailResult.emailSent,
            emailError = emailResult.emailError
        });
    }

    [HttpPost("{id:int}/change-dates")]
    public async Task<IActionResult> ChangeDates(int id, [FromBody] AdminChangeReservationDatesRequest request)
    {
        if (request == null)
            return BadRequest("Solicitud inválida.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("El motivo del cambio es obligatorio.");

        var newCheckIn = request.CheckIn.Date;
        var newCheckOut = request.CheckOut.Date;

        if (newCheckOut <= newCheckIn)
            return BadRequest("La fecha de check-out debe ser mayor que la fecha de check-in.");

        var res = await _db.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status != "PENDING" && res.Status != "CONFIRMED")
            return BadRequest("Solo se pueden modificar reservas en estado PENDING o CONFIRMED.");

        var oldCheckIn = res.CheckIn;
        var oldCheckOut = res.CheckOut;
        var oldStatus = res.Status;
        var reason = request.Reason.Trim();

        var newNights = (newCheckOut - newCheckIn).Days;
        if (newNights <= 0)
            return BadRequest("La cantidad de noches debe ser mayor que cero.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var rr in res.Rooms)
        {
            var targetTypeInventory = await _db.RoomTypeInventories
                .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == rr.RoomTypeId && x.Date >= newCheckIn && x.Date < newCheckOut)
                .OrderBy(x => x.Date)
                .ToListAsync();

            if (targetTypeInventory.Count != newNights)
                return BadRequest("No hay inventario comercial completo para el nuevo rango solicitado.");

            if (targetTypeInventory.Any(x => (x.QuantityTotal - x.QuantityReserved) <= 0))
                return BadRequest("No hay disponibilidad suficiente para el nuevo rango solicitado.");
        }

        foreach (var rr in res.Rooms)
        {
            var currentTypeInventory = await _db.RoomTypeInventories
                .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == rr.RoomTypeId && x.Date >= oldCheckIn && x.Date < oldCheckOut)
                .ToListAsync();

            var expectedOldNights = (oldCheckOut.Date - oldCheckIn.Date).Days;
            if (currentTypeInventory.Count != expectedOldNights)
                return StatusCode(500, "Inventario comercial incompleto para liberar la reserva actual.");

            foreach (var row in currentTypeInventory)
            {
                if (row.QuantityReserved <= 0)
                    return StatusCode(500, "Inventario comercial inconsistente al liberar la reserva actual.");

                row.QuantityReserved -= 1;
            }
        }

        foreach (var rr in res.Rooms)
        {
            var newTypeInventory = await _db.RoomTypeInventories
                .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == rr.RoomTypeId && x.Date >= newCheckIn && x.Date < newCheckOut)
                .ToListAsync();

            foreach (var row in newTypeInventory)
                row.QuantityReserved += 1;

            if (rr.RoomId.HasValue)
            {
                var currentPhysicalInventory = await _db.RoomInventories
                    .Where(x => x.RoomId == rr.RoomId.Value && x.Date >= oldCheckIn && x.Date < oldCheckOut)
                    .ToListAsync();

                foreach (var row in currentPhysicalInventory)
                {
                    if (row.QuantityReserved > 0)
                        row.QuantityReserved -= 1;
                }

                var newPhysicalInventory = await _db.RoomInventories
                    .Where(x => x.RoomId == rr.RoomId.Value && x.Date >= newCheckIn && x.Date < newCheckOut)
                    .ToListAsync();

                if (newPhysicalInventory.Count != newNights)
                    return BadRequest("No hay inventario físico completo para la habitación asignada en el nuevo rango solicitado.");

                if (newPhysicalInventory.Any(x => (x.QuantityTotal - x.QuantityReserved) <= 0))
                    return BadRequest("La habitación asignada ya no está disponible en el nuevo rango solicitado.");

                foreach (var row in newPhysicalInventory)
                    row.QuantityReserved += 1;
            }

            rr.Nights = newNights;
            rr.Subtotal = rr.PricePerNight * newNights;
        }

        res.CheckIn = newCheckIn;
        res.CheckOut = newCheckOut;
        res.TotalAmount = res.Rooms.Sum(x => x.Subtotal);

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "CHANGE_DATES",
            OldStatus = oldStatus,
            NewStatus = res.Status,
            Reason = reason,
            Actor = "ADMIN",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var emailResult = await SendChangeDatesEmailSafeAsync(res, oldCheckIn, oldCheckOut, reason);

        return Ok(new
        {
            res.Id,
            res.Code,
            res.Status,
            res.CheckIn,
            res.CheckOut,
            res.TotalAmount,
            emailSent = emailResult.emailSent,
            emailError = emailResult.emailError
        });
    }


    [HttpGet("{id:int}/charges")]
    public async Task<IActionResult> GetCharges(int id)
    {
        var reservation = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Charges)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound("Reserva no existe.");

        var activeCharges = reservation.Charges
            .Where(c => !c.IsVoided)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Category,
                c.Description,
                c.Amount,
                c.CreatedAt,
                c.CreatedBy,
                c.IsVoided,
                c.IsSettled,
                c.SettledAt,
                c.SettledBy
            })
            .ToList();

        var chargesTotal = activeCharges.Sum(x => x.Amount);
        var settledChargesTotal = activeCharges.Where(x => x.IsSettled).Sum(x => x.Amount);
        var outstandingChargesTotal = activeCharges.Where(x => !x.IsSettled).Sum(x => x.Amount);

        return Ok(new
        {
            reservation.Id,
            reservation.Code,
            reservation.Status,
            BaseAmount = reservation.TotalAmount,
            ChargesTotal = chargesTotal,
            SettledChargesTotal = settledChargesTotal,
            OutstandingChargesTotal = outstandingChargesTotal,
            GrandTotal = reservation.TotalAmount + chargesTotal,
            IsStayAccountSettled = outstandingChargesTotal <= 0,
            Charges = activeCharges
        });
    }

    [HttpPost("{id:int}/charges")]
    public async Task<IActionResult> AddCharge(int id, [FromBody] AdminAddReservationChargeRequest request)
    {
        if (request == null)
            return BadRequest("Solicitud inválida.");

        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("La descripción del cargo es obligatoria.");

        if (request.Amount <= 0)
            return BadRequest("El monto del cargo debe ser mayor que cero.");

        var reservation = await _db.Reservations
            .Include(r => r.Charges)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound("Reserva no existe.");

        if (reservation.Status != "CHECKED_IN")
            return BadRequest("Solo se pueden registrar cargos en reservas CHECKED_IN.");

        var charge = new ReservationCharge
        {
            ReservationId = reservation.Id,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "OTHER" : request.Category.Trim().ToUpperInvariant(),
            Description = request.Description.Trim(),
            Amount = decimal.Round(request.Amount, 2),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            IsSettled = false
        };

        _db.ReservationCharges.Add(charge);
        await _db.SaveChangesAsync();

        var chargesTotal = await _db.ReservationCharges
            .Where(c => c.ReservationId == reservation.Id && !c.IsVoided)
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        return Ok(new
        {
            charge.Id,
            charge.Category,
            charge.Description,
            charge.Amount,
            charge.CreatedAt,
            charge.CreatedBy,
            BaseAmount = reservation.TotalAmount,
            ChargesTotal = chargesTotal,
            GrandTotal = reservation.TotalAmount + chargesTotal
        });
    }

    [HttpPost("{id:int}/charges/settle")]
    [HttpPost("{id:int}/settle-charges")]
    public async Task<IActionResult> SettleCharges(int id, [FromBody] AdminSettleReservationChargesRequest request)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Charges)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound("Reserva no existe.");

        if (reservation.Status != "CHECKED_IN")
            return BadRequest("Solo se puede liquidar la cuenta de una reserva en estado CHECKED_IN.");

        var pendingCharges = reservation.Charges
            .Where(c => !c.IsVoided && !c.IsSettled)
            .ToList();

        if (pendingCharges.Count == 0)
            return Ok(new
            {
                reservation.Id,
                reservation.Code,
                message = "La cuenta no tiene cargos pendientes de liquidar.",
                OutstandingChargesTotal = 0m
            });

        var outstandingAmount = pendingCharges.Sum(c => c.Amount);
        var cardDigits = new string((request.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var cvvDigits = new string((request.Cvv ?? string.Empty).Where(char.IsDigit).ToArray());

        if (cardDigits.Length < 13 || cardDigits.Length > 19)
            return BadRequest("El número de tarjeta debe tener entre 13 y 19 dígitos.");

        if (cvvDigits.Length < 3 || cvvDigits.Length > 4)
            return BadRequest("El CVV debe tener 3 o 4 dígitos.");

        if (string.IsNullOrWhiteSpace(request.CardHolderName))
            return BadRequest("Ingresa el nombre en tarjeta.");

        if (string.IsNullOrWhiteSpace(request.BillingAddress))
            return BadRequest("Ingresa la dirección de cobro.");

        if (request.Amount <= 0 || Math.Abs(request.Amount - outstandingAmount) > 0.01m)
            return BadRequest("El monto enviado no coincide con el saldo pendiente de la cuenta.");

        var last4 = cardDigits.Length >= 4 ? cardDigits[^4..] : cardDigits;
        var actor = User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "ADMIN";
        var now = DateTime.UtcNow;

        foreach (var charge in pendingCharges)
        {
            charge.IsSettled = true;
            charge.SettledAt = now;
            charge.SettledBy = actor;
        }

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = reservation.Id,
            Action = "SETTLE_CHARGES",
            OldStatus = reservation.Status,
            NewStatus = reservation.Status,
            Reason = $"Liquidación de cuenta de estancia por Q {outstandingAmount:0.00}. Tarjeta terminación {last4}. Titular: {request.CardHolderName.Trim()}",
            Actor = "ADMIN",
            CreatedAt = now
        });

        await _db.SaveChangesAsync();

        var activeCharges = reservation.Charges.Where(c => !c.IsVoided).ToList();
        var chargesTotal = activeCharges.Sum(c => c.Amount);
        var settledTotal = activeCharges.Where(c => c.IsSettled).Sum(c => c.Amount);
        var outstandingTotal = activeCharges.Where(c => !c.IsSettled).Sum(c => c.Amount);

        return Ok(new
        {
            reservation.Id,
            reservation.Code,
            message = "Cuenta liquidada correctamente.",
            BaseAmount = reservation.TotalAmount,
            ChargesTotal = chargesTotal,
            SettledChargesTotal = settledTotal,
            OutstandingChargesTotal = outstandingTotal,
            GrandTotal = reservation.TotalAmount + chargesTotal,
            IsStayAccountSettled = outstandingTotal <= 0
        });
    }

    [HttpPost("charges/{chargeId:int}/void")]
    public async Task<IActionResult> VoidCharge(int chargeId)
    {
        var charge = await _db.ReservationCharges
            .Include(c => c.Reservation)
            .FirstOrDefaultAsync(c => c.Id == chargeId);

        if (charge == null)
            return NotFound("Cargo no existe.");

        if (charge.IsVoided)
            return Ok(new { charge.Id, charge.IsVoided });

        var status = charge.Reservation.Status;
        if (status == "CHECKED_OUT" || status == "CANCELED" || status == "NO_SHOW")
            return BadRequest("No se puede anular un cargo de una reservación cerrada.");

        charge.IsVoided = true;
        await _db.SaveChangesAsync();

        return Ok(new { charge.Id, charge.IsVoided });
    }

    private async Task ExpirePendingReservationsAsync()
    {
        var reservations = await _db.Reservations
            .Include(r => r.Rooms)
            .Where(r => r.Status == "PENDING" && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= DateTime.UtcNow)
            .ToListAsync();

        if (reservations.Count == 0)
            return;

        foreach (var reservation in reservations)
        {
            foreach (var room in reservation.Rooms)
            {
                var typeInvRows = await _db.RoomTypeInventories
                    .Where(x => x.HotelId == reservation.HotelId && x.RoomTypeId == room.RoomTypeId && x.Date >= reservation.CheckIn && x.Date < reservation.CheckOut)
                    .ToListAsync();

                foreach (var row in typeInvRows)
                {
                    if (row.QuantityReserved > 0)
                        row.QuantityReserved -= 1;
                }

                if (!room.RoomId.HasValue)
                    continue;

                var physicalInvRows = await _db.RoomInventories
                    .Where(x => x.RoomId == room.RoomId.Value && x.Date >= reservation.CheckIn && x.Date < reservation.CheckOut)
                    .ToListAsync();

                foreach (var row in physicalInvRows)
                {
                    if (row.QuantityReserved > 0)
                        row.QuantityReserved -= 1;
                }

                room.RoomId = null;
            }

            _db.ReservationAudits.Add(new ReservationAudit
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

        await _db.SaveChangesAsync();
    }

    private async Task<(bool emailSent, string? emailError)> SendCancellationEmailSafeAsync(Reservation res, string reason)
    {
        var userEmail = res.UserId.HasValue
            ? await _db.Users
                .Where(u => u.Id == res.UserId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync()
            : null;

        if (string.IsNullOrWhiteSpace(userEmail))
            return (false, "El usuario no tiene correo registrado.");

        try
        {
            var subject = $"Reservación cancelada por HotelChain: {res.Code}";
            var htmlBody = $@"
                <h2>Reservación cancelada</h2>
                <p>Tu reservación <strong>{res.Code}</strong> ha sido cancelada por la cadena hotelera.</p>
                <p><strong>Hotel:</strong> {WebUtility.HtmlEncode(res.Hotel.Name)}</p>
                <p><strong>Check-in:</strong> {res.CheckIn:dd/MM/yyyy}</p>
                <p><strong>Check-out:</strong> {res.CheckOut:dd/MM/yyyy}</p>
                <p><strong>Motivo:</strong> {WebUtility.HtmlEncode(reason)}</p>
                <p>Si necesitas más información, por favor comunícate con soporte.</p>";

            await _emailSender.SendAsync(userEmail, subject, htmlBody);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool emailSent, string? emailError)> SendChangeDatesEmailSafeAsync(
        Reservation res,
        DateTime oldCheckIn,
        DateTime oldCheckOut,
        string reason)
    {
        var userEmail = res.UserId.HasValue
            ? await _db.Users
                .Where(u => u.Id == res.UserId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync()
            : null;

        if (string.IsNullOrWhiteSpace(userEmail))
            return (false, "El usuario no tiene correo registrado.");

        try
        {
            var subject = $"Cambio de fechas en tu reservación HotelChain: {res.Code}";
            var htmlBody = $@"
                <h2>Cambio de fechas de reservación</h2>
                <p>La cadena hotelera ha actualizado las fechas de tu reservación <strong>{res.Code}</strong>.</p>
                <p><strong>Hotel:</strong> {WebUtility.HtmlEncode(res.Hotel.Name)}</p>
                <p><strong>Fechas anteriores:</strong> {oldCheckIn:dd/MM/yyyy} - {oldCheckOut:dd/MM/yyyy}</p>
                <p><strong>Fechas nuevas:</strong> {res.CheckIn:dd/MM/yyyy} - {res.CheckOut:dd/MM/yyyy}</p>
                <p><strong>Nuevo total:</strong> Q {res.TotalAmount:0.00}</p>
                <p><strong>Motivo:</strong> {WebUtility.HtmlEncode(reason)}</p>
                <p>Si necesitas más información, por favor comunícate con soporte.</p>";

            await _emailSender.SendAsync(userEmail, subject, htmlBody);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
