# Pruebas unitarias/backend agregadas

Este paquete agrega pruebas con xUnit + EF Core InMemory para cubrir los flujos principales del backend del sistema Cadena de Hoteles.

## Cobertura agregada

- Administración de hoteles:
  - Validaciones de creación.
  - Código duplicado.
  - Ciudad inválida.
  - Creación correcta.
  - Consulta inexistente.
  - Activación/desactivación.
  - Validación de código duplicado en actualización.

- Administración de habitaciones:
  - Hotel inexistente.
  - Tipo de habitación inexistente.
  - Habitación duplicada en el mismo hotel.
  - Creación correcta con campos normalizados.
  - Actualización inexistente.
  - Activación/desactivación.

- Búsqueda pública:
  - Fechas inválidas.
  - Huéspedes inválidos.
  - Auditoría de búsqueda WEB.
  - Opciones disponibles con inventario comercial.
  - Mensajes de restricción por venta cerrada.
  - Filtros por precio y tipo de habitación.

- Lecturas públicas:
  - Ciudades.
  - Tipos de habitación.
  - Detalle público de hotel.
  - Hotel inactivo no visible.
  - Opciones de habitación con y sin contexto de estadía.

- Reseñas:
  - Hotel inexistente.
  - Rating fuera de rango.
  - Usuario no autenticado.
  - Creación de reseña principal.
  - Creación de respuesta multinivel.
  - Árbol de reseñas con respuestas.

- Reservaciones públicas:
  - Usuario no autenticado.
  - Fechas inválidas.
  - Sin opción comercial disponible.
  - Creación de reserva PENDING.
  - Reserva de inventario comercial.
  - Consulta inexistente.
  - Checkout con tarjeta inválida por Luhn.
  - Checkout correcto, cambio a CONFIRMED y creación de pago.

- Operación hotelera/admin de reservas:
  - Cargos con descripción inválida.
  - Cargos en reserva que no está CHECKED_IN.
  - Creación de cargos.
  - Liquidación sin cargos pendientes.
  - Liquidación con monto incorrecto.
  - Liquidación correcta de cargos.
  - Bloqueo de check-out con cargos pendientes.
  - Check-out correcto con cuenta liquidada.

## Cómo correr

Desde la raíz del proyecto:

```powershell
dotnet test
```

O específicamente el proyecto de pruebas:

```powershell
dotnet test HotelChain.Tests/HotelChain.Tests.csproj
```

## Nota importante

No pude ejecutar `dotnet test` dentro de este entorno porque aquí no está instalado el SDK de .NET. Los archivos se prepararon directamente contra la estructura real del proyecto enviado. Si aparece algún error de compilación local, normalmente será por una diferencia mínima de versión/namespace y se corrige rápido con el mensaje exacto de la consola.

## Ampliación v3

Esta versión agrega cobertura adicional para:

- `AdminAnalyticsController`: listado filtrado/paginado, dashboard agregado y exportación CSV.
- `IntegrationController`: hoteles activos, búsqueda B2B, auditoría de búsqueda, creación de reserva por integración y cancelación con liberación de inventario.
- `WsSearchController` y `WsReservationsController`: búsqueda webservice y lectura de identidad de agencia desde JWT de prueba.
- `UserReservationsController`: validación de acceso sin usuario autenticado.
- `PublicController`: ping público.

La suite ahora cubre más endpoints secundarios y flujos B2B, manteniendo pruebas con EF Core InMemory para no depender de SQL Server real.
