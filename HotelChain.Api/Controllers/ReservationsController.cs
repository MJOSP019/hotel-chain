using System.Security.Claims;
using HotelChain.Api.Contracts;
using HotelChain.Api.Services;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador público para la creación, consulta, pago, cancelación,
/// cambio de fechas y generación de PDF de reservaciones.
/// </summary>
/// <remarks>
/// Este controlador concentra gran parte del flujo transaccional del sistema:
/// creación de reservaciones, validación de disponibilidad, checkout,
/// cancelación con regla de 24 horas y emisión del comprobante PDF.
/// </remarks>
[ApiController]
[Route("api/public/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly HotelChainDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ReservationsController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de reservaciones.
    /// </summary>
    /// <param name="db">Contexto de base de datos del sistema.</param>
    /// <param name="emailSender">Servicio encargado del envío de correos electrónicos.</param>
    /// <param name="logger">Servicio de logging del controlador.</param>
    public ReservationsController(
        HotelChainDbContext db,
        IEmailSender emailSender,
        ILogger<ReservationsController> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <summary>
    /// Representa la solicitud para crear una nueva reservación.
    /// </summary>
    public class CreateReservationRequest
    {
        /// <summary>
        /// Identificador del hotel donde se realizará la reservación.
        /// </summary>
        public int HotelId { get; set; }

        /// <summary>
        /// Identificador del tipo de habitación a reservar comercialmente.
        /// </summary>
        public int RoomTypeId { get; set; }

        /// <summary>
        /// Fecha de check-in solicitada.
        /// </summary>
        public DateTime CheckIn { get; set; }

        /// <summary>
        /// Fecha de check-out solicitada.
        /// </summary>
        public DateTime CheckOut { get; set; }

        /// <summary>
        /// Cantidad de huéspedes para la reservación.
        /// </summary>
        public int Guests { get; set; }
    }

    /// <summary>
    /// Representa la solicitud de pago para confirmar una reservación pendiente.
    /// </summary>
    public class CheckoutRequest
    {
        /// <summary>
        /// Número de tarjeta de crédito o débito.
        /// </summary>
        public string CardNumber { get; set; } = "";

        /// <summary>
        /// Código de seguridad de la tarjeta.
        /// </summary>
        public string Cvv { get; set; } = "";

        /// <summary>
        /// Nombre del titular de la tarjeta.
        /// </summary>
        public string CardHolderName { get; set; } = "";

        /// <summary>
        /// Dirección de cobro utilizada en el checkout.
        /// </summary>
        public string BillingAddress { get; set; } = "";
    }

    /// <summary>
    /// Representa la solicitud para cambiar las fechas de una reservación existente.
    /// </summary>
    public class ChangeReservationDatesRequest
    {
        /// <summary>
        /// Nueva fecha de check-in.
        /// </summary>
        public DateTime CheckIn { get; set; }

        /// <summary>
        /// Nueva fecha de check-out.
        /// </summary>
        public DateTime CheckOut { get; set; }

        /// <summary>
        /// Motivo opcional del cambio de fechas.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Crea una nueva reservación en estado PENDING para un usuario autenticado.
    /// </summary>
    /// <param name="req">Datos de la reservación solicitada.</param>
    /// <returns>
    /// La información básica de la reservación creada, incluyendo código, estado, total y URL del PDF.
    /// </returns>
    [Authorize(Roles = Roles.REGISTERED + "," + Roles.ADMIN)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest req)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = null;

        if (Guid.TryParse(userIdValue, out var parsedUserId))
        {
            userId = parsedUserId;
        }
        else
        {
            return Unauthorized();
        }

        var start = req.CheckIn.Date;
        var end = req.CheckOut.Date;

        if (end <= start) return BadRequest("Fechas inválidas.");
        if (req.Guests <= 0) return BadRequest("Guests inválido.");
        if (req.HotelId <= 0) return BadRequest("Hotel inválido.");
        if (req.RoomTypeId <= 0) return BadRequest("Tipo de habitación inválido.");

        var nights = (end - start).Days;

        var roomOption = await _db.Rooms
            .Where(r => r.IsActive)
            .Where(r => r.Hotel.IsActive)
            .Where(r => r.HotelId == req.HotelId)
            .Where(r => r.RoomTypeId == req.RoomTypeId)
            .Where(r => r.MaxGuests >= req.Guests)
            .OrderBy(r => r.BasePricePerNight)
            .ThenByDescending(r => r.MaxGuests)
            .ThenBy(r => r.Id)
            .Select(r => new
            {
                r.HotelId,
                r.RoomTypeId,
                r.BasePricePerNight
            })
            .FirstOrDefaultAsync();

        if (roomOption is null)
            return BadRequest("No existe una opción activa para ese tipo de habitación con la capacidad solicitada.");

        var (isValid, error, inventoryRows) = await ValidateCommercialAvailabilityAsync(req.HotelId, req.RoomTypeId, start, end);
        if (!isValid)
            return BadRequest(error);

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var row in inventoryRows)
            row.QuantityReserved += 1;

        var total = roomOption.BasePricePerNight * nights;
        var code = $"R-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var reservation = new Reservation
        {
            Code = code,
            HotelId = roomOption.HotelId,
            UserId = userId,
            CheckIn = start,
            CheckOut = end,
            Guests = req.Guests,
            TotalAmount = total,
            Status = "PENDING",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Rooms = new List<ReservationRoom>
            {
                new()
                {
                    RoomTypeId = roomOption.RoomTypeId,
                    RoomId = null,
                    PricePerNight = roomOption.BasePricePerNight,
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
            reservation.TotalAmount,
            PdfUrl = $"/api/public/reservations/{reservation.Code}/pdf"
        });
    }

    /// <summary>
    /// Obtiene el detalle de una reservación a partir de su código único.
    /// </summary>
    /// <param name="code">Código único de la reservación.</param>
    /// <returns>
    /// Información del hotel, fechas, estado, pago y habitaciones asociadas.
    /// </returns>
    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var res = await _db.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.Room)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.RoomType)
            .Include(r => r.Payment)
            .Include(r => r.Charges)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null)
            return NotFound();

        if (res.Status == "PENDING" &&
            res.ExpiresAt.HasValue &&
            res.ExpiresAt.Value < DateTime.UtcNow)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var oldStatus = res.Status;
            var expectedNights = (res.CheckOut.Date - res.CheckIn.Date).Days;

            foreach (var rr in res.Rooms)
            {
                var commercialRows = await _db.RoomTypeInventories
                    .Where(x => x.HotelId == res.HotelId &&
                                x.RoomTypeId == rr.RoomTypeId &&
                                x.Date >= res.CheckIn.Date &&
                                x.Date < res.CheckOut.Date)
                    .ToListAsync();

                if (commercialRows.Count == expectedNights)
                {
                    foreach (var row in commercialRows)
                        row.QuantityReserved = Math.Max(0, row.QuantityReserved - 1);
                }

                if (!rr.RoomId.HasValue)
                    continue;

                var physicalRows = await _db.RoomInventories
                    .Where(x => x.RoomId == rr.RoomId.Value &&
                                x.Date >= res.CheckIn.Date &&
                                x.Date < res.CheckOut.Date)
                    .ToListAsync();

                if (physicalRows.Count == expectedNights)
                {
                    foreach (var row in physicalRows)
                        row.QuantityReserved = Math.Max(0, row.QuantityReserved - 1);
                }
            }

            res.Status = "EXPIRED";

            _db.ReservationAudits.Add(new ReservationAudit
            {
                ReservationId = res.Id,
                Action = "EXPIRE",
                OldStatus = oldStatus,
                NewStatus = "EXPIRED",
                Reason = "Expiración automática por pago no completado dentro del tiempo límite.",
                Actor = "SYSTEM",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

            var activeCharges = res.Charges
        .Where(c => !c.IsVoided)
        .OrderBy(c => c.CreatedAt)
        .ToList();

    var baseAmount = res.TotalAmount;
    var chargesTotal = activeCharges.Sum(c => c.Amount);
    var settledChargesTotal = activeCharges.Where(c => c.IsSettled).Sum(c => c.Amount);
    var outstandingChargesTotal = activeCharges.Where(c => !c.IsSettled).Sum(c => c.Amount);
    var grandTotal = baseAmount + chargesTotal;

    return Ok(new
    {
        res.Id,
        res.Code,
        Hotel = res.Hotel.Name,
        res.CheckIn,
        res.CheckOut,
        Nights = Math.Max(1, (res.CheckOut.Date - res.CheckIn.Date).Days),
        res.Guests,
        res.Status,

        BaseAmount = baseAmount,
        ChargesTotal = chargesTotal,
        SettledChargesTotal = settledChargesTotal,
        OutstandingChargesTotal = outstandingChargesTotal,
        GrandTotal = grandTotal,
        TotalAmount = grandTotal,
        IsStayAccountSettled = outstandingChargesTotal <= 0,

        IsPaid = res.Payment != null && res.Payment.Status == "APPROVED",
        PaymentStatus = res.Payment?.Status ?? "PENDING",
        CardLast4 = res.Payment != null ? $"**** {res.Payment.Last4}" : null,

        Charges = activeCharges.Select(c => new
        {
            c.Id,
            c.Category,
            c.Description,
            c.Amount,
            c.CreatedAt,
            c.CreatedBy,
            c.IsSettled,
            c.SettledAt,
            c.SettledBy
        }),

        Rooms = res.Rooms.Select(x => new
        {
            x.RoomTypeId,
            RoomType = x.RoomType.Name,
            x.RoomId,
            RoomName = x.Room != null ? x.Room.NameOrNumber : "Pendiente de check-in",
            x.PricePerNight,
            x.Nights,
            x.Subtotal
        })
    });
    }

    /// <summary>
    /// Cambia las fechas de una reservación si todavía cumple las reglas del negocio.
    /// </summary>
    /// <param name="code">Código de la reservación a modificar.</param>
    /// <param name="req">Nuevas fechas propuestas para la reservación.</param>
    /// <returns>
    /// El resumen actualizado de la reservación si el cambio fue exitoso.
    /// </returns>
    [Authorize(Roles = Roles.REGISTERED + "," + Roles.ADMIN)]
    [HttpPost("{code}/change-dates")]
    public async Task<IActionResult> ChangeDates(string code, [FromBody] ChangeReservationDatesRequest req)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var newStart = req.CheckIn.Date;
        var newEnd = req.CheckOut.Date;

        if (newEnd <= newStart)
            return BadRequest("Fechas inválidas.");

        if (newStart < DateTime.Now.Date)
            return BadRequest("La nueva fecha de check-in no puede estar en el pasado.");

        var res = await _db.Reservations
            .Include(r => r.Rooms)
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null)
            return NotFound("Reserva no existe.");

        var isAdmin = User.IsInRole(Roles.ADMIN);
        if (!isAdmin && res.UserId != userId)
            return Forbid();

        if (res.Status != "PENDING" && res.Status != "CONFIRMED")
            return BadRequest("Solo se pueden cambiar fechas de reservas PENDING o CONFIRMED.");

        if (res.Rooms == null || res.Rooms.Count == 0)
            return StatusCode(500, "La reserva no tiene habitaciones asociadas.");

        var oldStart = res.CheckIn.Date;
        var oldEnd = res.CheckOut.Date;

        var oldNights = (oldEnd - oldStart).Days;
        var newNights = (newEnd - newStart).Days;

        if (oldNights <= 0 || newNights <= 0)
            return BadRequest("Fechas inválidas.");

        var changeDeadlineLocal = oldStart.AddHours(-24);
        if (DateTime.Now > changeDeadlineLocal)
            return BadRequest("No se puede cambiar la fecha: faltan menos de 24 horas para el check-in.");

        if (res.Payment != null && newNights != oldNights)
            return BadRequest("No se puede cambiar la cantidad de noches en una reserva pagada.");

        var roomTypeId = res.Rooms.First().RoomTypeId;
        var (isValid, error, newCommercialRows) = await ValidateCommercialAvailabilityAsync(res.HotelId, roomTypeId, newStart, newEnd);
        if (!isValid)
            return BadRequest(error);

        await using var tx = await _db.Database.BeginTransactionAsync();

        var oldCommercialRows = await _db.RoomTypeInventories
            .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == roomTypeId && x.Date >= oldStart && x.Date < oldEnd)
            .ToListAsync();

        foreach (var row in oldCommercialRows)
        {
            if (row.QuantityReserved <= 0)
                return StatusCode(500, "Inventario comercial inconsistente (QuantityReserved <= 0).");
            row.QuantityReserved -= 1;
        }

        foreach (var rr in res.Rooms)
        {
            if (!rr.RoomId.HasValue)
                continue;

            var oldInvRows = await _db.RoomInventories
                .Where(x => x.RoomId == rr.RoomId.Value && x.Date >= oldStart && x.Date < oldEnd)
                .ToListAsync();

            if (oldInvRows.Count != oldNights)
                return StatusCode(500, "Inventario incompleto para liberar la reserva actual.");

            foreach (var row in oldInvRows)
            {
                if (row.QuantityReserved <= 0)
                    return StatusCode(500, "Inventario inconsistente (QuantityReserved <= 0).");

                row.QuantityReserved -= 1;
            }

            rr.RoomId = null;
        }

        foreach (var row in newCommercialRows)
            row.QuantityReserved += 1;

        decimal newTotal = 0m;

        foreach (var rr in res.Rooms)
        {
            rr.Nights = newNights;
            rr.Subtotal = rr.PricePerNight * newNights;
            newTotal += rr.Subtotal;
        }

        res.CheckIn = newStart;
        res.CheckOut = newEnd;
        res.TotalAmount = newTotal;

        _db.ReservationAudits.Add(new ReservationAudit
        {
            ReservationId = res.Id,
            Action = "CHANGE_DATES",
            OldStatus = res.Status,
            NewStatus = res.Status,
            Reason = req.Reason,
            Actor = "PUBLIC",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            res.Id,
            res.Code,
            res.Status,
            res.CheckIn,
            res.CheckOut,
            res.TotalAmount
        });
    }

    /// <summary>
    /// Cancela una reservación existente si todavía cumple la regla de al menos 24 horas
    /// antes del check-in y libera el inventario asociado.
    /// </summary>
    /// <param name="code">Código de la reservación a cancelar.</param>
    /// <param name="request">Motivo opcional de cancelación.</param>
    /// <returns>
    /// El estado final de la reservación luego de la operación.
    /// </returns>
    [Authorize(Roles = Roles.REGISTERED + "," + Roles.ADMIN)]
    [HttpPost("{code}/cancel")]
    public async Task<IActionResult> Cancel(string code, [FromBody] CancelReservationRequest? request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var res = await _db.Reservations
            .Include(r => r.Rooms)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null) return NotFound("Reserva no existe.");

        var isAdmin = User.IsInRole(Roles.ADMIN);
        if (!isAdmin && res.UserId != userId)
            return Forbid();

        if (res.Status == "PENDING" || res.Status == "CONFIRMED")
        {
        }
        else if (res.Status == "CANCELED")
        {
            return Ok(new { res.Id, res.Code, res.Status });
        }
        else
        {
            return BadRequest("No se puede cancelar en este estado.");
        }

        var oldStatus = res.Status;

        var cancelDeadlineLocal = res.CheckIn.AddHours(-24);
        if (DateTime.Now > cancelDeadlineLocal)
            return BadRequest("No se puede cancelar: faltan menos de 24 horas para el check-in.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var rr in res.Rooms)
        {
            var roomTypeInvRows = await _db.RoomTypeInventories
                .Where(x => x.HotelId == res.HotelId && x.RoomTypeId == rr.RoomTypeId && x.Date >= res.CheckIn && x.Date < res.CheckOut)
                .ToListAsync();

            var expectedNights = (res.CheckOut.Date - res.CheckIn.Date).Days;
            if (roomTypeInvRows.Count != expectedNights)
                return StatusCode(500, "Inventario comercial incompleto para liberar la reserva.");

            foreach (var row in roomTypeInvRows)
            {
                if (row.QuantityReserved <= 0)
                    return StatusCode(500, "Inventario comercial inconsistente (QuantityReserved <= 0).");

                row.QuantityReserved -= 1;
            }

            if (!rr.RoomId.HasValue)
                continue;

            var invRows = await _db.RoomInventories
                .Where(x => x.RoomId == rr.RoomId.Value && x.Date >= res.CheckIn && x.Date < res.CheckOut)
                .ToListAsync();

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
            Reason = request?.Reason,
            Actor = "PUBLIC",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { res.Id, res.Code, res.Status });
    }

    /// <summary>
    /// Genera y devuelve el comprobante PDF de una reservación.
    /// </summary>
    /// <param name="code">Código de la reservación.</param>
    /// <returns>Archivo PDF con el detalle de la reservación.</returns>
    [Authorize(Roles = Roles.REGISTERED + "," + Roles.ADMIN)]
    [HttpGet("{code}/pdf")]
    public async Task<IActionResult> GetPdf(string code)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var res = await _db.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.Room)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.RoomType)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null) return NotFound("Reserva no existe.");

        var isAdmin = User.IsInRole(Roles.ADMIN);
        if (!isAdmin && res.UserId != userId)
            return Forbid();

        var bytes = ReservationPdfGenerator.Generate(res);

        var fileName = $"Reserva-{res.Code}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Confirma una reservación pendiente registrando un pago demo
    /// y actualizando su estado a CONFIRMED.
    /// </summary>
    /// <param name="code">Código de la reservación a pagar.</param>
    /// <param name="req">Datos del checkout.</param>
    /// <returns>
    /// El estado final del pago y un mensaje de depuración sobre el envío de correo.
    /// </returns>
    [Authorize(Roles = Roles.REGISTERED + "," + Roles.ADMIN)]
    [HttpPost("{code}/checkout")]
    public async Task<IActionResult> Checkout(string code, [FromBody] CheckoutRequest req)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var res = await _db.Reservations
            .Include(r => r.Payment)
            .Include(r => r.Hotel)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.Room)
            .Include(r => r.Rooms)
                .ThenInclude(rr => rr.RoomType)
            .FirstOrDefaultAsync(r => r.Code == code);

        if (res == null) return NotFound("Reserva no existe.");

        var isAdmin = User.IsInRole(Roles.ADMIN);
        if (!isAdmin && res.UserId != userId)
            return Forbid();

        if (res.Status != "PENDING") return BadRequest("Solo se puede pagar una reserva PENDING.");
        if (res.Payment != null) return BadRequest("Esta reserva ya tiene un pago registrado.");

        var card = (req.CardNumber ?? "").Replace(" ", "").Replace("-", "");
        if (card.Length < 12 || card.Length > 19 || !card.All(char.IsDigit))
            return BadRequest("Número de tarjeta inválido.");
        if (string.IsNullOrWhiteSpace(req.Cvv) || !(req.Cvv.Length == 3 || req.Cvv.Length == 4) || !req.Cvv.All(char.IsDigit))
            return BadRequest("CVV inválido.");
        if (!IsValidLuhn(card))
            return BadRequest("Número de tarjeta inválido (Luhn).");

        var last4 = card[^4..];

        await using var tx = await _db.Database.BeginTransactionAsync();

        res.Status = "CONFIRMED";

        _db.ReservationPayments.Add(new ReservationPayment
        {
            ReservationId = res.Id,
            Last4 = last4,
            CardHolderName = req.CardHolderName.Trim(),
            BillingAddress = req.BillingAddress.Trim(),
            Status = "APPROVED",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        string? emailDebugMessage = null;

        if (res.UserId.HasValue)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == res.UserId.Value);

                if (user == null)
                {
                    emailDebugMessage = "No se encontró el usuario de la reserva.";
                }
                else if (string.IsNullOrWhiteSpace(user.Email))
                {
                    emailDebugMessage = "El usuario no tiene Email guardado.";
                }
                else
                {
                    var roomNames = res.Rooms.Any()
                        ? string.Join(", ", res.Rooms.Select(x => x.Room?.NameOrNumber ?? (x.RoomId.HasValue ? x.RoomId.Value.ToString() : "Sin asignar")))
                        : "-";

                    var subject = $"Confirmación de reservación {res.Code}";
                    var htmlBody = $@"
                        <h2>Reservación confirmada</h2>
                        <p>Hola {user.FirstName} {user.LastName},</p>
                        <p>Tu reservación ha sido confirmada correctamente.</p>
                        <hr />
                        <p><strong>Código:</strong> {res.Code}</p>
                        <p><strong>Hotel:</strong> {res.Hotel?.Name}</p>
                        <p><strong>Check-in:</strong> {res.CheckIn:yyyy-MM-dd}</p>
                        <p><strong>Check-out:</strong> {res.CheckOut:yyyy-MM-dd}</p>
                        <p><strong>Huéspedes:</strong> {res.Guests}</p>
                        <p><strong>Habitación:</strong> {roomNames}</p>
                        <p><strong>Total:</strong> Q {res.TotalAmount:0.00}</p>
                        <p><strong>Pago:</strong> APPROVED</p>
                        <p><strong>Tarjeta:</strong> **** {last4}</p>
                        <hr />
                        <p>Gracias por reservar con HotelChain.</p>";

                    await _emailSender.SendAsync(user.Email, subject, htmlBody);
                    emailDebugMessage = $"Correo enviado a {user.Email}";
                }
            }
            catch (Exception ex)
            {
                emailDebugMessage = $"Error enviando correo: {ex.Message}";
                _logger.LogError(ex, "No se pudo enviar el correo de confirmación para la reserva {Code}", res.Code);
            }
        }
        else
        {
            emailDebugMessage = "La reserva no tiene UserId.";
        }

        return Ok(new
        {
            res.Id,
            res.Code,
            res.Status,
            PaymentStatus = "APPROVED",
            CardLast4 = last4,
            EmailDebug = emailDebugMessage
        });
    }

    /// <summary>
    /// Valida un número de tarjeta utilizando el algoritmo de Luhn.
    /// </summary>
    
    /// <returns>
    /// <see langword="true"/> si el número cumple la validación; de lo contrario, <see langword="false"/>.
    /// </returns>
    private async Task<(bool IsValid, string? Error, List<RoomTypeInventory> Rows)> ValidateCommercialAvailabilityAsync(
        int hotelId,
        int roomTypeId,
        DateTime start,
        DateTime end)
    {
        var nights = (end - start).Days;
        var rows = await _db.RoomTypeInventories
            .Where(x => x.HotelId == hotelId && x.RoomTypeId == roomTypeId && x.Date >= start && x.Date <= end)
            .OrderBy(x => x.Date)
            .ToListAsync();

        var stayRows = rows
            .Where(x => x.Date >= start && x.Date < end)
            .OrderBy(x => x.Date)
            .ToList();

        if (stayRows.Count != nights)
            return (false, "No hay inventario comercial completo para el rango solicitado.", stayRows);

        if (stayRows.Any(x => x.IsClosed))
            return (false, "La tarifa no está disponible para una o más fechas seleccionadas.", stayRows);

        var arrivalRow = stayRows.FirstOrDefault(x => x.Date == start);
        if (arrivalRow?.ClosedToArrival == true)
            return (false, "No se permiten llegadas para la fecha seleccionada.", stayRows);

        var departureRow = rows.FirstOrDefault(x => x.Date == end);
        if (departureRow?.ClosedToDeparture == true)
            return (false, "No se permiten salidas para la fecha seleccionada.", stayRows);

        var minLos = stayRows
            .Where(x => x.MinLengthOfStay.HasValue)
            .Select(x => x.MinLengthOfStay!.Value)
            .DefaultIfEmpty(0)
            .Max();

        if (minLos > 0 && nights < minLos)
            return (false, $"La estancia mínima para estas fechas es de {minLos} noche(s).", stayRows);

        var maxLos = stayRows
            .Where(x => x.MaxLengthOfStay.HasValue)
            .Select(x => x.MaxLengthOfStay!.Value)
            .DefaultIfEmpty(0)
            .Where(x => x > 0)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        if (maxLos != int.MaxValue && nights > maxLos)
            return (false, $"La estancia máxima para estas fechas es de {maxLos} noche(s).", stayRows);

        if (stayRows.Any(x => (x.QuantityTotal - x.QuantityReserved) <= 0))
            return (false, "No hay disponibilidad para ese tipo de habitación en el rango solicitado.", stayRows);

        return (true, null, stayRows);
    }

    private static bool IsValidLuhn(string digits)
    {
        int sum = 0;
        bool alt = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (alt)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alt = !alt;
        }

        return sum % 10 == 0;
    }
}