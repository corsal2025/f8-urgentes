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

    public bool Marked { get; set; }
    public DateTimeOffset? MarkedAt { get; set; }
    public DateTimeOffset? SectorPdfGeneratedAt { get; set; }
    public bool PendienteCarpeta { get; set; }

    // Matriz sync (design: Excel matriz de distribucion de carpetas). MatrizSector is the
    // physical office sector (AV. ARGENTINA / PLACILLA / MERC. PUERTO) read from the sheet
    // name the row came from — distinct from the computed FolderSector (Archivo/Oficina43)
    // above, which is about the old-vs-new filing room, not the department office.
    // PendienteEscrituraExcel is set when the operator marks a case as uploaded and the
    // matching row in the matriz still needs "SUBIDA CON F8" + today's date written back;
    // it's cleared only after MatrizSyncService confirms the write succeeded.
    public string? MatrizSector { get; set; }
    public bool PendienteEscrituraExcel { get; set; }

    // Set when a completed case (SUBIDA A CONASET) has been included in a monthly print
    // batch (ImpresionMensual page) — lets the end-of-month reminder know which cases from
    // the previous month still need printing.
    public DateTimeOffset? ImpresoMensualAt { get; set; }

    // "Sin carpeta" (point 2 of the Caja module): operator-ticked flag meaning the case is closed
    // but stays visible in Casos (Index), rendered gray like a confirmed row. Independent of
    // EstadoActual — does not change routing/confirmation flow. Reversible via "Revertir".
    public bool SinCarpeta { get; set; }

    // Caja module (points 3-6): once TransferredAt is set the case leaves Casos (Index) and lives
    // on /Caja — either in the open queue (CajaBoxId is null) or packed into a closed Box
    // (CajaBoxId set). Independent of EstadoActual, mirroring SinCarpeta above.
    public DateTimeOffset? CajaTransferredAt { get; set; }
    public long? CajaBoxId { get; set; }

    public FolderSector? Sector => FolderSectorCalculator.For(FechaPenultimaCarpeta);
}

/// <summary>A closed batch of Caja cases, in the physical order they were packed — see
/// IUrgentRequestRepository.CloseBox. Numbered sequentially; once closed a box's membership
/// never changes except via ReopenBox/RemoveCaseFromClosedBox.</summary>
public sealed class Box
{
    public long Id { get; set; }
    public int Number { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ClosedAt { get; set; }
}
