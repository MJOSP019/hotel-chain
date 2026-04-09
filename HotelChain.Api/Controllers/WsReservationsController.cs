using System.Security.Claims;
using HotelChain.Infrastructure.Auth;
using HotelChain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/ws/reservations")]
[Authorize(Roles = Roles.WEBSERVICE)]
public class WsReservationsController : ControllerBase
{
    private readonly HotelChainDbContext _db;

    public WsReservationsController(HotelChainDbContext db) => _db = db;

    public class CreateWsReservationRequest
    {
        public int HotelId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }
        public List<int> RoomIds { get; set; } = new();

        // Datos del cliente final (recomendado por doc para agencia)
        public string CustomerFirstName { get; set; } = "";
        public string CustomerLastName { get; set; } = "";
        public string CustomerNationality { get; set; } = "";
        public DateTime CustomerBirthDate { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWsReservationRequest req)
    {
        // Identidad de la agencia desde JWT:
        var agencyUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ✅ Aquí lo mínimo indispensable:
        // - Llamar tu lógica actual de crear reserva (la que pone PENDING y reserva inventario)
        // - Guardar que fue creada por agencia (en audit Actor o en un campo CreatedByUserId si ya lo tienes)

        return Ok(new
        {
            message = "Implementación: llama tu CreateReservation actual aquí",
            agencyUserId
        });
    }
}