using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Data;

public sealed class UrgentRequestRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public UrgentRequestRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath}");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private static UrgentRequest SampleRequest(string rut = "15949558-2", string origin = "Web") => new()
    {
        FechaPeticion = new DateOnly(2024, 6, 1),
        Nombres = "Juan",
        Apellidos = "Perez",
        NombreCompleto = "Juan Perez",
        Rut = rut,
        RutRaw = rut,
        Estado = "PRIMERA LICENCIA",
        EstadoActual = "PENDIENTE",
        Origin = origin,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void EnsureSchema_CreatesExpectedTables()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("UrgentRequest", tables);
        Assert.Contains("ImportFlag", tables);
        Assert.Contains("ImportRun", tables);
    }

    [Fact]
    public void Insert_ReturnsGeneratedId()
    {
        var id = _repository.Insert(SampleRequest());
        Assert.True(id > 0);
    }

    [Fact]
    public void FindById_RoundTripsAllFieldsIncludingNullableDates()
    {
        var request = SampleRequest();
        request.FechaUltimaCarpeta = null;
        var id = _repository.Insert(request);

        var found = _repository.FindById(id);

        Assert.NotNull(found);
        Assert.Equal(request.Nombres, found!.Nombres);
        Assert.Equal(request.Rut, found.Rut);
        Assert.Equal(request.FechaPeticion, found.FechaPeticion);
        Assert.Null(found.FechaUltimaCarpeta);
    }

    [Fact]
    public void Update_PersistsFieldChangeAndUpdatesUpdatedAt()
    {
        var id = _repository.Insert(SampleRequest());
        var found = _repository.FindById(id)!;
        found.Estado = "CAMBIO DE DOMICILIO";

        _repository.Update(found);

        var updated = _repository.FindById(id)!;
        Assert.Equal("CAMBIO DE DOMICILIO", updated.Estado);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public void GetAll_ReturnsInsertedRows()
    {
        _repository.Insert(SampleRequest("15949558-2"));
        _repository.Insert(SampleRequest("7654321-K"));

        var all = _repository.GetAll();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Query_FiltersByMonthOfFechaPeticion()
    {
        var june = SampleRequest();
        june.FechaPeticion = new DateOnly(2024, 6, 15);
        var july = SampleRequest();
        july.FechaPeticion = new DateOnly(2024, 7, 15);
        _repository.Insert(june);
        _repository.Insert(july);

        var results = _repository.Query(new UrgentRequestFilter("2024-06", null, null, null), null);

        Assert.Single(results);
        Assert.Equal(new DateOnly(2024, 6, 15), results[0].FechaPeticion);
    }

    [Fact]
    public void Query_FiltersByEstado()
    {
        var a = SampleRequest();
        a.Estado = "PRIMERA LICENCIA";
        var b = SampleRequest();
        b.Estado = "CAMBIO DE DOMICILIO";
        _repository.Insert(a);
        _repository.Insert(b);

        var results = _repository.Query(new UrgentRequestFilter(null, "PRIMERA LICENCIA", null, null), null);

        Assert.Single(results);
        Assert.Equal("PRIMERA LICENCIA", results[0].Estado);
    }

    [Fact]
    public void Query_FiltersByEstadoActual()
    {
        var a = SampleRequest();
        a.EstadoActual = "PENDIENTE";
        var b = SampleRequest();
        b.EstadoActual = "SUBIDA A CONASET";
        _repository.Insert(a);
        _repository.Insert(b);

        var results = _repository.Query(new UrgentRequestFilter(null, null, "SUBIDA A CONASET", null), null);

        Assert.Single(results);
        Assert.Equal("SUBIDA A CONASET", results[0].EstadoActual);
    }

    [Fact]
    public void Query_SearchesByNormalizedRutIgnoringDotsInInput()
    {
        _repository.Insert(SampleRequest("15949558-2"));
        _repository.Insert(SampleRequest("7654321-K"));

        var results = _repository.Query(new UrgentRequestFilter(null, null, null, null), "15.949.558-2");

        Assert.Single(results);
        Assert.Equal("15949558-2", results[0].Rut);
    }

    [Fact]
    public void Query_SearchesByPartialName()
    {
        _repository.Insert(SampleRequest());

        var results = _repository.Query(new UrgentRequestFilter(null, null, null, null), "juan");

        Assert.Single(results);
    }

    [Fact]
    public void Query_CombinesMonthAndEstadoFilters()
    {
        var match = SampleRequest();
        match.FechaPeticion = new DateOnly(2024, 6, 1);
        match.Estado = "PRIMERA LICENCIA";
        var wrongMonth = SampleRequest();
        wrongMonth.FechaPeticion = new DateOnly(2024, 7, 1);
        wrongMonth.Estado = "PRIMERA LICENCIA";
        _repository.Insert(match);
        _repository.Insert(wrongMonth);

        var results = _repository.Query(new UrgentRequestFilter("2024-06", "PRIMERA LICENCIA", null, null), null);

        Assert.Single(results);
    }

    [Fact]
    public void AddFlag_InsertsChildRowAndSetsNeedsReview()
    {
        var id = _repository.Insert(SampleRequest());

        _repository.AddFlag(id, "Rut", ImportFlag.ReasonCodes.RutCheckDigit, "15949558-9");

        var found = _repository.FindById(id)!;
        Assert.True(found.NeedsReview);
        Assert.Single(_repository.GetFlagsFor(id));
    }

    [Fact]
    public void GetFlagged_ReturnsOnlyNeedsReviewRows()
    {
        var flaggedId = _repository.Insert(SampleRequest());
        _repository.Insert(SampleRequest());
        _repository.AddFlag(flaggedId, "Rut", ImportFlag.ReasonCodes.RutCheckDigit, "x");

        var flagged = _repository.GetFlagged();

        Assert.Single(flagged);
        Assert.Equal(flaggedId, flagged[0].Id);
    }

    [Fact]
    public void ClearFlags_DeletesChildRowsAndResetsNeedsReview()
    {
        var id = _repository.Insert(SampleRequest());
        _repository.AddFlag(id, "Rut", ImportFlag.ReasonCodes.RutCheckDigit, "x");

        _repository.ClearFlags(id);

        Assert.False(_repository.FindById(id)!.NeedsReview);
        Assert.Empty(_repository.GetFlagsFor(id));
    }

    [Fact]
    public void HasCompletedImport_FalseBeforeAnyRun_TrueAfterRecordImportRun()
    {
        Assert.False(_repository.HasCompletedImport("URGENTES DIARIOS.xlsx"));

        _repository.RecordImportRun("URGENTES DIARIOS.xlsx", 100, 5);

        Assert.True(_repository.HasCompletedImport("URGENTES DIARIOS.xlsx"));
        Assert.False(_repository.HasCompletedImport("otro.xlsx"));
    }

    [Fact]
    public void DeleteImportedRows_RemovesOnlyImportOriginRows()
    {
        _repository.Insert(SampleRequest("15949558-2", "Import"));
        _repository.Insert(SampleRequest("7654321-K", "Web"));

        _repository.DeleteImportedRows();

        var remaining = _repository.GetAll();
        Assert.Single(remaining);
        Assert.Equal("Web", remaining[0].Origin);
    }
}
