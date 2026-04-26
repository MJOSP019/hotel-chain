using HotelChain.Api.DTOs.Admin;
using HotelChain.Domain.Entities;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador administrativo para la gestión de habitaciones físicas de los hoteles de la cadena.
/// </summary>
/// <remarks>
/// Permite listar, consultar, crear, crear en lote, actualizar, desactivar y reactivar habitaciones.
/// Todos los endpoints requieren autenticación con rol ADMIN.
/// </remarks>
[ApiController]
[Route("api/admin/rooms")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminRoomsController : ControllerBase
{
    private const int InventoryHorizonMonths = 12;
    private const int MaxBulkRooms = 100;
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
    /// Obtiene el listado completo de habitaciones registradas, opcionalmente filtrado por hotel.
    /// Se mantiene para compatibilidad con pantallas existentes.
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
            .ThenBy(r => r.RoomType.Name)
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
    /// Obtiene habitaciones paginadas para administración operativa.
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedRoomsResponse>> GetPaged(
        [FromQuery] int? hotelId,
        [FromQuery] int? roomTypeId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);

        var query = _db.Rooms
            .Include(r => r.Hotel)
            .Include(r => r.RoomType)
            .AsQueryable();

        if (hotelId.HasValue && hotelId.Value > 0)
            query = query.Where(r => r.HotelId == hotelId.Value);

        if (roomTypeId.HasValue && roomTypeId.Value > 0)
            query = query.Where(r => r.RoomTypeId == roomTypeId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(r =>
                r.NameOrNumber.Contains(normalizedSearch) ||
                r.Hotel.Name.Contains(normalizedSearch) ||
                r.RoomType.Name.Contains(normalizedSearch) ||
                (r.BedType != null && r.BedType.Contains(normalizedSearch)));
        }

        var activeCount = await query.CountAsync(r => r.IsActive);
        var inactiveCount = await query.CountAsync(r => !r.IsActive);

        if (!includeInactive)
            query = query.Where(r => r.IsActive);

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (page > totalPages)
            page = totalPages;

        var items = await query
            .OrderBy(r => r.Hotel.Name)
            .ThenBy(r => r.RoomType.Name)
            .ThenBy(r => r.NameOrNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return Ok(new PagedRoomsResponse
        {
            Items = items,
            TotalCount = totalCount,
            ActiveCount = activeCount,
            InactiveCount = inactiveCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>
    /// Obtiene un resumen operativo de habitaciones agrupado por hotel y tipo.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<RoomTypeOperationalSummaryDto>>> GetSummary([FromQuery] int? hotelId)
    {
        var query = _db.Rooms
            .Include(r => r.Hotel)
            .Include(r => r.RoomType)
            .AsQueryable();

        if (hotelId.HasValue && hotelId.Value > 0)
            query = query.Where(r => r.HotelId == hotelId.Value);

        var rows = await query
            .Select(r => new
            {
                r.HotelId,
                HotelName = r.Hotel.Name,
                r.RoomTypeId,
                RoomTypeName = r.RoomType.Name,
                r.IsActive,
                r.BasePricePerNight,
                r.MaxGuests,
                r.BedType
            })
            .ToListAsync();

        var summary = rows
            .GroupBy(x => new { x.HotelId, x.HotelName, x.RoomTypeId, x.RoomTypeName })
            .OrderBy(g => g.Key.HotelName)
            .ThenBy(g => g.Key.RoomTypeName)
            .Select(g => new RoomTypeOperationalSummaryDto
            {
                HotelId = g.Key.HotelId,
                HotelName = g.Key.HotelName,
                RoomTypeId = g.Key.RoomTypeId,
                RoomTypeName = g.Key.RoomTypeName,
                ActiveRooms = g.Count(x => x.IsActive),
                InactiveRooms = g.Count(x => !x.IsActive),
                TotalRooms = g.Count(),
                MinPrice = g.Min(x => x.BasePricePerNight),
                MaxPrice = g.Max(x => x.BasePricePerNight),
                MaxGuests = g.Max(x => x.MaxGuests),
                BedTypes = string.Join(", ", g
                    .Select(x => x.BedType)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .Distinct()
                    .OrderBy(x => x))
            })
            .ToList();

        return Ok(summary);
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
    /// Crea una nueva habitación física en un hotel existente.
    /// </summary>
    /// <param name="request">Datos de la habitación a crear.</param>
    /// <returns>Resultado de la operación de creación.</returns>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SaveRoomRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var validation = await ValidateRoomRequest(request);
        if (validation is not null)
            return validation;

        var normalizedName = request.NameOrNumber.Trim();

        var existsSameName = await _db.Rooms.AnyAsync(r =>
            r.HotelId == request.HotelId &&
            r.NameOrNumber == normalizedName);

        if (existsSameName)
            return BadRequest(new { message = "Ya existe una habitación física con ese nombre o número en ese hotel." });

        var room = new Room
        {
            HotelId = request.HotelId,
            RoomTypeId = request.RoomTypeId,
            NameOrNumber = normalizedName,
            MaxGuests = request.MaxGuests,
            BasePricePerNight = request.BasePricePerNight,
            BedType = NormalizeOptionalText(request.BedType),
            AreaSquareMeters = request.AreaSquareMeters,
            ShortDescription = NormalizeOptionalText(request.ShortDescription),
            ImageUrl = NormalizeOptionalText(request.ImageUrl),
            IsActive = request.IsActive
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        var startDate = DateTime.Today;
        var endDate = startDate.AddMonths(InventoryHorizonMonths);
        await EnsureOperationalInventoryForRoom(room, startDate, endDate, room.IsActive ? 1 : 0, overwriteExisting: false);
        await RebuildCommercialInventoryForRange(room.HotelId, room.RoomTypeId, GetDates(startDate, endDate));

        return Ok(new
        {
            message = "Habitación creada correctamente y sincronizada con el inventario comercial.",
            id = room.Id
        });
    }

    /// <summary>
    /// Crea varias habitaciones físicas del mismo hotel y tipo usando un rango de numeración.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult> BulkCreate([FromBody] BulkCreateRoomsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.StartNumber <= 0 || request.EndNumber <= 0)
            return BadRequest(new { message = "El rango de habitaciones debe ser mayor a 0." });

        if (request.EndNumber < request.StartNumber)
            return BadRequest(new { message = "El número final no puede ser menor que el número inicial." });

        var totalRequested = request.EndNumber - request.StartNumber + 1;
        if (totalRequested > MaxBulkRooms)
            return BadRequest(new { message = $"Por seguridad, solo se pueden crear hasta {MaxBulkRooms} habitaciones por lote." });

        var validation = await ValidateBulkRequest(request);
        if (validation is not null)
            return validation;

        var prefix = NormalizeForRoomCode(request.Prefix);
        var suffix = NormalizeForRoomCode(request.Suffix);

        var generatedNames = Enumerable
            .Range(request.StartNumber, totalRequested)
            .Select(number => $"{prefix}{number}{suffix}".Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (generatedNames.Count == 0)
            return BadRequest(new { message = "No se pudo generar ningún número de habitación." });

        var existingNames = await _db.Rooms
            .Where(r => r.HotelId == request.HotelId && generatedNames.Contains(r.NameOrNumber))
            .Select(r => r.NameOrNumber)
            .ToListAsync();

        if (existingNames.Count > 0 && !request.SkipExisting)
        {
            return BadRequest(new
            {
                message = "Ya existen habitaciones físicas con algunos números del rango.",
                existingRooms = existingNames.OrderBy(x => x).ToList()
            });
        }

        var existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var namesToCreate = generatedNames
            .Where(name => !existingSet.Contains(name))
            .ToList();

        if (namesToCreate.Count == 0)
        {
            return Ok(new
            {
                message = "No se creó ninguna habitación porque todas las habitaciones del rango ya existían.",
                created = 0,
                skipped = generatedNames.Count
            });
        }

        var rooms = namesToCreate.Select(name => new Room
        {
            HotelId = request.HotelId,
            RoomTypeId = request.RoomTypeId,
            NameOrNumber = name,
            MaxGuests = request.MaxGuests,
            BasePricePerNight = request.BasePricePerNight,
            BedType = NormalizeOptionalText(request.BedType),
            AreaSquareMeters = request.AreaSquareMeters,
            ShortDescription = NormalizeOptionalText(request.ShortDescription),
            ImageUrl = NormalizeOptionalText(request.ImageUrl),
            IsActive = request.IsActive
        }).ToList();

        _db.Rooms.AddRange(rooms);
        await _db.SaveChangesAsync();

        var startDate = DateTime.Today;
        var endDate = startDate.AddMonths(InventoryHorizonMonths);
        var quantityTotal = request.IsActive ? 1 : 0;

        foreach (var room in rooms)
        {
            await EnsureOperationalInventoryForRoom(room, startDate, endDate, quantityTotal, overwriteExisting: false);
        }

        await RebuildCommercialInventoryForRange(request.HotelId, request.RoomTypeId, GetDates(startDate, endDate));

        return Ok(new
        {
            message = $"Lote creado correctamente. Habitaciones creadas: {rooms.Count}. Omitidas por existir: {generatedNames.Count - rooms.Count}.",
            created = rooms.Count,
            skipped = generatedNames.Count - rooms.Count,
            rooms = rooms.Select(r => r.NameOrNumber).OrderBy(x => x).ToList()
        });
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

        var validation = await ValidateRoomRequest(request);
        if (validation is not null)
            return validation;

        var normalizedName = request.NameOrNumber.Trim();

        var existsSameName = await _db.Rooms.AnyAsync(r =>
            r.Id != id &&
            r.HotelId == request.HotelId &&
            r.NameOrNumber == normalizedName);

        if (existsSameName)
            return BadRequest(new { message = "Ya existe otra habitación física con ese nombre o número en ese hotel." });

        var previousHotelId = room.HotelId;
        var previousRoomTypeId = room.RoomTypeId;
        var wasActive = room.IsActive;

        room.HotelId = request.HotelId;
        room.RoomTypeId = request.RoomTypeId;
        room.NameOrNumber = normalizedName;
        room.MaxGuests = request.MaxGuests;
        room.BasePricePerNight = request.BasePricePerNight;
        room.BedType = NormalizeOptionalText(request.BedType);
        room.AreaSquareMeters = request.AreaSquareMeters;
        room.ShortDescription = NormalizeOptionalText(request.ShortDescription);
        room.ImageUrl = NormalizeOptionalText(request.ImageUrl);
        room.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        var startDate = DateTime.Today;
        var endDate = startDate.AddMonths(InventoryHorizonMonths);
        var dates = GetDates(startDate, endDate);

        if (room.IsActive && !wasActive)
        {
            await EnsureOperationalInventoryForRoom(room, startDate, endDate, 1, overwriteExisting: true);
        }
        else if (!room.IsActive && wasActive)
        {
            await EnsureOperationalInventoryForRoom(room, startDate, endDate, 0, overwriteExisting: true);
        }
        else if (room.IsActive)
        {
            await EnsureOperationalInventoryForRoom(room, startDate, endDate, 1, overwriteExisting: false);
        }

        if (previousHotelId != room.HotelId || previousRoomTypeId != room.RoomTypeId)
        {
            await RebuildCommercialInventoryForRange(previousHotelId, previousRoomTypeId, dates);
        }

        await RebuildCommercialInventoryForRange(room.HotelId, room.RoomTypeId, dates);

        return Ok(new { message = "Habitación actualizada correctamente y sincronizada con el inventario comercial." });
    }

    /// <summary>
    /// Desactiva una habitación para evitar su venta futura.
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

        var startDate = DateTime.Today;
        var endDate = startDate.AddMonths(InventoryHorizonMonths);
        var dates = GetDates(startDate, endDate);

        await EnsureOperationalInventoryForRoom(room, startDate, endDate, 0, overwriteExisting: true);
        await RebuildCommercialInventoryForRange(room.HotelId, room.RoomTypeId, dates);

        return Ok(new
        {
            message = "Habitación desactivada correctamente y retirada del inventario comercial futuro.",
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

        var startDate = DateTime.Today;
        var endDate = startDate.AddMonths(InventoryHorizonMonths);
        var dates = GetDates(startDate, endDate);

        await EnsureOperationalInventoryForRoom(room, startDate, endDate, 1, overwriteExisting: true);
        await RebuildCommercialInventoryForRange(room.HotelId, room.RoomTypeId, dates);

        return Ok(new
        {
            message = "Habitación reactivada correctamente y agregada al inventario comercial futuro.",
            id = room.Id,
            isActive = room.IsActive
        });
    }

    private async Task<ActionResult?> ValidateRoomRequest(SaveRoomRequest request)
    {
        if (request.HotelId <= 0)
            return BadRequest(new { message = "Debe seleccionar un hotel." });

        if (request.RoomTypeId <= 0)
            return BadRequest(new { message = "Debe seleccionar un tipo de habitación." });

        if (string.IsNullOrWhiteSpace(request.NameOrNumber))
            return BadRequest(new { message = "El nombre o número de habitación es obligatorio." });

        if (request.MaxGuests <= 0)
            return BadRequest(new { message = "La cantidad máxima de huéspedes debe ser mayor a 0." });

        if (request.BasePricePerNight <= 0)
            return BadRequest(new { message = "El precio base por noche debe ser mayor a 0." });

        if (request.AreaSquareMeters.HasValue && request.AreaSquareMeters.Value <= 0)
            return BadRequest(new { message = "El área en metros cuadrados debe ser mayor a 0." });

        var hotelExists = await _db.Hotels.AnyAsync(h => h.Id == request.HotelId);
        if (!hotelExists)
            return BadRequest(new { message = "El hotel seleccionado no existe." });

        var roomTypeExists = await _db.RoomTypes.AnyAsync(rt => rt.Id == request.RoomTypeId);
        if (!roomTypeExists)
            return BadRequest(new { message = "El tipo de habitación seleccionado no existe." });

        return null;
    }

    private async Task<ActionResult?> ValidateBulkRequest(BulkCreateRoomsRequest request)
    {
        var singleRoomValidation = await ValidateRoomRequest(new SaveRoomRequest
        {
            HotelId = request.HotelId,
            RoomTypeId = request.RoomTypeId,
            NameOrNumber = request.StartNumber.ToString(),
            MaxGuests = request.MaxGuests,
            BasePricePerNight = request.BasePricePerNight,
            BedType = request.BedType,
            AreaSquareMeters = request.AreaSquareMeters,
            ShortDescription = request.ShortDescription,
            ImageUrl = request.ImageUrl,
            IsActive = request.IsActive
        });

        return singleRoomValidation;
    }

    private async Task EnsureOperationalInventoryForRoom(
        Room room,
        DateTime startDate,
        DateTime endDate,
        int quantityTotal,
        bool overwriteExisting)
    {
        var dates = GetDates(startDate, endDate);
        var existingRows = await _db.RoomInventories
            .Where(x => x.RoomId == room.Id && x.Date >= startDate.Date && x.Date <= endDate.Date)
            .ToListAsync();

        var existingByDate = existingRows.ToDictionary(x => x.Date.Date);

        foreach (var date in dates)
        {
            if (existingByDate.TryGetValue(date, out var existing))
            {
                if (!overwriteExisting)
                    continue;

                existing.QuantityTotal = Math.Max(quantityTotal, existing.QuantityReserved);
                continue;
            }

            _db.RoomInventories.Add(new RoomInventory
            {
                RoomId = room.Id,
                Date = date,
                QuantityTotal = quantityTotal,
                QuantityReserved = 0
            });
        }

        await _db.SaveChangesAsync();
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

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeForRoomCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public class PagedRoomsResponse
{
    public List<AdminRoomDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class RoomTypeOperationalSummaryDto
{
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public int RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public int ActiveRooms { get; set; }
    public int InactiveRooms { get; set; }
    public int TotalRooms { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public int MaxGuests { get; set; }
    public string BedTypes { get; set; } = string.Empty;
}
