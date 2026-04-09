using HotelChain.Api.Contracts;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Solicitud utilizada para cancelaciones administrativas de reservaciones.
/// </summary>
public class AdminCancelReservationRequest
{
    /// <summary>
    /// Motivo obligatorio de la cancelación administrativa.
    /// </summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Controlador administrativo para consulta y cancelación de reservaciones.
/// </summary>
/// <remarks>
/// Permite al administrador listar reservaciones del sistema y cancelarlas
/// liberando inventario y registrando auditoría de la operación.
/// </remarks>
[ApiController]
[Route("api/admin/reservations")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminReservationsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia del controlador administrativo de reservaciones.
    /// </summary>
    /// <param name="db">Contexto de base de datos del sistema.</param>
    public AdminReservationsController(HotelChainDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Obtiene el listado completo de reservaciones para uso administrativo.
    /// </summary>
    /// <returns>Listado de reservaciones con información de hotel, pago y usuario.</returns>
    [HttpGet]
    public async Task<ActionResult<List<AdminReservationDto>>> GetAll()
    {
        var reservations = await _db.Reservations
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(r => new AdminReservationDto
            {
                Id = r.Id,
                Code = r.Code,
                Hotel = r.Hotel.Name,
                CheckIn = r.CheckIn,
                CheckOut = r.CheckOut,
                Guests = r.Guests,
                TotalAmount = r.TotalAmount,
                Status = r.Status,
                PaymentStatus = r.Payment != null ? r.Payment.Status : "PENDING",
                IsPaid = r.Payment != null && r.Payment.Status == "APPROVED",
                UserId = r.UserId
            })
            .ToListAsync();

        return Ok(reservations);
    }

    /// <summary>
    /// Cancela una reservación desde el módulo administrativo.
    /// </summary>
    /// <param name="id">Identificador de la reservación.</param>
    /// <param name="request">Motivo de cancelación.</param>
    /// <returns>Estado final de la reservación cancelada.</returns>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] AdminCancelReservationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("El motivo de cancelación es obligatorio.");

        var res = await _db.Reservations
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res == null)
            return NotFound("Reserva no existe.");

        if (res.Status == "PENDING" || res.Status == "CONFIRMED")
        {
        }
        else if (res.Status == "CANCELED")
        {
            return Ok(new
            {
                res.Id,
                res.Code,
                res.Status
            });
        }
        else
        {
            return BadRequest("No se puede cancelar en este estado.");
        }

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
            Reason = request.Reason.Trim(),
            Actor = "ADMIN",
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