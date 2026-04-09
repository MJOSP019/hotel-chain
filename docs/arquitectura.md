# Arquitectura del sistema

## Estructura general
El sistema está dividido en cuatro proyectos principales:

- **HotelChain.Domain**: contiene las entidades del dominio.
- **HotelChain.Infrastructure**: contiene acceso a datos, Entity Framework Core, Identity, JWT y persistencia.
- **HotelChain.Api**: expone la API REST del sistema.
- **HotelChain.Web**: contiene el portal web desarrollado en Blazor WebAssembly.

## Base de datos
- Motor: SQL Server
- Base de datos: HotelChainDb

## Comunicación
El frontend consume la API REST propia del sistema mediante HTTP.  
La comunicación entre componentes sigue una separación por capas para facilitar mantenimiento y escalabilidad.