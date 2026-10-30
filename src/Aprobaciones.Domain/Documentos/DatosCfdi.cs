namespace Aprobaciones.Domain.Documentos;

/// <summary>Datos extraídos del XML CFDI 4.0.</summary>
public sealed record DatosCfdi(
    Guid Uuid,
    string RfcEmisor,
    string NombreEmisor,
    string RfcReceptor,
    string? Serie,
    string? Folio,
    DateTime Fecha,
    decimal SubTotal,
    decimal Total,
    string Moneda,
    decimal? TipoCambio,
    string TipoComprobante)
{
    /// <summary>Monto en pesos usado para elegir la cadena de aprobación.</summary>
    public decimal MontoMxn => Moneda is "MXN" or "XXX"
        ? Total
        : Math.Round(Total * (TipoCambio ?? throw new DomainException($"El CFDI en {Moneda} no trae TipoCambio.")), 2);
}
