using HotelChain.Api.Contracts;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador administrativo para la gestión del inventario diario por habitación.
/// </summary>
/// <remarks>
/// Este módulo controla la disponibilidad por fecha de habitaciones individuales.
/// La cantidad total permitida por día para una habitación física es 0 o 1.
/// </remarks>
[ApiController]
[Route("api/admin/room-inventories")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminRoomInventoriesController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de inventario de habitaciones.
    /// </summary>
    /// <param name="db">Contexto de base de datos del sistema.</param>
    public AdminRoomInventoriesController(HotelChainDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Obtiene el inventario registrado para una habitación específica.
    /// </summary>
    /// <param name="roomId">Identificador de la habitación.</param>
    /// <returns>Listado de inventario por fecha para la habitación indicada.</returns>
    [HttpGet]
    public async Task<IActionResult> GetByRoom([FromQuery] int roomId)
    {
        if (roomId <= 0)
            return BadRequest("roomId es requerido.");

        var roomExists = await _db.Rooms.AnyAsync(r => r.Id == roomId);
        if (!roomExists)
            return BadRequest("La habitación no existe.");

        var items = await _db.RoomInventories
            .Include(x => x.Room)
                .ThenInclude(r => r.Hotel)
            .Where(x => x.RoomId == roomId)
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

    /// <summary>
    /// Inserta o actualiza el inventario de una habitación en un rango de fechas.
    /// </summary>
    /// <param name="req">Solicitud con habitación, rango y disponibilidad total por día.</param>
    /// <returns>Resultado de la operación.</returns>
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

        var dates = new List<DateTime>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        var existingInventories = await _db.RoomInventories
            .Where(x => x.RoomId == req.RoomId && dates.Contains(x.Date))
            .ToListAsync();

        foreach (var date in dates)
        {
            var existing = existingInventories.FirstOrDefault(x => x.Date == date);

            if (existing == null)
            {
                var inventory = new RoomInventory
                {
                    RoomId = req.RoomId,
                    Date = date,
                    QuantityTotal = req.QuantityTotal,
                    QuantityReserved = 0
                };

                _db.RoomInventories.Add(inventory);
            }
            else
            {
                if (req.QuantityTotal < existing.QuantityReserved)
                {
                    return BadRequest(
                        $"No se puede poner QuantityTotal={req.QuantityTotal} para la fecha {date:yyyy-MM-dd} porque ya hay {existing.QuantityReserved} reservadas.");
                }

                existing.QuantityTotal = req.QuantityTotal;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Inventario actualizado correctamente."
        });
    }
}