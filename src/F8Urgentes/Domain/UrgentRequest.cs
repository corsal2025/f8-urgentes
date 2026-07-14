namespace F8Urgentes.Domain;

/// <summary>
/// Plain data holder mirroring the UrgentRequest table (design D2): every
/// Excel column typed, plus import provenance and the NeedsReview rollup.
/// No behavior here — validation/normalization lives in Rut/FolderDate/EstadoCatalog.
/// </summary>
public sealed class UrgentRequest
{
    public long Id { get; set; }

    public DateOnly? FechaPeticion { get; set; }
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public string? NombreCompleto { get; set; }
    public string? Rut { get; set; }
    public string? RutRaw { get; set; }
    public DateOnly? FechaUltimaCarpeta { get; set; }
    public string? CodigoF8 { get; set; }
    public DateOnly? FechaPenultimaCarpeta { get; set; }
    public string? Estado { get; set; }
    public string? EstadoActual { get; set; }
    public DateOnly? FechaDeSubida { get; set; }

    public string? SourceSheet { get; set; }
    public int? SourceRowNumber { get; set; }
    public string Origin { get; set; } = "Web";

    public bool NeedsReview { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
