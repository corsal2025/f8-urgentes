using F8Urgentes.Data;
using F8Urgentes.Domain;
using F8Urgentes.Tests.Fixtures;

namespace F8Urgentes.Tests.Import;

public sealed class ExcelUrgentImporterTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _workbookPath;
    private readonly UrgentRequestRepository _repository;

    public ExcelUrgentImporterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-import-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
        _workbookPath = WorkbookFixtures.BuildPinnedWorkbook();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (File.Exists(_workbookPath)) File.Delete(_workbookPath);
    }

    [Fact]
    public void Import_ReadsAllThreeSheets_MayoJunioJulio()
    {
        var result = F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        Assert.Equal(3, result.Sheets.Count);
        Assert.Contains(result.Sheets, s => s.SheetName == "MAYO");
        Assert.Contains(result.Sheets, s => s.SheetName == "JUNIO");
        Assert.Contains(result.Sheets, s => s.SheetName == "JULIO");
    }

    [Fact]
    public void Import_SkipsAgostoEmptyTemplateSheet()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var all = _repository.GetAll();
        Assert.DoesNotContain(all, r => r.SourceSheet == "AGOSTO");
    }

    [Fact]
    public void Import_MayoRow_MapsFieldsByNameAndLeavesFechaDeSubidaNull()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var carlos = _repository.GetAll().Single(r => r.NombreCompleto == "Carlos Ruiz");
        Assert.Equal("MAYO", carlos.SourceSheet);
        Assert.Equal("15949558-2", carlos.Rut);
        Assert.Null(carlos.FechaDeSubida);
        Assert.NotNull(carlos.FechaPeticion);
    }

    [Fact]
    public void Import_ScRow_HasNullDateAndNoFlag()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var maria = _repository.GetAll().Single(r => r.NombreCompleto == "Maria Soto" && r.SourceSheet == "JUNIO");
        Assert.Null(maria.FechaUltimaCarpeta);
        Assert.False(maria.NeedsReview);
    }

    [Fact]
    public void Import_OutOfRangeSerial_RealMayoH54_FlaggedNotDropped()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var rosa = _repository.GetAll().Single(r => r.NombreCompleto == "Rosa Nunez");
        Assert.Null(rosa.FechaUltimaCarpeta);
        Assert.True(rosa.NeedsReview);
        var flags = _repository.GetFlagsFor(rosa.Id);
        Assert.Contains(flags, f => f.ReasonCode == ImportFlag.ReasonCodes.DateOutOfRange && f.RawValue == "29262516");
    }

    [Fact]
    public void Import_BadCheckDigitRut_FlaggedNotDropped()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var pedro = _repository.GetAll().Single(r => r.NombreCompleto == "Pedro Diaz" && r.SourceSheet == "JUNIO");
        Assert.NotNull(pedro.Rut);
        Assert.True(pedro.NeedsReview);
        Assert.Contains(_repository.GetFlagsFor(pedro.Id), f => f.ReasonCode == ImportFlag.ReasonCodes.RutCheckDigit);
    }

    [Fact]
    public void Import_UnrecognizedEstado_StoredVerbatimAndFlagged()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var ana = _repository.GetAll().Single(r => r.NombreCompleto == "Ana Lopez" && r.SourceSheet == "JUNIO");
        Assert.Equal("ESTADO INVENTADO", ana.Estado);
        Assert.True(ana.NeedsReview);
        Assert.Contains(_repository.GetFlagsFor(ana.Id), f => f.ReasonCode == ImportFlag.ReasonCodes.UnknownEstado);
    }

    [Fact]
    public void Import_UnrecognizedEstado_PreservesOriginalCasing()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-import-casing-{Guid.NewGuid():N}.db");
        var workbookPath = WorkbookFixtures.BuildPinnedWorkbook();
        var repo = new UrgentRequestRepository($"Data Source={dbPath};Pooling=False");
        repo.EnsureSchema();

        try
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook(workbookPath);
            var sheet = workbook.Worksheet("JUNIO");
            sheet.Cell(5, 9).Value = "estado inventado";
            sheet.Cell(5, 4).Value = "Ana Lopez";
            sheet.Cell(5, 5).Value = "9876543-3";
            workbook.SaveAs(workbookPath);

            F8Urgentes.Import.ExcelUrgentImporter.Import(workbookPath, repo);

            var ana = repo.GetAll().Single(r => r.NombreCompleto == "Ana Lopez" && r.SourceSheet == "JUNIO");
            Assert.Equal("estado inventado", ana.Estado);
        }
        finally
        {
            if (File.Exists(workbookPath)) File.Delete(workbookPath);
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public void Import_FullyEmptyRow_SkippedNoRecordNoFlag()
    {
        var result = F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        var junio = result.Sheets.Single(s => s.SheetName == "JUNIO");
        Assert.True(junio.RowsSkipped >= 1);
    }

    [Fact]
    public void Import_RunningTwiceWithSameSourceFile_IsNoOpSecondTime()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);
        var countAfterFirst = _repository.GetAll().Count;

        var second = F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);

        Assert.True(second.NoOp);
        Assert.Equal(countAfterFirst, _repository.GetAll().Count);
    }

    [Fact]
    public void Import_Force_DeletesAndReimportsWithoutDuplicating()
    {
        F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository);
        var countAfterFirst = _repository.GetAll().Count;

        var forced = F8Urgentes.Import.ExcelUrgentImporter.Import(_workbookPath, _repository, force: true);

        Assert.False(forced.NoOp);
        Assert.Equal(countAfterFirst, _repository.GetAll().Count);
    }
}
