using Aprobaciones.Api.Endpoints;
using Aprobaciones.Api.Errores;
using Aprobaciones.Api.Seguridad;
using Aprobaciones.Application;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// Intranet: Windows Authentication (Kerberos/NTLM). En IIS la negociación la hace IIS y este handler la reutiliza.
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.Configure<OpcionesSeguridad>(builder.Configuration.GetSection(OpcionesSeguridad.Seccion));
builder.Services.AddScoped<IClaimsTransformation, TransformacionClaims>();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Roles.Usuario, p => p.RequireRole(Roles.Usuario))
    .AddPolicy(Roles.Aprobador, p => p.RequireRole(Roles.Aprobador))
    .AddPolicy(Roles.Tesoreria, p => p.RequireRole(Roles.Tesoreria))
    .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();
builder.Services.AddAntiforgery(o => o.HeaderName = ValidacionAntiforgery.Encabezado);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddOpenApi();

// Solo desarrollo: Angular corre en otro puerto (ng serve). En producción se sirve desde este mismo sitio.
const string CorsDesarrollo = "angular-dev";
builder.Services.AddCors(o => o.AddPolicy(CorsDesarrollo, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseCors(CorsDesarrollo);
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseValidacionAntiforgery();

app.MapCuentaEndpoints();
app.MapReglasEndpoints();

// Rutas de API inexistentes devuelven 404; el resto lo resuelve el router de Angular.
app.MapFallback("/api/{**ruta}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
