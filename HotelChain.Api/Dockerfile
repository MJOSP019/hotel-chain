FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HotelChain.Domain/HotelChain.Domain.csproj HotelChain.Domain/
COPY HotelChain.Infrastructure/HotelChain.Infrastructure.csproj HotelChain.Infrastructure/
COPY HotelChain.Api/HotelChain.Api.csproj HotelChain.Api/

RUN dotnet restore HotelChain.Api/HotelChain.Api.csproj

COPY . .

WORKDIR /src/HotelChain.Api
RUN dotnet publish HotelChain.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HotelChain.Api.dll"]