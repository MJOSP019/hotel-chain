using HotelChain.Api.Models.Auth;
using HotelChain.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotelChain.Api.Controllers;

/// <summary>
/// Controlador responsable del registro, autenticación y utilidades básicas
/// de usuario dentro del sistema.
/// </summary>
/// <remarks>
/// Expone endpoints para captcha, registro, login, consulta del usuario autenticado
/// y promoción de usuarios al rol de tipo WebService.
/// </remarks>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICaptchaService _captcha;
    private readonly IJwtTokenService _jwt;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de autenticación.
    /// </summary>
    /// <param name="userManager">Administrador de usuarios basado en ASP.NET Core Identity.</param>
    /// <param name="signInManager">Administrador de inicio de sesión para validación de credenciales.</param>
    /// <param name="captcha">Servicio encargado de generar y validar captchas.</param>
    /// <param name="jwt">Servicio encargado de generar tokens JWT para usuarios autenticados.</param>
    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICaptchaService captcha,
        IJwtTokenService jwt)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _captcha = captcha;
        _jwt = jwt;
    }

    /// <summary>
    /// Genera un nuevo captcha para el proceso de registro.
    /// </summary>
    /// <returns>
    /// Un objeto con el identificador del captcha y la pregunta que debe resolver el usuario.
    /// </returns>
    [HttpGet("captcha")]
    [AllowAnonymous]
    public IActionResult Captcha()
    {
        var (captchaId, question) = _captcha.Create();
        return Ok(new { captchaId, question });
    }

    /// <summary>
    /// Registra un nuevo usuario en el sistema y le asigna el rol REGISTERED.
    /// </summary>
    /// <param name="req">Datos de registro del usuario, incluyendo captcha y credenciales.</param>
    /// <returns>
    /// Un token JWT si el registro fue exitoso; de lo contrario, devuelve errores de validación.
    /// </returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (!_captcha.Validate(req.CaptchaId, req.CaptchaAnswer))
            return BadRequest(new { message = "Captcha inválido." });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.Email,
            Email = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Age = req.Age,
            Country = req.Country,
            PassportNumber = req.PassportNumber
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, Roles.REGISTERED);

        var token = await _jwt.CreateTokenAsync(user);
        return Ok(new AuthResponse(token));
    }

    /// <summary>
    /// Autentica un usuario registrado y genera un token JWT.
    /// </summary>
    /// <param name="req">Credenciales del usuario.</param>
    /// <returns>
    /// Un token JWT si las credenciales son válidas; en caso contrario, devuelve Unauthorized.
    /// </returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null) return Unauthorized(new { message = "Credenciales inválidas." });

        var ok = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!ok.Succeeded) return Unauthorized(new { message = "Credenciales inválidas." });

        var token = await _jwt.CreateTokenAsync(user);
        return Ok(new AuthResponse(token));
    }

    /// <summary>
    /// Promueve un usuario registrado al rol WEBSERVICE.
    /// </summary>
    /// <param name="userId">Identificador del usuario a promover.</param>
    /// <returns>
    /// NoContent si la operación fue exitosa; NotFound o BadRequest si no cumple las condiciones requeridas.
    /// </returns>
    [HttpPost("users/{userId:guid}/promote-webservice")]
    [Authorize(Roles = Roles.ADMIN)]
    public async Task<IActionResult> PromoteToWebService(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();

        if (!await _userManager.IsInRoleAsync(user, Roles.REGISTERED))
            return BadRequest(new { message = "Debe ser REGISTERED antes de WEBSERVICE." });

        await _userManager.AddToRoleAsync(user, Roles.WEBSERVICE);
        return NoContent();
    }

    /// <summary>
    /// Obtiene información básica del usuario autenticado actual.
    /// </summary>
    /// <returns>
    /// Identificador y correo electrónico del usuario autenticado.
    /// </returns>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

        return Ok(new
        {
            userId,
            email
        });
    }

    /// <summary>
    /// Endpoint de prueba para verificar que el controlador responde correctamente.
    /// </summary>
    /// <returns>La cadena "pong".</returns>
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok("pong");

    /// <summary>
    /// Busca un usuario por correo electrónico y devuelve su identificador.
    /// </summary>
    /// <param name="email">Correo electrónico del usuario a consultar.</param>
    /// <returns>
    /// El identificador y correo del usuario si existe; NotFound en caso contrario.
    /// </returns>
    [HttpGet("users/by-email")]
    [Authorize(Roles = Roles.ADMIN)]
    public async Task<IActionResult> GetUserIdByEmail([FromQuery] string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return NotFound(new { message = "No existe ese usuario." });

        return Ok(new { userId = user.Id, email = user.Email });
    }
}