using HotelChain.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HotelChain.Infrastructure.Seeding;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        const string email = "admin@hotelchain.com";
        const string password = "HotelChain#123";

        var admin = await userManager.FindByEmailAsync(email);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Admin",
                Age = 30,
                Country = "GT",
                PassportNumber = "ADMIN-000"
            };

            var created = await userManager.CreateAsync(admin, password);

            if (!created.Succeeded)
            {
                var errors = string.Join(" | ", created.Errors.Select(e => e.Description));
                throw new Exception($"No se pudo crear el admin: {errors}");
            }
        }
        else
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            var reset = await userManager.ResetPasswordAsync(admin, token, password);

            if (!reset.Succeeded)
            {
                var errors = string.Join(" | ", reset.Errors.Select(e => e.Description));
                throw new Exception($"No se pudo resetear la contraseña del admin: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.ADMIN))
            await userManager.AddToRoleAsync(admin, Roles.ADMIN);
    }
}