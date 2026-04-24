using HotelChain.Api.Contracts;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador administrativo para la gestión de hoteles de la cadena.
/// </summary>
/// <remarks>
/// Permite listar, consultar, crear, actualizar, desactivar y reactivar hoteles.
/// Todos los endpoints de este controlador requieren rol ADMIN.
/// </remarks>
[ApiController]
[Route("api/admin/hotels")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminHotelsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia del controlador administrativo de hoteles.
    /// </summary>
    /// <param name="db">Contexto de base de datos del sistema.</param>
    public AdminHotelsController(HotelChainDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Obtiene el listado completo de hoteles registrados en el sistema.
    /// </summary>
    /// <returns>
    /// Una colección de hoteles ordenados por nombre, incluyendo ciudad y estado activo.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var hotels = await _db.Hotels
            .Include(h => h.City)
            .OrderBy(h => h.Name)
            .Select(h => new AdminHotelDto
            {
                Id = h.Id,
                Code = h.Code,
                Name = h.Name,
                Address = h.Address,
                Description = h.Description ?? "",
                MainImageUrl = h.MainImageUrl,
                ZoneInfo = h.ZoneInfo,
                Amenities = h.Amenities,
                CityId = h.CityId,
                CityName = h.City.Name,
                IsActive = h.IsActive
            })
            .ToListAsync();

        return Ok(hotels);
    }

    /// <summary>
    /// Obtiene el detalle administrativo de un hotel a partir de su identificador.
    /// </summary>
    /// <param name="id">Identificador del hotel.</param>
    /// <returns>
    /// El hotel solicitado si existe; en caso contrario, devuelve NotFound.
    /// </returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var hotel = await _db.Hotels
            .Include(h => h.City)
            .Where(h => h.Id == id)
            .Select(h => new AdminHotelDto
            {
                Id = h.Id,
                Code = h.Code,
                Name = h.Name,
                Address = h.Address,
                Description = h.Description ?? "",
                MainImageUrl = h.MainImageUrl,
                ZoneInfo = h.ZoneInfo,
                Amenities = h.Amenities,
                CityId = h.CityId,
                CityName = h.City.Name,
                IsActive = h.IsActive
            })
            .FirstOrDefaultAsync();

        if (hotel == null) return NotFound("Hotel no existe.");

        return Ok(hotel);
    }

    /// <summary>
    /// Crea un nuevo hotel en el sistema.
    /// </summary>
    /// <param name="req">Datos del hotel a crear.</param>
    /// <returns>
    /// Información básica del hotel creado.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveHotelRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest("Code es requerido.");

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name es requerido.");

        if (string.IsNullOrWhiteSpace(req.Address))
            return BadRequest("Address es requerido.");

        var cityExists = await _db.Cities.AnyAsync(c => c.Id == req.CityId);
        if (!cityExists)
            return BadRequest("CityId inválido.");

        var codeExists = await _db.Hotels.AnyAsync(h => h.Code == req.Code);
        if (codeExists)
            return BadRequest("Ya existe un hotel con ese código.");

        var hotel = new Hotel
        {
            Code = req.Code.Trim(),
            Name = req.Name.Trim(),
            Address = req.Address.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            MainImageUrl = string.IsNullOrWhiteSpace(req.MainImageUrl) ? null : req.MainImageUrl.Trim(),
            ZoneInfo = string.IsNullOrWhiteSpace(req.ZoneInfo) ? null : req.ZoneInfo.Trim(),
            Amenities = string.IsNullOrWhiteSpace(req.Amenities) ? null : req.Amenities.Trim(),
            CityId = req.CityId,
            IsActive = true
        };

        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            hotel.Id,
            hotel.Code,
            hotel.Name
        });
    }

    /// <summary>
    /// Actualiza los datos de un hotel existente.
    /// </summary>
    /// <param name="id">Identificador del hotel a actualizar.</param>
    /// <param name="req">Nuevos datos del hotel.</param>
    /// <returns>
    /// Información básica del hotel actualizado.
    /// </returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveHotelRequest req)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id);
        if (hotel == null) return NotFound("Hotel no existe.");

        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest("Code es requerido.");

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name es requerido.");

        if (string.IsNullOrWhiteSpace(req.Address))
            return BadRequest("Address es requerido.");

        var cityExists = await _db.Cities.AnyAsync(c => c.Id == req.CityId);
        if (!cityExists)
            return BadRequest("CityId inválido.");

        var codeExists = await _db.Hotels.AnyAsync(h => h.Code == req.Code && h.Id != id);
        if (codeExists)
            return BadRequest("Ya existe otro hotel con ese código.");

        hotel.Code = req.Code.Trim();
        hotel.Name = req.Name.Trim();
        hotel.Address = req.Address.Trim();
        hotel.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        hotel.MainImageUrl = string.IsNullOrWhiteSpace(req.MainImageUrl) ? null : req.MainImageUrl.Trim();
        hotel.ZoneInfo = string.IsNullOrWhiteSpace(req.ZoneInfo) ? null : req.ZoneInfo.Trim();
        hotel.Amenities = string.IsNullOrWhiteSpace(req.Amenities) ? null : req.Amenities.Trim();
        hotel.CityId = req.CityId;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            hotel.Id,
            hotel.Code,
            hotel.Name
        });
    }

    /// <summary>
    /// Desactiva un hotel para evitar que participe en búsquedas y operaciones activas.
    /// </summary>
    /// <param name="id">Identificador del hotel a desactivar.</param>
    /// <returns>
    /// Estado actual del hotel después de la operación.
    /// </returns>
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id);
        if (hotel == null)
            return NotFound("Hotel no existe.");

        if (!hotel.IsActive)
        {
            return Ok(new
            {
                hotel.Id,
                hotel.Code,
                hotel.Name,
                hotel.IsActive
            });
        }

        hotel.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            hotel.Id,
            hotel.Code,
            hotel.Name,
            hotel.IsActive
        });
    }

    /// <summary>
    /// Reactiva un hotel previamente desactivado.
    /// </summary>
    /// <param name="id">Identificador del hotel a reactivar.</param>
    /// <returns>
    /// Estado actual del hotel después de la operación.
    /// </returns>
    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id);
        if (hotel == null)
            return NotFound("Hotel no existe.");

        if (hotel.IsActive)
        {
            return Ok(new
            {
                hotel.Id,
                hotel.Code,
                hotel.Name,
                hotel.IsActive
            });
        }

        hotel.IsActive = true;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            hotel.Id,
            hotel.Code,
            hotel.Name,
            hotel.IsActive
        });
    }
}