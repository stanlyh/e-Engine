namespace Aprobaciones.Application;

/// <summary>El recurso no existe o el usuario no tiene permiso de verlo (se responde igual para no revelar su existencia).</summary>
public sealed class NoEncontradoException(string message) : Exception(message);

/// <summary>El usuario puede ver el recurso pero no tiene el rol para la acción.</summary>
public sealed class AccesoDenegadoException(string message) : Exception(message);

/// <summary>Otra persona cambió el recurso desde que se leyó; hay que recargar y volver a intentar.</summary>
public sealed class ConflictoException(string message) : Exception(message);
