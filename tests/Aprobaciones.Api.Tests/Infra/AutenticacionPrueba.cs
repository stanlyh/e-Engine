using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Api.Tests.Infra;

/// <summary>
/// Sustituye a Windows Authentication: la cuenta y sus grupos de AD llegan en encabezados.
/// Los grupos van como roles, igual que los grupos de Windows, así que la asignación de roles de la app
/// (TransformacionClaims) se ejecuta sin cambios.
/// </summary>
public sealed class AutenticacionPrueba(IOptionsMonitor<AuthenticationSchemeOptions> opciones, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    public const string Esquema = "Prueba";
    public const string EncabezadoCuenta = "X-Prueba-Cuenta";
    public const string EncabezadoGrupos = "X-Prueba-Grupos";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cuenta = Request.Headers[EncabezadoCuenta].ToString();
        if (string.IsNullOrWhiteSpace(cuenta)) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.Name, cuenta) };
        foreach (var grupo in Request.Headers[EncabezadoGrupos].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, grupo));

        var identidad = new ClaimsIdentity(claims, Esquema, ClaimTypes.Name, ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema)));
    }
}

/// <summary>
/// El manejador de Negotiate atiende todas las peticiones aunque no sea el esquema por defecto, y fuera de Kestrel
/// (el servidor en memoria de las pruebas) lanza NotSupportedException. Aquí se excluye de esa atención.
/// </summary>
public sealed class ProveedorEsquemasPrueba(IOptions<AuthenticationOptions> opciones) : AuthenticationSchemeProvider(opciones)
{
    public override async Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() =>
        (await base.GetRequestHandlerSchemesAsync()).Where(s => s.Name != "Negotiate");
}
