using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Matriz;

/// <summary>
/// Reads the "matriz de distribucion de carpetas" workbook (a separate spreadsheet from
/// URGENTES DIARIOS.xlsx, maintained by the office to track physical folders) to auto-create
/// cases flagged "NO HAY CARPETA" (entrada), and writes back "SUBIDA CON F8" + today's date
/// once the operator marks a case uploaded in the dashboard (salida).
///
/// Uses the raw DocumentFormat.OpenXml SDK instead of ClosedXML for this specific file: the
/// real matriz workbook has data-validation dropdown lists (ESTADO/DECISION columns) whose
/// formula text exceeds ClosedXML's hardcoded 255-char limit, which throws
/// ArgumentOutOfRangeException the instant ClosedXML tries to *load* the file — before any
/// cell is even read. The OpenXml SDK never parses data validations unless explicitly asked
/// to, so it opens/edits this file without touching (or needing to understand) them at all.
/// Writes only ever touch the two target cells' own XML nodes — nothing else in the workbook
/// (styles, formulas, validations, other sheets) is rewritten — and a timestamped backup copy
/// is taken before every save.
/// </summary>
public static class MatrizSyncService
{
    private static readonly string[] KnownSectors = ["AV. ARGENTINA", "PLACILLA", "MERC. PUERTO"];
    // The real workbook uses "NO EXISTE CARPETA" — "NO HAY CARPETA" is kept as a second accepted
    // spelling since operators across sheets/months aren't perfectly consistent in wording.
    private static readonly string[] EstadosSinCarpeta = ["NO EXISTE CARPETA", "NO HAY CARPETA"];
    private const int MaxRetries = 4;
    private const int MaxConsecutiveEmptyRows = 50;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static MatrizSyncResult Sync(string? workbookPath, IUrgentRequestRepository repository)
    {
        var result = new MatrizSyncResult();
        if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
        {
            result.NoOp = true;
            return result;
        }

        RunEntrada(workbookPath, repository, result);
        return result;
    }

