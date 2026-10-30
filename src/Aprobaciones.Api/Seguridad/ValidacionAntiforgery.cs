using Microsoft.AspNetCore.Antiforgery;

namespace Aprobaciones.Api.Seguridad;

/// <summary>
/// Con Windows Authentication el navegador envía las credenciales en cada petición, así que toda
/// escritura en /api exige el encabezado X-XSRF-TOKEN que Angular toma de la cookie XSRF-TOKEN.
/// </summary>
internal static class ValidacionAntiforgery
{
    public const string Cookie = "XSRF-TOKEN";
    public const string Encabezado = "X-XSRF-TOKEN";

    public static IApplicationBuilder UseValidacionAntiforgery(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            if (contexto.Request.Path.StartsWithSegments("/api") && !EsMetodoSeguro(contexto.Request.Method))
            {
                var antiforgery = contexto.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    await Results.Problem(
                        title: "Token antiforgery inválido",
                        detail: "Recarga la página e intenta de nuevo.",
                        statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(contexto);
                    return;
                }
            }
            await siguiente(contexto);
        });

    /// <summary>Emite el token para que Angular lo reenvíe en el encabezado.</summary>
    public static void EmitirToken(HttpContext contexto)
    {
        var antiforgery = contexto.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(contexto);
        contexto.Response.Cookies.Append(Cookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = contexto.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }

    private static bool EsMetodoSeguro(string metodo) =>
        HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo);
}
