using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class IndexPageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public IndexPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-index-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath}");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private long Insert(string estado = "PRIMERA LICENCIA", DateOnly? fecha = null, bool flagged = false, string rut = "15949558-2")
    {
        var id = _repository.Insert(new UrgentRequest
        {
            Estado = estado,
            FechaPeticion = fecha ?? new DateOnly(2024, 6, 1),
            NombreCompleto = "Juan Perez",
            Rut = rut,
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        if (flagged)
        {
            _repository.AddFlag(id, "Rut", ImportFlag.ReasonCodes.RutCheckDigit, "x");
        }
        return id;
    }

    [Fact]
    public void OnGet_NoFilters_ReturnsAllRequests()
    {
        Insert();
        Insert(rut: "7654321-K");
        var model = new IndexModel(_repository);

        model.OnGet(null, null, null, null, null);

        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public void OnGet_MonthFilter_ReturnsOnlyMatching()
    {
        Insert(fecha: new DateOnly(2024, 6, 1));
        Insert(fecha: new DateOnly(2024, 7, 1), rut: "7654321-K");
        var model = new IndexModel(_repository);

        model.OnGet("2024-06", null, null, null, null);

        Assert.Single(model.Requests);
    }

    [Fact]
    public void OnGet_EstadoFilter_ReturnsOnlyMatching()
    {
        Insert(estado: "PRIMERA LICENCIA");
        Insert(estado: "CAMBIO DE DOMICILIO", rut: "7654321-K");
        var model = new IndexModel(_repository);

        model.OnGet(null, "PRIMERA LICENCIA", null, null, null);

        Assert.Single(model.Requests);
    }

    [Fact]
    public void OnGet_FlaggedTrue_ReturnsOnlyNeedsReview()
    {
        Insert(flagged: true);
        Insert(rut: "7654321-K");
        var model = new IndexModel(_repository);

        model.OnGet(null, null, null, true, null);

        Assert.Single(model.Requests);
        Assert.True(model.Requests[0].NeedsReview);
    }

    [Fact]
    public void OnGet_Search_ReturnsRutOrNameMatches()
    {
        Insert();
        Insert(rut: "7654321-K");
        var model = new IndexModel(_repository);

        model.OnGet(null, null, null, null, "juan");

        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public void OnPostSetEstado_PersistsChangeAndRedirects()
    {
        var id = Insert();
        var model = new IndexModel(_repository);

        var result = model.OnPostSetEstado(id, "CARPETA SUBIDA");

        Assert.Equal("CARPETA SUBIDA", _repository.FindById(id)!.Estado);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostSetEstadoActual_PersistsChangeAndRedirects()
    {
        var id = Insert();
        var model = new IndexModel(_repository);

        var result = model.OnPostSetEstadoActual(id, "SUBIDA A CONASET");

        Assert.Equal("SUBIDA A CONASET", _repository.FindById(id)!.EstadoActual);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnGet_TabPendientes_ExcludesUploadedCases()
    {
        var pendingId = Insert();
        var uploadedId = Insert(rut: "7654321-K");
        _repository.Update(new UrgentRequest { Id = uploadedId, EstadoActual = "SUBIDA A CONASET", Rut = "7654321-K", NombreCompleto = "Juan Perez", Origin = "Web" });
        var model = new IndexModel(_repository);

        model.OnGet(null, null, null, null, null, "Pendientes");

        Assert.Single(model.Requests);
        Assert.Equal(pendingId, model.Requests[0].Id);
    }

    [Fact]
    public void OnGet_TabSubidas_ReturnsOnlyUploadedCases()
    {
        Insert();
        var uploadedId = Insert(rut: "7654321-K");
        _repository.Update(new UrgentRequest { Id = uploadedId, EstadoActual = "SUBIDA A CONASET", Rut = "7654321-K", NombreCompleto = "Juan Perez", Origin = "Web" });
        var model = new IndexModel(_repository);

        model.OnGet(null, null, null, null, null, "Subidas");

        Assert.Single(model.Requests);
        Assert.Equal(uploadedId, model.Requests[0].Id);
    }

    [Fact]
    public void OnPostSetPersonData_PersistsNombreAndRut()
    {
        var id = Insert();
        var model = new IndexModel(_repository);

        var result = model.OnPostSetPersonData(id, "Maria Gonzalez", "7654321-K");

        var found = _repository.FindById(id)!;
        Assert.Equal("Maria Gonzalez", found.NombreCompleto);
        Assert.Equal("7654321-K", found.Rut);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostMarkUploaded_SetsEstadoActualAndFechaDeSubida()
    {
        var id = Insert();
        var model = new IndexModel(_repository);

        var result = model.OnPostMarkUploaded(id);

        var found = _repository.FindById(id)!;
        Assert.Equal("SUBIDA A CONASET", found.EstadoActual);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), found.FechaDeSubida);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostDeleteCase_RemovesRequest()
    {
        var id = Insert();
        var model = new IndexModel(_repository);

        var result = model.OnPostDeleteCase(id);

        Assert.Null(_repository.FindById(id));
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostAddManualCases_InsertsAllValidRows()
    {
        var model = new IndexModel(_repository);

        var result = model.OnPostAddManualCases(
            new List<string> { "Pedro Soto", "Ana Diaz" },
            new List<string> { "15949558-2", "7654321-K" },
            new List<string> { "PRIMERA LICENCIA", "CAMBIO DE DOMICILIO" });

        Assert.Equal(2, _repository.GetAll().Count);
        Assert.NotNull(result);
    }

    [Fact]
    public void DiasHabilesRestantes_ComputesFromFechaPeticion()
    {
        var model = new IndexModel(_repository);
        var request = new UrgentRequest { FechaPeticion = DateOnly.FromDateTime(DateTime.Today) };

        var remaining = model.DiasHabilesRestantes(request);

        Assert.True(remaining <= 15 && remaining >= 0);
    }
}
