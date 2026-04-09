using HotelChain.Api.Contracts;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/public/room-types")]
public class RoomTypesController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public RoomTypesController(HotelChainDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoomTypeDto>>> Get()
    {
        var roomTypes = await _db.RoomTypes
            .OrderBy(x => x.Name)
            .Select(x => new RoomTypeDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();

        return Ok(roomTypes);
    }
}