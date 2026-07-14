using ClosedXML.Excel;

namespace F8Urgentes.Tests.Fixtures;

/// <summary>
/// Builds small pinned-layout .xlsx fixtures in a temp file per test run
/// (rather than committing binary .xlsx files), covering the real MAYO
/// 10-column reordered layout (no FECHA DE SUBIDA, including the real
/// out-of-range serial 29262516 in H54) and the standard JUNIO/JULIO
/// 11-column layout, plus an empty AGOSTO template sheet.
/// </summary>
public static class WorkbookFixtures
{
    public static string BuildPinnedWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"f8urgentes-fixture-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();

        // JUNIO/JULIO standard 11-column layout.
        foreach (var sheetName in new[] { "JUNIO", "JULIO" })
        {
            var ws = workbook.Worksheets.Add(sheetName);
            string[] headers =
            [
                "FECHA PETICION", "NOMBRES", "APELLIDOS", "NOMBRE COMPLETO", "RUT",
                "FECHA ULTIMA CARPETA", "CODIGO F8", "FECHA PENULTIMA CARPETA", "ESTADO", "ESTADO ACTUAL", "FECHA DE SUBIDA",
            ];
            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }

            // Clean row.
            ws.Cell(2, 1).Value = "2024-06-01";
            ws.Cell(2, 2).Value = "Juan";
            ws.Cell(2, 3).Value = "Perez";
            ws.Cell(2, 4).Value = "Juan Perez";
            ws.Cell(2, 5).Value = "15.949.558-2";
            ws.Cell(2, 6).Value = "2024-05-01";
            ws.Cell(2, 7).Value = "F8-001";
            ws.Cell(2, 8).Value = "2024-04-01";
            ws.Cell(2, 9).Value = "PRIMERA LICENCIA";
            ws.Cell(2, 10).Value = "PENDIENTE";
            ws.Cell(2, 11).Value = "2024-06-10";

            // S/C date row.
            ws.Cell(3, 1).Value = "2024-06-02";
            ws.Cell(3, 2).Value = "Maria";
            ws.Cell(3, 3).Value = "Soto";
            ws.Cell(3, 4).Value = "Maria Soto";
            ws.Cell(3, 5).Value = "15949558-2";
            ws.Cell(3, 6).Value = "S/C";
            ws.Cell(3, 9).Value = "SUBIR CON F8";
            ws.Cell(3, 10).Value = "";

            // Bad RUT check digit row.
            ws.Cell(4, 1).Value = "2024-06-03";
            ws.Cell(4, 2).Value = "Pedro";
            ws.Cell(4, 3).Value = "Diaz";
            ws.Cell(4, 4).Value = "Pedro Diaz";
            ws.Cell(4, 5).Value = "15949558-9"; // wrong check digit (correct is -2)
            ws.Cell(4, 9).Value = "CAMBIO DE DOMICILIO";

            // Unrecognized ESTADO row.
            ws.Cell(5, 1).Value = "2024-06-04";
            ws.Cell(5, 2).Value = "Ana";
            ws.Cell(5, 3).Value = "Lopez";
            ws.Cell(5, 4).Value = "Ana Lopez";
            ws.Cell(5, 5).Value = "9876543-3";
            ws.Cell(5, 9).Value = "ESTADO INVENTADO";

            // Fully empty padding row: a whitespace-only cell forces the row into
            // ClosedXML's used range (a purely-blank cell would not be), while the
            // importer's own IsNullOrWhiteSpace handling still treats it as empty.
            ws.Cell(6, 1).Value = " ";
        }

        // MAYO: 10 columns, reordered, FECHA PETICION last, no FECHA DE SUBIDA.
        var mayo = workbook.Worksheets.Add("MAYO");
        string[] mayoHeaders =
        [
            "NOMBRES", "APELLIDOS", "NOMBRE COMPLETO", "RUT",
            "FECHA ULTIMA CARPETA", "CODIGO F8", "FECHA PENULTIMA CARPETA", "ESTADO", "ESTADO ACTUAL", "FECHA PETICION",
        ];
        for (var i = 0; i < mayoHeaders.Length; i++)
        {
            mayo.Cell(1, i + 1).Value = mayoHeaders[i];
        }

        mayo.Cell(2, 1).Value = "Carlos";
        mayo.Cell(2, 2).Value = "Ruiz";
        mayo.Cell(2, 3).Value = "Carlos Ruiz";
        mayo.Cell(2, 4).Value = "15.949.558-2";
        mayo.Cell(2, 5).Value = "2024-04-15";
        mayo.Cell(2, 6).Value = "F8-002";
        mayo.Cell(2, 7).Value = "2024-03-15";
        mayo.Cell(2, 8).Value = "PRIMERA LICENCIA";
        mayo.Cell(2, 9).Value = "PENDIENTE";
        mayo.Cell(2, 10).Value = "2024-05-01";

        // Row 54 in the real workbook has the out-of-range serial 29262516 in the
        // FECHA ULTIMA CARPETA-equivalent cell (H54 in the original 10-col layout, column 5 here).
        for (var r = 3; r <= 53; r++)
        {
            // keep sparse — not needed for row-count assertions beyond row 54
        }
        mayo.Cell(54, 1).Value = "Rosa";
        mayo.Cell(54, 2).Value = "Nunez";
        mayo.Cell(54, 3).Value = "Rosa Nunez";
        mayo.Cell(54, 4).Value = "9876543-3";
        mayo.Cell(54, 5).Value = "29262516"; // real corrupt cell value, out-of-range Excel serial
        mayo.Cell(54, 8).Value = "CREAR CERTIFICADO";
        mayo.Cell(54, 10).Value = "2024-05-05";

        // AGOSTO: empty template, header only.
        var agosto = workbook.Worksheets.Add("AGOSTO");
        for (var i = 0; i < mayoHeaders.Length; i++)
        {
            agosto.Cell(1, i + 1).Value = mayoHeaders[i];
        }

        workbook.SaveAs(path);
        return path;
    }
}
