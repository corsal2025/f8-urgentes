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
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
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

    // Handlers that echo JSON for AJAX callers read Request.Headers, so the model needs a live
    // HttpContext even in unit tests — without it PageModel.Request throws NullReferenceException.
    private IndexModel CreateModel() => new(_repository)
    {
        PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        },
    };

    [Fact]
    public void OnGet_NoFilters_ReturnsAllRequests()
    {
        Insert();
        Insert(rut: "7654321-K");
        var model = CreateModel();

        model.OnGet(null, null);

        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public void OnGet_FlaggedTrue_ReturnsOnlyNeedsReview()
    {
        Insert(flagged: true);
        Insert(rut: "7654321-K");
        var model = CreateModel();

        model.OnGet(true, null);

        Assert.Single(model.Requests);
        Assert.True(model.Requests[0].NeedsReview);
    }

    [Fact]
    public void OnGet_Search_ReturnsRutOrNameMatches()
    {
        Insert();
        Insert(rut: "7654321-K");
        var model = CreateModel();

        model.OnGet(null, "juan");

        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public void OnPostSetEstadoActual_PersistsChangeAndRedirects()
    {
        var id = Insert();
        var model = CreateModel();

        var result = model.OnPostSetEstadoActual(id, "SUBIDA A CONASET");

        Assert.Equal("SUBIDA A CONASET", _repository.FindById(id)!.EstadoActual);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostSetEstadoActual_SetsFechaDeSubidaWhenMarkedAsUploaded()
    {
        var id = Insert();
        var model = CreateModel();

        model.OnPostSetEstadoActual(id, "SUBIDA A CONASET");

        var found = _repository.FindById(id)!;
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), found.FechaDeSubida);
    }

    [Fact]
    public void OnPostSetEstadoActual_ClearsReviewWhenUnknownValueBecomesValid()
    {
        var id = Insert();
        var request = _repository.FindById(id)!;
        request.EstadoActual = "ESTADO ACTUAL INVENTADO";
        request.NeedsReview = true;
        _repository.Update(request);
        _repository.AddFlag(id, "EstadoActual", ImportFlag.ReasonCodes.UnknownEstadoActual, "ESTADO ACTUAL INVENTADO");

        var model = CreateModel();

        model.OnPostSetEstadoActual(id, "PENDIENTE");

        var updated = _repository.FindById(id)!;
        Assert.Equal("PENDIENTE", updated.EstadoActual);
        Assert.False(updated.NeedsReview);
        Assert.Empty(_repository.GetFlagsFor(id));
    }

    [Fact]
    public void OnGet_TabPendientes_ExcludesUploadedCases()
    {
        var pendingId = Insert();
        var uploadedId = Insert(rut: "7654321-K");
        _repository.Update(new UrgentRequest { Id = uploadedId, EstadoActual = "SUBIDA A CONASET", Rut = "7654321-K", NombreCompleto = "Juan Perez", Origin = "Web" });
        var model = CreateModel();

        model.OnGet(null, null, "Pendientes");

        Assert.Single(model.Requests);
        Assert.Equal(pendingId, model.Requests[0].Id);
    }

    [Fact]
    public void OnGet_TabSubidas_ReturnsOnlyUploadedCases()
    {
        Insert();
        var uploadedId = Insert(rut: "7654321-K");
        _repository.Update(new UrgentRequest { Id = uploadedId, EstadoActual = "SUBIDA A CONASET", Rut = "7654321-K", NombreCompleto = "Juan Perez", Origin = "Web" });
        var model = CreateModel();

        model.OnGet(null, null, "Subidas");

        Assert.Single(model.Requests);
        Assert.Equal(uploadedId, model.Requests[0].Id);
    }

    [Fact]
    public void OnPostSetPersonData_PersistsNombreAndRut()
    {
        var id = Insert();
        var model = CreateModel();

        var result = model.OnPostSetPersonData(id, "Maria Gonzalez", "7654321-K");

        var found = _repository.FindById(id)!;
        Assert.Equal("Maria Gonzalez", found.NombreCompleto);
        Assert.Equal("07654321-K", found.Rut);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostMarkUploaded_SetsEstadoActualAndFechaDeSubida()
    {
        var id = Insert();
        var model = CreateModel();

        var result = model.OnPostMarkUploaded(id);

        var found = _repository.FindById(id)!;
        Assert.Equal("SUBIDA A CONASET", found.EstadoActual);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), found.FechaDeSubida);
        Assert.False(found.PendienteEscrituraExcel);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostMarkUploaded_DoesNotQueueExcelWriteBack()
    {
        var id = Insert();
        var request = _repository.FindById(id)!;
        request.SourceSheet = "2DO SEM. AV. ARGENTINA";
        request.SourceRowNumber = 12;
        _repository.Update(request);

        var model = CreateModel();
        var result = model.OnPostMarkUploaded(id);

        var found = _repository.FindById(id)!;
        Assert.Equal("SUBIDA A CONASET", found.EstadoActual);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), found.FechaDeSubida);
        Assert.False(found.PendienteEscrituraExcel);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostDeleteCase_RemovesRequest()
    {
        var id = Insert();
        var model = CreateModel();

        var result = model.OnPostDeleteCase(id);

        Assert.Null(_repository.FindById(id));
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostAddManualCases_InsertsAllValidRows()
    {
        var model = CreateModel();

        var result = model.OnPostAddManualCases(
            new List<string> { "Pedro Soto", "Ana Diaz" },
            new List<string> { "15949558-2", "7654321-K" },
            new List<string> { "PRIMERA LICENCIA", "CAMBIO DE DOMICILIO" });

        Assert.Equal(2, _repository.GetAll().Count);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnPostAddManualCases_WithoutEstado_InsertsRowsWithNullEstado()
    {
        var model = CreateModel();

        var result = model.OnPostAddManualCases(
            new List<string> { "Pedro Soto", "Ana Diaz" },
            new List<string> { "15949558-2", "7654321-K" },
            fechaPenultimaCarpeta: new List<string?> { "2024-05-10", "2024-06-12" });

        var cases = _repository.GetAll();
        Assert.Equal(2, cases.Count);
        Assert.All(cases, c => Assert.Null(c.Estado));
        Assert.NotNull(result);
    }

    [Fact]
    public void OnGet_StillShowsSinCarpetaCases()
    {
        var id = Insert(rut: "11111111-1");
        _repository.SetSinCarpeta(id, true);
        var model = CreateModel();

        model.OnGet(null, null);

        Assert.Single(model.Requests);
        Assert.True(model.Requests[0].SinCarpeta);
    }

    [Fact]
    public void OnPostRevertSinCarpeta_UnmarksCase()
    {
        var id = Insert();
        _repository.SetSinCarpeta(id, true);
        var model = CreateModel();

        model.OnPostRevertSinCarpeta(id);

        Assert.False(_repository.FindById(id)!.SinCarpeta);
    }

    [Fact]
    public void BuildUrl_PreservesActiveFilters()
    {
        var model = CreateModel();
        model.OnGet(true, search: "Perez", tab: "Pendientes");

        var urlNewTab = model.BuildUrl(newTab: "Subidas");
        Assert.Contains("tab=Subidas", urlNewTab);
        Assert.Contains("search=Perez", urlNewTab);
        Assert.Contains("flagged=true", urlNewTab);

        var urlClearFlagged = model.BuildUrl(newFlagged: false);
        Assert.DoesNotContain("flagged=true", urlClearFlagged);
        Assert.Contains("search=Perez", urlClearFlagged);
    }
}
