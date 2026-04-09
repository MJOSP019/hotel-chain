# Backend

## Descripción
El backend fue desarrollado con ASP.NET Core Web API y es responsable de exponer la lógica funcional del sistema.

## Responsabilidades principales
- Autenticación y autorización
- Búsqueda pública de hoteles y habitaciones
- Reservaciones
- Checkout
- Generación de PDF
- Cancelación y cambio de fechas
- Administración de hoteles, habitaciones e inventario
- Administración de usuarios y roles
- Analíticas de búsquedas
- Integración REST para clientes empresariales

## Documentación técnica generada del código
La documentación del backend se apoya en dos mecanismos:

- **Swagger/OpenAPI**, que documenta automáticamente los endpoints REST del sistema.
- **Comentarios XML en C#**, habilitados en el proyecto para generar archivos de documentación `.xml` durante la compilación.

Se documentaron controladores, DTOs y entidades principales, permitiendo complementar la documentación funcional con documentación generada directamente desde el código fuente.

## Ejemplos documentados
- AuthController
- ReservationsController
- AdminHotelsController
- HotelDetailDto
- Hotel
- Reservation