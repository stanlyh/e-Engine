namespace Aprobaciones.Application;

/// <summary>El recurso no existe o el usuario no tiene permiso de verlo (se responde igual para no revelar su existencia).</summary>
public sealed class NoEncontradoException(string message) : Exception(message);

/// <summary>El usuario puede ver el recurso pero no tiene el rol para la acción.</summary>
public sealed class AccesoDenegadoException(string message) : Exception(message);
