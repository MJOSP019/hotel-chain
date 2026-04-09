using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/ws/search")]
[Authorize(Roles = Roles.WEBSERVICE)] // ✅ SOLO agencias
public class WsSearchController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public WsSearchController(HotelChainDbContext db) => _db = db;

    public class SearchRequest
    {
        public int CityId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequest req)
    {
        // Copia exacta de tu SearchController (para no perder tiempo)
        var start = req.CheckIn.Date;
        var end = req.CheckOut.Date;

        if (end <= start)
            return BadRequest("CheckOut debe ser mayor que CheckIn.");

        var nights = Enumerable.Range(0, (end - start).Days)
            .Select(i => start.AddDays(i))
            .ToList();

        var rooms = await _db.Rooms
            .Where(r => r.IsActive)
            .Where(r => r.MaxGuests >= req.Guests)
            .Where(r => r.Hotel.CityId == req.CityId)
            .Where(r => nights.All(d =>
                _db.RoomInventories.Any(inv =>
                    inv.RoomId == r.Id &&
                    inv.Date == d &&
                    (inv.QuantityTotal - inv.QuantityReserved) > 0
                )
            ))
            .Select(r => new
            {
                r.Id,
                r.NameOrNumber,
                r.MaxGuests,
                r.BasePricePerNight,
                RoomType = r.RoomType.Name,
                Hotel = r.Hotel.Name
            })
            .ToListAsync();

        return Ok(rooms);
    }
}