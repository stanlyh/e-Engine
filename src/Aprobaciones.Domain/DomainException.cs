namespace Aprobaciones.Domain;

/// <summary>Violación de una regla de negocio. La API la traduce a 409/400.</summary>
public sealed class DomainException(string message) : Exception(message);
