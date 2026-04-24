namespace HotelChain.Domain.Entities;

/// <summary>
/// Inventario comercial diario por hotel y tipo de habitación.
/// </summary>
/// <remarks>
/// Esta entidad representa la capa comercial del inventario. Permite vender
/// por tipo de habitación sin amarrar la reserva desde el inicio a una
/// habitación física específica.
/// 
/// Además de las cantidades base y reservadas, concentra reglas comerciales
/// típicas de un motor hotelero real como cierre de ventas, restricciones de
/// llegada/salida y longitud mínima o máxima de estancia.
/// </remarks>
public class RoomTypeInventory
{
    public int Id { get; set; }

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public DateTime Date { get; set; }

    /// <summary>
    /// Capacidad comercial total disponible para ese tipo de habitación en el hotel y fecha.
    /// </summary>
    public int QuantityTotal { get; set; }

    /// <summary>
    /// Cantidad ya comprometida por reservas comerciales para ese tipo de habitación.
    /// </summary>
    public int QuantityReserved { get; set; }

    /// <summary>
    /// Cierra por completo la venta comercial del tipo de habitación en la fecha.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Indica que no se permite llegar en esta fecha.
    /// </summary>
    public bool ClosedToArrival { get; set; }

    /// <summary>
    /// Indica que no se permite salir en esta fecha.
    /// </summary>
    public bool ClosedToDeparture { get; set; }

    /// <summary>
    /// Mínimo de noches exigido para estancias que incluyan esta fecha.
    /// </summary>
    public int? MinLengthOfStay { get; set; }

    /// <summary>
    /// Máximo de noches permitido para estancias que incluyan esta fecha.
    /// </summary>
    public int? MaxLengthOfStay { get; set; }
}
