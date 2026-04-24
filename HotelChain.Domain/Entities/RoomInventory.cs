namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa el inventario diario de una habitación.
/// </summary>
/// <remarks>
/// Este registro permite controlar la disponibilidad operativa de una habitación
/// física para una fecha específica, indicando cantidad total y cantidad reservada.
/// En este sistema la capa física normalmente trabaja con 0 o 1 por día.
/// </remarks>
public class RoomInventory
{
    /// <summary>
    /// Identificador interno del inventario.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Identificador de la habitación asociada.
    /// </summary>
    public int RoomId { get; set; }

    /// <summary>
    /// Habitación asociada al inventario.
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// Fecha a la que corresponde el inventario.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Cantidad total disponible operativamente para la fecha.
    /// </summary>
    public int QuantityTotal { get; set; }

    /// <summary>
    /// Cantidad ya reservada para la fecha.
    /// </summary>
    public int QuantityReserved { get; set; }
}
