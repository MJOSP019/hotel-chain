namespace HotelChain.Web.Models;

public class ReservationCartItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public int HotelId { get; set; }
    public string HotelName { get; set; } = "";

    public int RoomTypeId { get; set; }
    public string RoomType { get; set; } = "";

    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }

    public int Guests { get; set; }
    public int Quantity { get; set; } = 1;

    public decimal PricePerNight { get; set; }
    public int MaxGuests { get; set; }
    public int AvailableUnits { get; set; }

    public string? ImageUrl { get; set; }
    public string? BedType { get; set; }
    public decimal? AreaSquareMeters { get; set; }

    public int Nights
    {
        get
        {
            var nights = (CheckOut.Date - CheckIn.Date).Days;
            return Math.Max(1, nights);
        }
    }

    public decimal Subtotal => PricePerNight * Nights * Quantity;
}