    // SpreadsheetDocument.Open(path, false) requests FileShare.Read, which Windows refuses while
    // Excel (or the OneDrive/Drive client) holds a write handle on the workbook — the exact
    // case where the operator has the matriz open. Reading through a stream that allows
    // concurrent writers, into memory, avoids that conflict and releases the file immediately.
    private static SpreadsheetDocument OpenReadOnlySnapshot(string workbookPath)
    {
        var buffer = new MemoryStream();
        using (var source = new FileStream(
                   workbookPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            source.CopyTo(buffer);
        }

        buffer.Position = 0;
        return SpreadsheetDocument.Open(buffer, isEditable: false);
    }

    // Entrada: opened read-only (isEditable: false) — the OpenXml SDK cannot alter the file
    // under a read-only open, so this pass is inherently non-destructive.
    //
    // Retries mirror RunSalida: the office keeps this file syncing via OneDrive/Drive, and a
    // read that lands mid-sync sees a half-written zip package. The OpenXml SDK surfaces that
    // as FileFormatException ("File contains corrupted data.") rather than IOException, so both
    // are treated as transient here — the file is not actually corrupt, just momentarily
    // inconsistent on disk.
    private static void RunEntrada(string workbookPath, IUrgentRequestRepository repository, MatrizSyncResult result)
    {
        SpreadsheetDocument? document = null;
        for (var attempt = 1; attempt <= MaxRetries && document is null; attempt++)
        {
            try
            {
                document = OpenReadOnlySnapshot(workbookPath);
            }
            catch (IOException) when (attempt < MaxRetries)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (FileFormatException) when (attempt < MaxRetries)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (Exception ex) when (ex is IOException or FileFormatException or UnauthorizedAccessException)
            {
                result.ExcelLocked = true;
                result.Alerts.Add($"El Excel matriz no se pudo leer ({ex.GetType().Name}: {ex.Message}) — se reintentara en la proxima sincronizacion.");
                return;
            }
        }

        if (document is null)
        {
            result.ExcelLocked = true;
            return;
        }

        using var _ = document;
        var workbookPart = document.WorkbookPart!;
        var sharedStrings = ReadSharedStrings(workbookPart);

        foreach (var sheet in workbookPart.Workbook.Sheets!.Elements<Sheet>())
        {
            var sheetName = sheet.Name!.Value!;
            var sector = SectorFor(sheetName);
            if (sector is null)
            {
                continue;
            }

            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            if (sheetData is null)
            {
                continue;
            }

            var columns = MapColumns(sheetData, sharedStrings);
            if (columns is null)
            {
                continue;
            }

            var consecutiveEmpty = 0;
            foreach (var row in sheetData.Elements<Row>())
            {
                var rowNumber = (int)row.RowIndex!.Value;
                if (rowNumber < 3)
                {
                    continue;
                }

                var rut = GetCellText(row, columns.Rut, sharedStrings)?.Trim() ?? "";
                var nombre = GetCellText(row, columns.NombreCompleto, sharedStrings)?.Trim() ?? "";
                var estado = GetCellText(row, columns.EstadoCarpeta, sharedStrings)?.Trim() ?? "";

                if (rut.Length == 0 && nombre.Length == 0 && estado.Length == 0)
                {
                    consecutiveEmpty++;
                    if (consecutiveEmpty >= MaxConsecutiveEmptyRows)
                    {
                        break;
                    }
                    continue;
                }

                consecutiveEmpty = 0;

                if (!EstadosSinCarpeta.Contains(NormalizeHeader(estado), StringComparer.Ordinal))
                {
                    continue;
                }

                if (!Rut.TryParse(rut, out var parsedRut))
                {
                    result.Alerts.Add($"RUT invalido en hoja '{sheetName}' fila {rowNumber}: '{rut}' — revisar manualmente.");
                    continue;
                }

                var normalizedRut = parsedRut.ToString();
                if (repository.FindByRut(normalizedRut) is not null)
                {
                    continue;
                }

                var fechaCitacionRaw = columns.FechaCitacion is { } fechaCol ? GetCellText(row, fechaCol, sharedStrings) : null;
                var fechaCitacion = FolderDate.Parse(fechaCitacionRaw).Value;

                repository.Insert(new UrgentRequest
                {
                    NombreCompleto = nombre.Length > 0 ? nombre : null,
                    Rut = normalizedRut,
                    RutRaw = rut,
                    FechaPeticion = fechaCitacion,
                    MatrizSector = sector,
                    SourceSheet = sheetName,
                    SourceRowNumber = rowNumber,
                    Origin = "Matriz",
                    NeedsReview = !parsedRut.IsValid,
                    CreatedAt = DateTimeOffset.UtcNow,
                });

                result.CasesCreated++;
            }
        }
    }

    private static string? SectorFor(string sheetName)
    {
        var normalized = NormalizeHeader(sheetName);
        if (normalized.Contains("PLANTILLA") || normalized.Contains("ESCANEADAS") || normalized.Contains("CORREOS"))
        {
            return null;
        }
        return KnownSectors.FirstOrDefault(sector => normalized.Contains(NormalizeHeader(sector)));
    }

    private sealed record MatrizColumns(int Rut, int NombreCompleto, int EstadoCarpeta, int? FechaCitacion, int FechaSubioCarpeta);

    private static MatrizColumns? MapColumns(SheetData sheetData, IReadOnlyList<string> sharedStrings)
    {
        var headerRow = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex!.Value == 2);
        if (headerRow is null)
        {
            return null;
        }

        int rutCol = 0, nombreCol = 0, estadoCol = 0, fechaSubioCol = 0, fechaCitacionCol = 0;

        foreach (var cell in headerRow.Elements<Cell>())
        {
            var colIndex = ColumnIndexFromReference(cell.CellReference!.Value!);
            var header = NormalizeHeader(GetCellText(cell, sharedStrings));

            if (header == "RUT")
            {
                rutCol = colIndex;
            }
            else if (header == "NOMBRE COMPLETO")
            {
                nombreCol = colIndex;
            }
            else if ((header.Contains("ESTADO") && header.Contains("CARPETA")) || header.Contains("ESTADO") || header.StartsWith("COLUMNA 10") || header == "COLUMNA10")
            {
                estadoCol = colIndex;
            }
            else if (header.Contains("FECHA") && header.Contains("SUBIO"))
            {
                fechaSubioCol = colIndex;
            }
            else if (header.Contains("FECHA") && header.Contains("CITACION"))
            {
                fechaCitacionCol = colIndex;
            }
        }

        // Resilient fallback: in standard 11-column template, Col 10 (J) is Estado, Col 1 is FechaCitacion, Col 2 is FechaSubio
        if (estadoCol == 0 && rutCol == 7 && nombreCol == 6)
        {
            estadoCol = 10;
        }

        if (fechaCitacionCol == 0 && rutCol == 7)
        {
            fechaCitacionCol = 1;
        }

        if (fechaSubioCol == 0 && rutCol == 7)
        {
            fechaSubioCol = 2;
        }

        if (rutCol == 0 || nombreCol == 0 || estadoCol == 0)
        {
            return null;
        }

        return new MatrizColumns(rutCol, nombreCol, estadoCol, fechaCitacionCol == 0 ? null : fechaCitacionCol, fechaSubioCol);
    }

    private static IReadOnlyList<string> ReadSharedStrings(WorkbookPart workbookPart)
    {
        var sharedStringsPart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
        return sharedStringsPart?.SharedStringTable?.Elements<SharedStringItem>().Select(x => x.InnerText).ToArray()
               ?? [];
    }

    private static string? GetCellText(Row row, int columnIndex, IReadOnlyList<string> sharedStrings)
    {
        var columnLetter = ColumnLetterFromIndex(columnIndex);
        var cell = row.Elements<Cell>().FirstOrDefault(c => ColumnPartOf(c.CellReference!.Value!) == columnLetter);
        return GetCellText(cell, sharedStrings);
    }

    private static string? GetCellText(Cell? cell, IReadOnlyList<string> sharedStrings)
    {
        if (cell is null)
        {
            return null;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.Text?.InnerText;
        }

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (cell.CellValue is null || !int.TryParse(cell.CellValue.InnerText, out var index))
            {
                return null;
            }
            return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : null;
        }

        return cell.CellValue?.InnerText;
    }

    private static string ColumnPartOf(string cellReference) => new(cellReference.TakeWhile(char.IsLetter).ToArray());

    private static int ColumnIndexFromReference(string cellReference)
    {
        var index = 0;
        foreach (var ch in ColumnPartOf(cellReference))
        {
            index = index * 26 + (ch - 'A' + 1);
        }
        return index;
    }

    private static string ColumnLetterFromIndex(int index)
    {
        var builder = new StringBuilder();
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            builder.Insert(0, (char)('A' + remainder));
            index = (index - 1) / 26;
        }
        return builder.ToString();
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }
}
