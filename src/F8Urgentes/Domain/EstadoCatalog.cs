namespace F8Urgentes.Domain;

/// <summary>
/// Known ESTADO / ESTADO ACTUAL values observed in the historical workbook.
/// Both fields are stored as canonicalized open strings (not enums) because
/// operators keep introducing new values; this catalog only powers dropdown
/// options and "is this a plausible known value" flagging, never rejection.
/// </summary>
public static class EstadoCatalog
{
    public static readonly IReadOnlyCollection<string> KnownEstados = new[]
    {
        "SUBIR CON F8",
        "PRIMERA LICENCIA",
        "CAMBIO DE DOMICILIO",
        "CREAR CERTIFICADO",
        "CARPETA SUBIDA",
    };

    public static readonly IReadOnlyCollection<string> KnownEstadosActuales = new[]
    {
        "SUBIDA A CONASET",
        "PENDIENTE",
        "CREAR CERTIFICADO",
        "",
    };

    public static string Canonicalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static bool IsKnownEstado(string? value) => KnownEstados.Contains(Canonicalize(value));

    public static bool IsKnownEstadoActual(string? value) => KnownEstadosActuales.Contains(Canonicalize(value));
}
