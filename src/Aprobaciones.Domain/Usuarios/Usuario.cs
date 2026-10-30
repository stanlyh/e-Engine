namespace Aprobaciones.Domain.Usuarios;

/// <summary>Copia local de un usuario de Active Directory; se crea en su primer acceso.</summary>
public sealed class Usuario
{
    private Usuario() { }

    public Usuario(string cuenta, string nombre, string? email, DateTime ahora)
    {
        Cuenta = cuenta;
        Nombre = nombre;
        Email = email;
        FechaAlta = ahora;
        UltimoAcceso = ahora;
        Activo = true;
    }

    public int Id { get; private set; }
    /// <summary>DOMINIO\usuario</summary>
    public string Cuenta { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public string? Email { get; private set; }
    public bool Activo { get; private set; }
    public DateTime FechaAlta { get; private set; }
    public DateTime UltimoAcceso { get; private set; }

    public void RegistrarAcceso(string nombre, string? email, DateTime ahora)
    {
        Nombre = nombre;
        Email = email;
        UltimoAcceso = ahora;
    }
}
