using System.Text; // 🔴 NUEVO (para JWT)
using HotelChain.Api.Services;
using HotelChain.Infrastructure.Auth; // 🔴 NUEVO
using HotelChain.Infrastructure.Data;
using HotelChain.Infrastructure.Seeding; // 🔴 NUEVO
using Microsoft.AspNetCore.Authentication.JwtBearer; // 🔴 NUEVO
using Microsoft.AspNetCore.Identity; // 🔴 NUEVO
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens; // 🔴 NUEVO
using Microsoft.OpenApi.Models; // 🔴 NUEVO
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

// ===============================================
// SERVICIOS
// ===============================================

builder.Services.AddEndpointsApiExplorer();

// 🔴 MODIFICADO: Swagger con soporte JWT
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(t => t.FullName);
    c.SwaggerDoc("v1", new() { Title = "HotelChain.Api", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega SOLO el JWT. Swagger agrega 'Bearer ' automáticamente."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorWasm",
        policy => policy
            .WithOrigins("http://localhost:5127")
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

// DB
builder.Services.AddDbContext<HotelChainDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(cs);
});

// ===============================================
// 🔴 NUEVO: IDENTITY
// ===============================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<HotelChainDbContext>()
.AddDefaultTokenProviders();

// ✅ IMPORTANTE para APIs: no redirigir a /Account/Login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// ===============================================
// 🔴 NUEVO: JWT CONFIG
// ===============================================

IdentityModelEventSource.ShowPII = true;

var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.IncludeErrorDetails = true;

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine("JWT AUTH FAILED: " + ctx.Exception);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            Console.WriteLine($"JWT CHALLENGE: error={ctx.Error} desc={ctx.ErrorDescription}");
            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// ===============================================
// 🔴 NUEVO: Servicios personalizados
// ===============================================
builder.Services.AddMemoryCache();
builder.Services.AddScoped<HotelChain.Infrastructure.Auth.ICaptchaService, HotelChain.Infrastructure.Auth.SimpleCaptchaService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// ===============================================
// 🔴 NUEVO: Email
// ===============================================
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var app = builder.Build();

// ===============================================
// SWAGGER
// ===============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===============================================
// MIDDLEWARE
// ===============================================
if (!app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("BlazorWasm");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===============================================
// 🔴 NUEVO: Seed de Roles al iniciar la app
// ===============================================
using (var scope = app.Services.CreateScope())
{
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
    await AdminSeeder.SeedAsync(scope.ServiceProvider); // ✅ NUEVO
}

// ===============================================
// Weather endpoint (lo dejo intacto)
// ===============================================

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}