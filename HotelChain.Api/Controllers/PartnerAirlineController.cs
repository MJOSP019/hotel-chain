using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/partner-airline")]
public class PartnerAirlineController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public PartnerAirlineController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("flights")]
    public async Task<IActionResult> SearchFlights(
        [FromQuery] string originCode = "GUA",
        [FromQuery] string destinationCode = "MIA",
        [FromQuery] string departureDate = "2026-05-10",
        [FromQuery] int passengers = 2,
        [FromQuery] string seatType = "TURISTA",
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? flightType = null,
        [FromQuery] decimal? minRating = null)
    {
        if (string.IsNullOrWhiteSpace(originCode))
            return BadRequest("Debe enviar el código de origen.");

        if (string.IsNullOrWhiteSpace(destinationCode))
            return BadRequest("Debe enviar el código de destino.");

        if (string.IsNullOrWhiteSpace(departureDate))
            return BadRequest("Debe enviar la fecha de salida.");

        if (passengers <= 0)
            return BadRequest("La cantidad de pasajeros debe ser mayor a 0.");

        var client = CreateAirlineClient();
        var baseUrl = GetAirlineBaseUrl();

        var request = new
        {
            originCode,
            destinationCode,
            departureDate,
            passengers,
            seatType,
            roundTrip = false,
            minPrice,
            maxPrice,
            flightType,
            minRating,
            source = "INTEGRATION",
            agencyCode = GetPartnerCode()
        };

        try
        {
            var response = await client.PostAsJsonAsync($"{baseUrl}/integration/flights/search", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new
                {
                    message = "La aerolínea aliada respondió con error.",
                    detail = error
                });
            }

            var flights = await response.Content.ReadFromJsonAsync<List<PartnerAirlineFlightDto>>(JsonOptions());
            return Ok(flights ?? new List<PartnerAirlineFlightDto>());
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new
            {
                message = "No fue posible conectar con el servicio de aerolínea.",
                detail = ex.Message
            });
        }
        catch (TaskCanceledException ex)
        {
            return StatusCode(504, new
            {
                message = "La conexión con la aerolínea tardó demasiado.",
                detail = ex.Message
            });
        }
    }

    [HttpPost("reservations")]
    public async Task<IActionResult> CreateReservation([FromBody] PartnerAirlineReservationRequest request)
    {
        if (request.FlightId <= 0)
            return BadRequest("Debe seleccionar un vuelo válido.");

        if (request.Passengers <= 0)
            return BadRequest("La cantidad de pasajeros debe ser mayor a 0.");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return BadRequest("Debe ingresar nombres del pasajero.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest("Debe ingresar apellidos del pasajero.");

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Debe ingresar correo del pasajero.");

        var client = CreateAirlineClient();
        var baseUrl = GetAirlineBaseUrl();

        var airlineRequest = new
        {
            flightId = request.FlightId,
            firstName = request.FirstName,
            lastName = request.LastName,
            email = request.Email,
            passengers = request.Passengers,
            dateOfBirth = request.DateOfBirth,
            nationality = request.Nationality,
            cardNumber = request.CardNumber,
            cvv = request.Cvv,
            cardHolderName = request.CardHolderName,
            billingAddress = request.BillingAddress,
            partnerCode = GetPartnerCode()
        };

        try
        {
            var response = await client.PostAsJsonAsync($"{baseUrl}/integration/reservations", airlineRequest);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new
                {
                    message = "La aerolínea aliada no pudo crear la reserva.",
                    detail = error
                });
            }

            var reservation = await response.Content.ReadFromJsonAsync<PartnerAirlineReservationResponse>(JsonOptions());
            return Ok(reservation);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new
            {
                message = "No fue posible conectar con el servicio de aerolínea.",
                detail = ex.Message
            });
        }
        catch (TaskCanceledException ex)
        {
            return StatusCode(504, new
            {
                message = "La conexión con la aerolínea tardó demasiado.",
                detail = ex.Message
            });
        }
    }

    [HttpGet("reservations/{code}")]
    public async Task<IActionResult> GetReservation(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Debe enviar código de reserva.");

        var client = CreateAirlineClient();
        var baseUrl = GetAirlineBaseUrl();

        try
        {
            var response = await client.GetAsync($"{baseUrl}/reservations/{Uri.EscapeDataString(code)}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new
                {
                    message = "La aerolínea aliada no pudo consultar la reserva.",
                    detail = error
                });
            }

            var reservation = await response.Content.ReadFromJsonAsync<PartnerAirlineReservationDetailDto>(JsonOptions());
            return Ok(reservation);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new
            {
                message = "No fue posible conectar con el servicio de aerolínea.",
                detail = ex.Message
            });
        }
    }

    [HttpPost("reservations/{code}/cancel")]
    public async Task<IActionResult> CancelReservation(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Debe enviar código de reserva.");

        var client = CreateAirlineClient();
        var baseUrl = GetAirlineBaseUrl();

        try
        {
            var response = await client.PostAsync($"{baseUrl}/reservations/{Uri.EscapeDataString(code)}/cancel", null);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new
                {
                    message = "La aerolínea aliada no pudo cancelar la reserva.",
                    detail = error
                });
            }

            var result = await response.Content.ReadFromJsonAsync<PartnerAirlineCancelReservationResponse>(JsonOptions());
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new
            {
                message = "No fue posible conectar con el servicio de aerolínea.",
                detail = ex.Message
            });
        }
    }

    private HttpClient CreateAirlineClient()
    {
        var client = _httpClientFactory.CreateClient();
        var apiKey = _configuration["AirlineApi:ApiKey"] ?? "HOTELCHAIN-DEMO-KEY-2026";
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private string GetAirlineBaseUrl()
    {
        return _configuration["AirlineApi:BaseUrl"] ?? "http://localhost:7070/api/airline";
    }

    private string GetPartnerCode()
    {
        return _configuration["AirlineApi:PartnerCode"] ?? "HOTELCHAIN";
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
}

public class PartnerAirlineFlightDto
{
    public int Id { get; set; }
    public string FlightCode { get; set; } = string.Empty;
    public string OriginCode { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string DepartureTime { get; set; } = string.Empty;
    public string ArrivalTime { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public string FlightType { get; set; } = string.Empty;
    public string? ScaleCity { get; set; }
    public decimal Price { get; set; }
    public int AvailableSeats { get; set; }
    public int TotalSeats { get; set; }
    public int ReservedSeats { get; set; }
    public bool Active { get; set; }
    public decimal RatingAverage { get; set; }
    public string BaggageInfo { get; set; } = string.Empty;
    public string Aircraft { get; set; } = string.Empty;
    public string Gate { get; set; } = string.Empty;
    public string? CancelReason { get; set; }
}

public class PartnerAirlineReservationRequest
{
    public int FlightId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Passengers { get; set; }
    public string DateOfBirth { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
}

public class PartnerAirlineReservationResponse
{
    public string ReservationCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Passengers { get; set; }
    public string FlightCode { get; set; } = string.Empty;
}

public class PartnerAirlineReservationDetailDto
{
    public string ReservationCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Passengers { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string FlightCode { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string DepartureTime { get; set; } = string.Empty;
    public string ArrivalTime { get; set; } = string.Empty;
}

public class PartnerAirlineCancelReservationResponse
{
    public string ReservationCode { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public int ReleasedSeats { get; set; }
    public string Message { get; set; } = string.Empty;
}
