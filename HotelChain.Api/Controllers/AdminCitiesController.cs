using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador administrativo para gestionar el catalogo de ciudades/destinos de la cadena hotelera.
/// </summary>
/// <remarks>
/// En un hotel real, la ciudad funciona como un catalogo maestro de destinos: primero se registra el destino
/// y luego se asocian hoteles a ese destino. No se elimina fisicamente para no romper historial de hoteles,
/// busquedas o reservaciones.
/// </remarks>
[ApiController]
[Route("api/admin/cities")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminCitiesController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public AdminCitiesController(HotelChainDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cities = await _db.Cities
            .OrderBy(c => c.CountryCode)
            .ThenBy(c => c.Name)
            .Select(c => new AdminCityDto
            {
                Id = c.Id,
                Name = c.Name,
                CountryCode = c.CountryCode,
                HotelsCount = c.Hotels.Count,
                ActiveHotelsCount = c.Hotels.Count(h => h.IsActive)
            })
            .ToListAsync();

        return Ok(cities);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCityRequest req)
    {
        var normalizedName = NormalizeName(req.Name);
        var normalizedCountryCode = NormalizeCountryCode(req.CountryCode);

        var validationError = ValidateCity(normalizedName, normalizedCountryCode);
        if (validationError != null)
            return BadRequest(validationError);

        var duplicate = await _db.Cities.AnyAsync(c =>
            c.Name.ToLower() == normalizedName.ToLower()
            && c.CountryCode.ToLower() == normalizedCountryCode.ToLower());

        if (duplicate)
            return BadRequest("Ya existe una ciudad con ese nombre y pais.");

        var city = new City
        {
            Name = normalizedName,
            CountryCode = normalizedCountryCode
        };

        _db.Cities.Add(city);
        await _db.SaveChangesAsync();

        return Ok(new AdminCityDto
        {
            Id = city.Id,
            Name = city.Name,
            CountryCode = city.CountryCode,
            HotelsCount = 0,
            ActiveHotelsCount = 0
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveCityRequest req)
    {
        var city = await _db.Cities.FirstOrDefaultAsync(c => c.Id == id);
        if (city == null)
            return NotFound("Ciudad no existe.");

        var normalizedName = NormalizeName(req.Name);
        var normalizedCountryCode = NormalizeCountryCode(req.CountryCode);

        var validationError = ValidateCity(normalizedName, normalizedCountryCode);
        if (validationError != null)
            return BadRequest(validationError);

        var duplicate = await _db.Cities.AnyAsync(c =>
            c.Id != id
            && c.Name.ToLower() == normalizedName.ToLower()
            && c.CountryCode.ToLower() == normalizedCountryCode.ToLower());

        if (duplicate)
            return BadRequest("Ya existe otra ciudad con ese nombre y pais.");

        city.Name = normalizedName;
        city.CountryCode = normalizedCountryCode;

        await _db.SaveChangesAsync();

        return Ok(new AdminCityDto
        {
            Id = city.Id,
            Name = city.Name,
            CountryCode = city.CountryCode,
            HotelsCount = await _db.Hotels.CountAsync(h => h.CityId == city.Id),
            ActiveHotelsCount = await _db.Hotels.CountAsync(h => h.CityId == city.Id && h.IsActive)
        });
    }

    private static string NormalizeName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeCountryCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    private static string? ValidateCity(string name, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "El nombre de la ciudad es obligatorio.";

        if (name.Length > 100)
            return "El nombre de la ciudad no puede exceder 100 caracteres.";

        if (string.IsNullOrWhiteSpace(countryCode))
            return "El codigo de pais es obligatorio.";

        if (countryCode.Length > 5)
            return "El codigo de pais no puede exceder 5 caracteres.";

        return null;
    }

    public class SaveCityRequest
    {
        public string Name { get; set; } = "";
        public string CountryCode { get; set; } = "";
    }

    public class AdminCityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public int HotelsCount { get; set; }
        public int ActiveHotelsCount { get; set; }
    }
}
