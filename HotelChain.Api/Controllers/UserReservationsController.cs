using System.Security.Claims;
using HotelChain.Api.DTOs;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/user/reservations")]
[Authorize]
public class UserReservationsController : ControllerBase
{
    private readonly HotelChainDbContext _context;

    public UserReservationsController(HotelChainDbContext context)
    {
        _context = context;
    }

    [HttpGet]
public async Task<IActionResult> GetMyReservations()
{
    var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (!Guid.TryParse(userIdValue, out var userId))
        return Unauthorized();

    var reservations = await _context.Reservations
        .Include(r => r.Hotel)
        .Where(r => r.UserId == userId)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new ReservationHistoryDto
        {
            Code = r.Code,
            Hotel = r.Hotel.Name,
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Guests = r.Guests,
            TotalAmount = r.TotalAmount,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        })
        .ToListAsync();

    return Ok(reservations);
}
}