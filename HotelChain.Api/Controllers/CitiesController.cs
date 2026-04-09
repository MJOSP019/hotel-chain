using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/public/cities")]
public class CitiesController : ControllerBase
{
    private readonly HotelChainDbContext _db;
    public CitiesController(HotelChainDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var cities = await _db.Cities
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.CountryCode })
            .ToListAsync();

        return Ok(cities);
    }
}