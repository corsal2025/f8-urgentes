namespace F8Urgentes.Domain;

public enum FolderSector { Archivo, Oficina43 }

/// <summary>
/// Sector is derived from FechaUltimaCarpeta, not stored: cases whose last-folder date falls
/// before the archive cutover go to the old Archivo, everything from that date on is Oficina43.
/// </summary>
public static class FolderSectorCalculator
{
    private static readonly DateOnly Cutoff = new(2023, 7, 1);

    public static FolderSector? For(DateOnly? fechaUltimaCarpeta) =>
        fechaUltimaCarpeta is { } fecha
            ? fecha < Cutoff ? FolderSector.Archivo : FolderSector.Oficina43
            : null;
}
