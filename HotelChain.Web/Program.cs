using HotelChain.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HotelChain.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Registrar el handler que agregará el JWT automáticamente
builder.Services.AddScoped<AuthTokenHandler>();

// HttpClient configurado con el handler
builder.Services.AddHttpClient("Api", client =>
{
    var cfg = builder.Configuration;
    var apiBaseUrl = cfg["ApiBaseUrl"] ?? "http://localhost:5225";
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<AuthTokenHandler>();

// HttpClient principal que usará la app
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));
builder.Services.AddScoped<ReservationCartService>();
await builder.Build().RunAsync();