using ClosedXML.Excel;
using F8Urgentes.Data;
using F8Urgentes.Matriz;
using Xunit;

namespace F8Urgentes.Tests.Matriz;

public sealed class MatrizSyncServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _workbookPath;
    private readonly UrgentRequestRepository _repository;

    public MatrizSyncServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-matriz-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();

        _workbookPath = Path.Combine(Path.GetTempPath(), $"matriz-fixture-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("2DO SEM. AV. ARGENTINA");

        // Row 1: Title/blank
        ws.Cell(1, 6).Value = "AGENDA MENSUAL AV. ARGENTINA";

        // Row 2: Headers with "Columna 10" instead of "ESTADO DE LA CARPETA"
        ws.Cell(2, 1).Value = "FECHA DE LA CITACIÓN";
        ws.Cell(2, 2).Value = "FECHA CUANDO SE SUBIO LA CARPETA";
        ws.Cell(2, 3).Value = "FECHA ULTIMA CARPETA";
        ws.Cell(2, 4).Value = "NOMBRE";
        ws.Cell(2, 5).Value = "APELLIDO";
        ws.Cell(2, 6).Value = "NOMBRE COMPLETO";
        ws.Cell(2, 7).Value = "RUT";
        ws.Cell(2, 8).Value = "ATENCION";
        ws.Cell(2, 9).Value = "IDEONIDAD MORAL";
        ws.Cell(2, 10).Value = "Columna 10"; // Typical Excel auto-generated column name
        ws.Cell(2, 11).Value = "DECISIÓN FINAL";

        // Row 3: Case with "NO EXISTE CARPETA"
        ws.Cell(3, 1).Value = "2024-08-15";
        ws.Cell(3, 6).Value = "CARLOS ANDRES MENDOZA RIOS";
        ws.Cell(3, 7).Value = "18.785.387-7";
        ws.Cell(3, 10).Value = "NO EXISTE CARPETA";

        // Row 4: Case already completed (should NOT be imported as new urgent case)
        ws.Cell(4, 1).Value = "2024-08-16";
        ws.Cell(4, 6).Value = "ANA MARIA PEREZ SOTO";
        ws.Cell(4, 7).Value = "15.949.558-2";
        ws.Cell(4, 10).Value = "SUBIDA A CONASET";

        workbook.SaveAs(_workbookPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (File.Exists(_workbookPath)) File.Delete(_workbookPath);
    }

    [Fact]
    public void Sync_ImportsCases_EvenWhenHeaderIsColumna10()
    {
        var result = MatrizSyncService.Sync(_workbookPath, _repository);

        Assert.False(result.NoOp);
        Assert.False(result.ExcelLocked);
        Assert.Equal(1, result.CasesCreated);

        var imported = _repository.FindByRut("18785387-7");
        Assert.NotNull(imported);
        Assert.Equal("CARLOS ANDRES MENDOZA RIOS", imported.NombreCompleto);
        Assert.Equal("AV. ARGENTINA", imported.MatrizSector);
        Assert.Equal("Matriz", imported.Origin);
    }

    [Fact]
    public void Sync_ImportsCasesAfterIntermediateEmptyRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"matriz-empty-rows-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("2DO SEM. AV. ARGENTINA");

                ws.Cell(2, 1).Value = "FECHA DE LA CITACIÓN";
                ws.Cell(2, 2).Value = "FECHA CUANDO SE SUBIO LA CARPETA";
                ws.Cell(2, 6).Value = "NOMBRE COMPLETO";
                ws.Cell(2, 7).Value = "RUT";
                ws.Cell(2, 10).Value = "ESTADO DE LA CARPETA";

                // Row 3: existing completed case
                ws.Cell(3, 1).Value = "2026-10-30";
                ws.Cell(3, 6).Value = "ANA MARIA PEREZ SOTO";
                ws.Cell(3, 7).Value = "15.949.558-2";
                ws.Cell(3, 10).Value = "SUBIDA A CONASET";

                // Rows 4 and 5: two intermediate empty rows (materialized in XML)
                ws.Cell(4, 1).Value = "";
                ws.Cell(5, 1).Value = "";

                // Row 6: November 2026 case after the empty rows
                ws.Cell(6, 1).Value = "2026-11-05";
                ws.Cell(6, 6).Value = "JUAN PABLO TORRES DIAZ";
                ws.Cell(6, 7).Value = "12.345.678-5";
                ws.Cell(6, 10).Value = "NO EXISTE CARPETA";

                workbook.SaveAs(path);
            }

            var result = MatrizSyncService.Sync(path, _repository);

            Assert.False(result.NoOp);
            Assert.False(result.ExcelLocked);
            Assert.Equal(1, result.CasesCreated);

            var imported = _repository.FindByRut("12345678-5");
            Assert.NotNull(imported);
            Assert.Equal("JUAN PABLO TORRES DIAZ", imported.NombreCompleto);
            Assert.Equal("AV. ARGENTINA", imported.MatrizSector);
            Assert.Equal(6, imported.SourceRowNumber);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
