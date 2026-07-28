using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class SectorF8PageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public SectorF8PageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-sectorf8-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath}");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private long Insert(DateOnly fechaPenultimaCarpeta, bool marked = false, bool pendienteCarpeta = false)
    {
        var id = _repository.Insert(new UrgentRequest
        {
            NombreCompleto = "Juan Perez",
            Rut = "15949558-2",
            FechaPenultimaCarpeta = fechaPenultimaCarpeta,
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        if (marked) _repository.SetMarked(id, true);
        if (pendienteCarpeta) _repository.SetPendienteCarpeta(id, true);
        return id;
    }

    [Fact]
    public void OnGet_FiltersByMarkedOrPendienteAndSector()
    {
        var oficina43Marked = Insert(new DateOnly(2024, 1, 1), marked: true);
        Insert(new DateOnly(2024, 1, 1)); // not marked, not pendiente — excluded
        Insert(new DateOnly(2023, 1, 1), marked: true); // wrong sector (Archivo) — excluded

        var model = new SectorF8Model(_repository);
        model.OnGet(FolderSector.Oficina43);

        Assert.Equal([oficina43Marked], model.Cases.Select(c => c.Id));
    }

    [Fact]
    public void OnGet_ExcludesAlreadyPrinted()
    {
        var id = Insert(new DateOnly(2024, 1, 1), marked: true);
        _repository.SetSectorPdfGenerated(id, DateTimeOffset.UtcNow);

        var model = new SectorF8Model(_repository);
        model.OnGet(FolderSector.Oficina43);

        Assert.Empty(model.Cases);
    }

    [Fact]
    public void OnPostClearAll_MarksAllVisibleAsPrinted()
    {
        var id = Insert(new DateOnly(2024, 1, 1), pendienteCarpeta: true);

        var model = new SectorF8Model(_repository);
        model.OnPostClearAll(FolderSector.Oficina43);

        Assert.NotNull(_repository.FindById(id)!.SectorPdfGeneratedAt);
    }

    [Fact]
    public void OnPostRemoveOne_MarksSingleCaseAsPrinted()
    {
        var id = Insert(new DateOnly(2024, 1, 1), marked: true);
        var other = Insert(new DateOnly(2024, 2, 1), marked: true);

        var model = new SectorF8Model(_repository);
        model.OnPostRemoveOne(id, FolderSector.Oficina43);

        Assert.NotNull(_repository.FindById(id)!.SectorPdfGeneratedAt);
        Assert.Null(_repository.FindById(other)!.SectorPdfGeneratedAt);
    }
}
