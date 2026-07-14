using ClosedXML.Excel;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Import;

/// <summary>
/// One-time historical importer (design D6): iterates MAYO/JUNIO/JULIO sheets,
/// resolves columns by header name (never by position), normalizes each field,
/// and flags-not-drops dubious cells. AGOSTO-DICIEMBRE empty template sheets are
/// skipped entirely.
/// </summary>
public static class ExcelUrgentImporter
{
    private static readonly string[] ImportableSheets = ["MAYO", "JUNIO", "JULIO"];

    public static ImportResult Import(string workbookPath, IUrgentRequestRepository repository, bool force = false)
    {
        var sourceFile = Path.GetFileName(workbookPath);

        if (!force && repository.HasCompletedImport(sourceFile))
        {
            return new ImportResult { NoOp = true };
        }

        if (force && repository.HasCompletedImport(sourceFile))
        {
            repository.DeleteImportedRows();
        }

        using var workbook = new XLWorkbook(workbookPath);
        var result = new ImportResult();

        foreach (var sheetName in ImportableSheets)
        {
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
            {
                continue;
            }

            result.Sheets.Add(ImportSheet(worksheet, sheetName, repository));
        }

        repository.RecordImportRun(sourceFile, result.TotalRowsImported, result.TotalRowsFlagged);
        return result;
    }

    private static SheetImportSummary ImportSheet(IXLWorksheet worksheet, string sheetName, IUrgentRequestRepository repository)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return new SheetImportSummary(sheetName, 0, 0, 0, 0);
        }

        var headerRow = usedRange.FirstRow();
        var headerCells = headerRow.Cells(headerRow.FirstCellUsed().Address.ColumnNumber, headerRow.LastCellUsed().Address.ColumnNumber)
            .Select(c => c.GetString())
            .ToList();
        var columnMap = HeaderCanonicalizer.Canonicalize(headerCells);

        if (columnMap.Count == 0)
        {
            return new SheetImportSummary(sheetName, 0, 0, 0, 0);
        }

        var firstColumn = usedRange.FirstColumn().ColumnNumber();
        int rowsRead = 0, rowsImported = 0, rowsFlagged = 0, rowsSkipped = 0;

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            var rowNumber = row.RowNumber();
            string? Cell(string canonicalName)
            {
                if (!columnMap.TryGetValue(canonicalName, out var offset))
                {
                    return null;
                }
                var value = worksheet.Cell(rowNumber, firstColumn + offset).GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            var nombres = Cell(HeaderCanonicalizer.Nombres);
            var apellidos = Cell(HeaderCanonicalizer.Apellidos);
            var nombreCompleto = Cell(HeaderCanonicalizer.NombreCompleto);
            var rutRaw = Cell(HeaderCanonicalizer.Rut);
            var fechaPeticionRaw = Cell(HeaderCanonicalizer.FechaPeticion);
            var fechaUltimaCarpetaRaw = Cell(HeaderCanonicalizer.FechaUltimaCarpeta);
            var codigoF8 = Cell(HeaderCanonicalizer.CodigoF8);
            var fechaPenultimaCarpetaRaw = Cell(HeaderCanonicalizer.FechaPenultimaCarpeta);
            var estadoRaw = Cell(HeaderCanonicalizer.Estado);
            var estadoActualRaw = Cell(HeaderCanonicalizer.EstadoActual);
            var fechaDeSubidaRaw = Cell(HeaderCanonicalizer.FechaDeSubida);

            rowsRead++;

            var isEmpty = nombres is null && apellidos is null && nombreCompleto is null && rutRaw is null &&
                          fechaPeticionRaw is null && fechaUltimaCarpetaRaw is null && codigoF8 is null &&
                          fechaPenultimaCarpetaRaw is null && estadoRaw is null && estadoActualRaw is null &&
                          fechaDeSubidaRaw is null;
            if (isEmpty)
            {
                rowsSkipped++;
                continue;
            }

            var flags = new List<(string Column, string Reason, string? RawValue)>();

            string? normalizedRut = null;
            if (rutRaw is not null)
            {
                if (Rut.TryParse(rutRaw, out var rut))
                {
                    normalizedRut = rut.ToString();
                    if (!rut.IsValid)
                    {
                        flags.Add(("Rut", ImportFlag.ReasonCodes.RutCheckDigit, rutRaw));
                    }
                }
                else
                {
                    flags.Add(("Rut", ImportFlag.ReasonCodes.RutMissing, rutRaw));
                }
            }

            var fechaPeticion = ParseDate(fechaPeticionRaw, "FechaPeticion", flags);
            var fechaUltimaCarpeta = ParseDate(fechaUltimaCarpetaRaw, "FechaUltimaCarpeta", flags);
            var fechaPenultimaCarpeta = ParseDate(fechaPenultimaCarpetaRaw, "FechaPenultimaCarpeta", flags);
            var fechaDeSubida = ParseDate(fechaDeSubidaRaw, "FechaDeSubida", flags);

            var estado = estadoRaw is null ? null : EstadoCatalog.Canonicalize(estadoRaw);
            if (estado is not null && !EstadoCatalog.IsKnownEstado(estado))
            {
                flags.Add(("Estado", ImportFlag.ReasonCodes.UnknownEstado, estadoRaw));
            }

            var estadoActual = estadoActualRaw is null ? null : EstadoCatalog.Canonicalize(estadoActualRaw);
            if (estadoActual is not null && !EstadoCatalog.IsKnownEstadoActual(estadoActual))
            {
                flags.Add(("EstadoActual", ImportFlag.ReasonCodes.UnknownEstadoActual, estadoActualRaw));
            }

            var request = new UrgentRequest
            {
                FechaPeticion = fechaPeticion,
                Nombres = nombres,
                Apellidos = apellidos,
                NombreCompleto = nombreCompleto,
                Rut = normalizedRut,
                RutRaw = rutRaw,
                FechaUltimaCarpeta = fechaUltimaCarpeta,
                CodigoF8 = codigoF8,
                FechaPenultimaCarpeta = fechaPenultimaCarpeta,
                Estado = estado,
                EstadoActual = estadoActual,
                FechaDeSubida = fechaDeSubida,
                SourceSheet = sheetName,
                SourceRowNumber = rowNumber,
                Origin = "Import",
                NeedsReview = flags.Count > 0,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var id = repository.Insert(request);
            foreach (var flag in flags)
            {
                repository.AddFlag(id, flag.Column, flag.Reason, flag.RawValue);
            }

            rowsImported++;
            if (flags.Count > 0)
            {
                rowsFlagged++;
            }
        }

        return new SheetImportSummary(sheetName, rowsRead, rowsImported, rowsFlagged, rowsSkipped);
    }

    private static DateOnly? ParseDate(string? raw, string columnName, List<(string, string, string?)> flags)
    {
        var result = FolderDate.Parse(raw);
        switch (result.Outcome)
        {
            case FolderDateOutcome.OutOfRange:
                flags.Add((columnName, ImportFlag.ReasonCodes.DateOutOfRange, raw));
                break;
            case FolderDateOutcome.Unparseable:
                flags.Add((columnName, ImportFlag.ReasonCodes.Unparseable, raw));
                break;
        }
        return result.Value;
    }
}
