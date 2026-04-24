using HotelChain.Api.DTOs.Admin;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador administrativo para la gestión de habitaciones de los hoteles de la cadena.
/// </summary>
/// <remarks>
/// Permite listar, consultar, crear, actualizar, desactivar y reactivar habitaciones.
/// Todos los endpoints requieren autenticación con rol ADMIN.
/// </remarks>
[ApiController]
[Route("api/admin/rooms")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminRoomsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia del controlador administrativo de habitaciones.
    /// </summary>
    /// <param name="db">Contexto de base de datos del sistema.</param>
    public AdminRoomsController(HotelChainDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Obtiene el listado de habitaciones registradas, opcionalmente filtrado por hotel.
    /// </summary>
    /// <param name="hotelId">Identificador opcional del hotel.</param>
    /// <returns>Listado de habitaciones con sus datos administrativos.</returns>
    [HttpGet]
    public async Task<ActionResult<List<AdminRoomDto>>> GetAll([FromQuery] int? hotelId)
    {
        var query = _db.Rooms
            .Include(r => r.Hotel)
            .Include(r => r.RoomType)
            .AsQueryable();

        if (hotelId.HasValue)
        {
            query = query.Where(r => r.HotelId == hotelId.Value);
        }

        var rooms = await query
            .OrderBy(r => r.Hotel.Name)
            .ThenBy(r => r.NameOrNumber)
            .Select(r => new AdminRoomDto
            {
                Id = r.Id,
                HotelId = r.HotelId,
                HotelName = r.Hotel.Name,
                RoomTypeId = r.RoomTypeId,
                RoomTypeName = r.RoomType.Name,
                NameOrNumber = r.NameOrNumber,
                MaxGuests = r.MaxGuests,
                BasePricePerNight = r.BasePricePerNight,
                BedType = r.BedType,
                AreaSquareMeters = r.AreaSquareMeters,
                ShortDescription = r.ShortDescription,
                ImageUrl = r.ImageUrl,
                IsActive = r.IsActive
            })
            .ToListAsync();

        return Ok(rooms);
    }

    /// <summary>
    /// Obtiene el detalle administrativo de una habitación por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la habitación.</param>
    /// <returns>Información detallada de la habitación si existe.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminRoomDto>> GetById(int id)
    {
        var room = await _db.Rooms
            .Include(r => r.Hotel)
            .Include(r => r.RoomType)
            .Where(r => r.Id == id)
            .Select(r => new AdminRoomDto
            {
                Id = r.Id,
                HotelId = r.HotelId,
                HotelName = r.Hotel.Name,
                RoomTypeId = r.RoomTypeId,
                RoomTypeName = r.RoomType.Name,
                NameOrNumber = r.NameOrNumber,
                MaxGuests = r.MaxGuests,
                BasePricePerNight = r.BasePricePerNight,
                BedType = r.BedType,
                AreaSquareMeters = r.AreaSquareMeters,
                ShortDescription = r.ShortDescription,
                ImageUrl = r.ImageUrl,
                IsActive = r.IsActive
            })
            .FirstOrDefaultAsync();

        if (room is null)
            return NotFound(new { message = "Habitación no encontrada." });

        return Ok(room);
    }

    /// <summary>
    /// Crea una nueva habitación en un hotel existente.
    /// </summary>
    /// <param name="request">Datos de la habitación a crear.</param>
    /// <returns>Resultado de la operación de creación.</returns>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SaveRoomRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == request.HotelId);
        if (!hotelExists)
            return BadRequest(new { message = "El hotel seleccionado no existe." });

        var roomTypeExists = await _db.RoomTypes.AnyAsync(rt => rt.Id == request.RoomTypeId);
        if (!roomTypeExists)
            return BadRequest(new { message = "El tipo de habitación seleccionado no existe." });

        var normalizedName = request.NameOrNumber.Trim();

        var existsSameName = await _db.Rooms.AnyAsync(r =>
            r.HotelId == request.HotelId &&
            r.NameOrNumber == normalizedName);

        if (existsSameName)
            return BadRequest(new { message = "Ya existe una habitación con ese nombre o número en ese hotel." });

        var room = new Room
        {
            HotelId = request.HotelId,
            RoomTypeId = request.RoomTypeId,
            NameOrNumber = normalizedName,
            MaxGuests = request.MaxGuests,
            BasePricePerNight = request.BasePricePerNight,
            BedType = string.IsNullOrWhiteSpace(request.BedType) ? null : request.BedType.Trim(),
            AreaSquareMeters = request.AreaSquareMeters,
            ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsActive = request.IsActive
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Habitación creada correctamente.", id = room.Id });
    }

    /// <summary>
    /// Actualiza la información de una habitación existente.
    /// </summary>
    /// <param name="id">Identificador de la habitación a modificar.</param>
    /// <param name="request">Nuevos datos de la habitación.</param>
    /// <returns>Resultado de la operación de actualización.</returns>
    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] SaveRoomRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room is null)
            return NotFound(new { message = "Habitación no encontrada." });

        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == request.HotelId);
        if (!hotelExists)
            return BadRequest(new { message = "El hotel seleccionado no existe." });

        var roomTypeExists = await _db.RoomTypes.AnyAsync(rt => rt.Id == request.RoomTypeId);
        if (!roomTypeExists)
            return BadRequest(new { message = "El tipo de habitación seleccionado no existe." });

        var normalizedName = request.NameOrNumber.Trim();

        var existsSameName = await _db.Rooms.AnyAsync(r =>
            r.Id != id &&
            r.HotelId == request.HotelId &&
            r.NameOrNumber == normalizedName);

        if (existsSameName)
            return BadRequest(new { message = "Ya existe otra habitación con ese nombre o número en ese hotel." });

        room.HotelId = request.HotelId;
        room.RoomTypeId = request.RoomTypeId;
        room.NameOrNumber = normalizedName;
        room.MaxGuests = request.MaxGuests;
        room.BasePricePerNight = request.BasePricePerNight;
        room.BedType = string.IsNullOrWhiteSpace(request.BedType) ? null : request.BedType.Trim();
        room.AreaSquareMeters = request.AreaSquareMeters;
        room.ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim();
        room.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        room.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Habitación actualizada correctamente." });
    }

    /// <summary>
    /// Desactiva una habitación para evitar su uso en operaciones activas.
    /// </summary>
    /// <param name="id">Identificador de la habitación.</param>
    /// <returns>Estado actualizado de la habitación.</returns>
    [HttpPost("{id:int}/deactivate")]
    public async Task<ActionResult> Deactivate(int id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room is null)
            return NotFound(new { message = "Habitación no encontrada." });

        if (!room.IsActive)
        {
            return Ok(new
            {
                message = "La habitación ya estaba inactiva.",
                id = room.Id,
                isActive = room.IsActive
            });
        }

        room.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Habitación desactivada correctamente.",
            id = room.Id,
            isActive = room.IsActive
        });
    }

    /// <summary>
    /// Reactiva una habitación previamente desactivada.
    /// </summary>
    /// <param name="id">Identificador de la habitación.</param>
    /// <returns>Estado actualizado de la habitación.</returns>
    [HttpPost("{id:int}/reactivate")]
    public async Task<ActionResult> Reactivate(int id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room is null)
            return NotFound(new { message = "Habitación no encontrada." });

        if (room.IsActive)
        {
            return Ok(new
            {
                message = "La habitación ya estaba activa.",
                id = room.Id,
                isActive = room.IsActive
            });
        }

        room.IsActive = true;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Habitación reactivada correctamente.",
            id = room.Id,
            isActive = room.IsActive
        });
    }
}