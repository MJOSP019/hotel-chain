using HotelChain.Api.Contracts;
using HotelChain.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador administrativo para consulta de usuarios y actualización de roles.
/// </summary>
/// <remarks>
/// Permite listar usuarios registrados y asignarles uno de los roles válidos del sistema:
/// ADMIN, REGISTERED o WEBSERVICE.
/// </remarks>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.ADMIN)]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Inicializa una nueva instancia del controlador administrativo de usuarios.
    /// </summary>
    /// <param name="userManager">Administrador de usuarios basado en Identity.</param>
    public AdminUsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Obtiene el listado de usuarios con su rol actual.
    /// </summary>
    /// <returns>Listado administrativo de usuarios.</returns>
    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> GetAll()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        var result = new List<AdminUserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var currentRole = roles.FirstOrDefault() ?? "SIN_ROL";

            result.Add(new AdminUserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                Age = user.Age,
                Country = user.Country,
                PassportNumber = user.PassportNumber,
                Role = currentRole
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Actualiza el rol principal de un usuario existente.
    /// </summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="request">Nuevo rol a asignar.</param>
    /// <returns>Resultado de la actualización del rol.</returns>
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Role))
            return BadRequest(new { message = "El rol es requerido." });

        var allowedRoles = new[] { Roles.ADMIN, Roles.REGISTERED, Roles.WEBSERVICE };

        if (!allowedRoles.Contains(request.Role))
            return BadRequest(new { message = "Rol no válido." });

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound(new { message = "Usuario no encontrado." });

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "No se pudieron remover los roles actuales.",
                    errors = removeResult.Errors.Select(e => e.Description)
                });
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "No se pudo asignar el nuevo rol.",
                errors = addResult.Errors.Select(e => e.Description)
            });
        }

        return Ok(new
        {
            message = "Rol actualizado correctamente.",
            userId = user.Id,
            role = request.Role
        });
    }
}