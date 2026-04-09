using HotelChain.Domain.Entities;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace HotelChain.Api.Services;

public static class ReservationPdfGenerator
{
    public static byte[] Generate(Reservation res)
    {
        var doc = new PdfDocument();
        doc.Info.Title = $"Reserva {res.Code}";

        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;

        using var gfx = XGraphics.FromPdfPage(page);

        var fontTitle = new XFont("Arial", 18, XFontStyle.Bold);
        var fontH = new XFont("Arial", 11, XFontStyle.Bold);
        var font = new XFont("Arial", 11, XFontStyle.Regular);
        var fontSmall = new XFont("Arial", 9, XFontStyle.Regular);

        double x = 40;
        double y = 45;
        double line = 18;

        // Encabezado
        gfx.DrawString("Confirmación de Reservación", fontTitle, XBrushes.Black, new XRect(x, y, page.Width - 80, 30), XStringFormats.TopLeft);
        y += 35;

        gfx.DrawString($"Código: {res.Code}", fontH, XBrushes.Black, x, y); y += line;
        gfx.DrawString($"Estado: {res.Status}", font, XBrushes.Black, x, y); y += line;

        // Hotel + Fechas
        var hotelName = res.Hotel?.Name ?? $"HotelId: {res.HotelId}";
        gfx.DrawString($"Hotel: {hotelName}", font, XBrushes.Black, x, y); y += line;

        gfx.DrawString($"Check-in: {res.CheckIn:yyyy-MM-dd}", font, XBrushes.Black, x, y); y += line;
        gfx.DrawString($"Check-out: {res.CheckOut:yyyy-MM-dd}", font, XBrushes.Black, x, y); y += line;
        gfx.DrawString($"Huéspedes: {res.Guests}", font, XBrushes.Black, x, y); y += line;

        y += 10;
        gfx.DrawLine(XPens.Black, x, y, page.Width - 40, y);
        y += 18;

        // Tabla simple: habitaciones
        gfx.DrawString("Detalle", fontH, XBrushes.Black, x, y); y += line;

        // Headers
        double col1 = x;          // Habitación
        double col2 = x + 220;    // Precio/noche
        double col3 = x + 340;    // Noches
        double col4 = x + 420;    // Subtotal

        gfx.DrawString("Habitación", fontH, XBrushes.Black, col1, y);
        gfx.DrawString("Precio/Noche", fontH, XBrushes.Black, col2, y);
        gfx.DrawString("Noches", fontH, XBrushes.Black, col3, y);
        gfx.DrawString("Subtotal", fontH, XBrushes.Black, col4, y);
        y += 8;
        gfx.DrawLine(XPens.Gray, x, y, page.Width - 40, y);
        y += 14;

        foreach (var rr in res.Rooms)
        {
            var roomLabel = rr.Room?.NameOrNumber ?? rr.RoomId.ToString();
            gfx.DrawString(roomLabel, font, XBrushes.Black, col1, y);
            gfx.DrawString(rr.PricePerNight.ToString("0.00"), font, XBrushes.Black, col2, y);
            gfx.DrawString(rr.Nights.ToString(), font, XBrushes.Black, col3, y);
            gfx.DrawString(rr.Subtotal.ToString("0.00"), font, XBrushes.Black, col4, y);

            y += line;

            // Si te pasas de página, creas otra (simple)
            if (y > page.Height - 80)
            {
                page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                gfx.Dispose();
                // Nota: para algo más pro, se refactoriza a renderer por páginas.
                // Pero para el proyecto normalmente no vas a tener 50 líneas.
                break;
            }
        }

        y += 5;
        gfx.DrawLine(XPens.Black, x, y, page.Width - 40, y);
        y += 20;

        gfx.DrawString($"TOTAL: Q {res.TotalAmount:0.00}", new XFont("Arial", 14, XFontStyle.Bold), XBrushes.Black, x, y);
        y += 26;

        gfx.DrawString($"Generado: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", fontSmall, XBrushes.Gray, x, y);

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }
}