using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class IndexPageModelTests : IDisposable
{
    private sealed class TestEmailSender : F8Urgentes.Services.IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

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
    private IndexModel CreateModel() => new(_repository, new TestEmailSender())
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

        model.OnGet(null, null, null, null, null);

        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public void OnGet_MonthFilter_ReturnsOnlyMatching()
    {
        Insert(fecha: new DateOnly(2024, 6, 1));
        Insert(fecha: new DateOnly(2024, 7, 1), rut: "7654321-K");
        var model = CreateModel();

        model.OnGet("2024-06", null, null, null, null);

        Assert.Single(model.Requests);
    }

    [Fact]
    public void OnGet_EstadoFilter_ReturnsOnlyMatching()
    {
        Insert(estado: "PRIMERA LICENCIA");
        Insert(estado: "CAMBIO DE DOMICILIO", rut: "7654321-K");
        var model = CreateModel();

        model.OnGet(null, "PRIMERA LICENCIA", null, null, null);

        Assert.Single(model.Requests);
    }

    [Fact]
    public void OnGet_FlaggedTrue_ReturnsOnlyNeedsReview()
    {
        Insert(flagged: true);
        Insert(rut: "7654321-K");
        var model = CreateModel();

        model.OnGet(null, null, null, true, null);

        Assert.Single(model.Requests);
        Assert.True(model.Requests[0].NeedsReview);
    }

    [Fact]
    public void OnGet_Search_ReturnsRutOrNameMatches()
    {
        Insert();
        Insert(rut: "7654321-K");
        var model = CreateModel();

        model.OnGet(null, null, null, null, "juan");

        Assert.Equal(2, model.Requests.Count);
    }

    [Fact]
    public void OnGet_SortPenultimaAsc_OrdersByFechaPenultimaCarpetaWithNullsLast()
    {
        var noFolderId = InsertWithPenultimaCarpeta(null, "11111111-1");
        var laterId = InsertWithPenultimaCarpeta(new DateOnly(2024, 6, 20), "22222222-2");
        var earlierId = InsertWithPenultimaCarpeta(new DateOnly(2024, 6, 5), "33333333-3");
        var model = CreateModel();

        model.OnGet(null, null, null, null, null, sort: "penultima-asc");

        Assert.Equal([earlierId, laterId, noFolderId], model.Requests.Select(r => r.Id));
    }

    [Fact]
    public void OnGet_SortPenultimaDesc_OrdersByFechaPenultimaCarpetaDescendingWithNullsLast()
    {
        var noFolderId = InsertWithPenultimaCarpeta(null, "11111111-1");
        var laterId = InsertWithPenultimaCarpeta(new DateOnly(2024, 6, 20), "22222222-2");
        var earlierId = InsertWithPenultimaCarpeta(new DateOnly(2024, 6, 5), "33333333-3");
        var model = CreateModel();

        model.OnGet(null, null, null, null, null, sort: "penultima-desc");

        Assert.Equal([laterId, earlierId, noFolderId], model.Requests.Select(r => r.Id));
    }

    private long InsertWithPenultimaCarpeta(DateOnly? fechaPenultimaCarpeta, string rut) =>
        _repository.Insert(new UrgentRequest
        {
            Estado = "PRIMERA LICENCIA",
            FechaPeticion = new DateOnly(2024, 6, 1),
            FechaPenultimaCarpeta = fechaPenultimaCarpeta,
            NombreCompleto = "Juan Perez",
            Rut = rut,
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });

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
        var model = CreateModel();

        model.OnGet(null, null, null, null, null, "Subidas");

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
    public void DeadlineChipText_ShowsPositiveAndNegativeBusinessDays()
    {
        Assert.Equal("+15", IndexModel.DeadlineChipText(new DateOnly(2024, 6, 3), new DateOnly(2024, 6, 3)));
        Assert.Equal("-1", IndexModel.DeadlineChipText(new DateOnly(2024, 6, 25), new DateOnly(2024, 6, 3)));
    }

    [Fact]
    public void DeadlineChipClass_UsesGreenForTimeLeftAndRedWhenOverdue()
    {
        Assert.Equal("badge-ok", IndexModel.DeadlineChipClass(new DateOnly(2024, 6, 3), new DateOnly(2024, 6, 3)));
        Assert.Equal("badge-review", IndexModel.DeadlineChipClass(new DateOnly(2024, 6, 25), new DateOnly(2024, 6, 3)));
    }

    [Fact]
    public void OnPostSetFechaPenultimaCarpeta_ParsesDate()
    {
        var id = Insert();
        var model = CreateModel();

        model.OnPostSetFechaPenultimaCarpeta(id, "15/03/2024");

        Assert.Equal(new DateOnly(2024, 3, 15), _repository.FindById(id)!.FechaPenultimaCarpeta);
    }

    [Fact]
    public void OnPostSetFechaPenultimaCarpeta_ParsesNativeDateInputValue()
    {
        var id = Insert();
        var model = CreateModel();
        model.PageContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = model.OnPostSetFechaPenultimaCarpeta(id, "2024-03-15");

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);
        Assert.Equal("2024-03-15", json.Value!.GetType().GetProperty("fecha")!.GetValue(json.Value));
        Assert.Equal(new DateOnly(2024, 3, 15), _repository.FindById(id)!.FechaPenultimaCarpeta);
    }

    [Fact]
    public void OnPostSetFechaPenultimaCarpeta_SinCarpeta_ClearsDate()
    {
        var id = Insert();
        var model = CreateModel();
        model.OnPostSetFechaPenultimaCarpeta(id, "15/03/2024");

        model.OnPostSetFechaPenultimaCarpeta(id, "S/C");

        Assert.Null(_repository.FindById(id)!.FechaPenultimaCarpeta);
    }

    [Fact]
    public void OnPostSetFechaPenultimaCarpeta_Unparseable_LeavesDateUnchanged()
    {
        var id = Insert();
        var model = CreateModel();
        model.OnPostSetFechaPenultimaCarpeta(id, "15/03/2024");

        model.OnPostSetFechaPenultimaCarpeta(id, "no es una fecha");

        Assert.Equal(new DateOnly(2024, 3, 15), _repository.FindById(id)!.FechaPenultimaCarpeta);
    }

    private static UrgentRequest CompletedCase(DateOnly fechaDeSubida, DateOnly? fechaPenultimaCarpeta, DateTimeOffset? impresoMensualAt = null) => new()
    {
        EstadoActual = "SUBIDA A CONASET",
        FechaDeSubida = fechaDeSubida,
        FechaPenultimaCarpeta = fechaPenultimaCarpeta,
        ImpresoMensualAt = impresoMensualAt,
        Origin = "Web",
    };

    [Fact]
    public void ShouldRemindMonthlyPrint_ExcludesCompletedCasesWithoutPenultimateFolder()
    {
        var today = new DateOnly(2026, 9, 5);
        var noFolder = CompletedCase(new DateOnly(2026, 8, 20), fechaPenultimaCarpeta: null);

        Assert.False(IndexModel.ShouldRemindMonthlyPrint([noFolder], today));
    }

    [Fact]
    public void ShouldRemindMonthlyPrint_TrueWhenUnprintedCaseWithFolderExists()
    {
        var today = new DateOnly(2026, 9, 5);
        var pending = CompletedCase(new DateOnly(2026, 8, 20), new DateOnly(2026, 7, 1));

        Assert.True(IndexModel.ShouldRemindMonthlyPrint([pending], today));
    }

    [Fact]
    public void ShouldRemindMonthlyPrint_FalseAfterBatchMarkedPrinted()
    {
        var today = new DateOnly(2026, 9, 5);
        var printed = CompletedCase(new DateOnly(2026, 8, 20), new DateOnly(2026, 7, 1), DateTimeOffset.UtcNow);

        Assert.False(IndexModel.ShouldRemindMonthlyPrint([printed], today));
    }

    [Fact]
    public void ShouldRemindMonthlyPrint_FalseAfterDay10()
    {
        var afterWindow = new DateOnly(2026, 9, 15);
        var pending = CompletedCase(new DateOnly(2026, 8, 20), new DateOnly(2026, 7, 1));

        Assert.False(IndexModel.ShouldRemindMonthlyPrint([pending], afterWindow));
    }

    [Fact]
    public void MonthlyReminder_ClearsAfterMarkingBatchPrinted_EvenWhenSomeCasesLackPenultimateFolder()
    {
        // In the batch: prev-month completed case WITH a penultimate-folder date.
        _repository.Insert(new UrgentRequest
        {
            EstadoActual = "SUBIDA A CONASET",
            FechaDeSubida = new DateOnly(2026, 8, 10),
            FechaPenultimaCarpeta = new DateOnly(2026, 7, 1),
            Rut = "15949558-2",
            NombreCompleto = "Con carpeta",
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        // Excluded from the batch: prev-month completed case with NO penultimate-folder date —
        // "marcar impreso" never touches it, so it must not keep the banner alive.
        _repository.Insert(new UrgentRequest
        {
            EstadoActual = "SUBIDA A CONASET",
            FechaDeSubida = new DateOnly(2026, 8, 11),
            FechaPenultimaCarpeta = null,
            Rut = "7654321-K",
            NombreCompleto = "Sin carpeta",
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        new ImpresionMensualModel(_repository).OnPostMarkPrinted("2026-08");

        Assert.False(IndexModel.ShouldRemindMonthlyPrint(_repository.GetAll(), new DateOnly(2026, 9, 5)));
    }

    [Fact]
    public void OnGet_ExcludesCasesSentToCaja()
    {
        var keptId = Insert(rut: "11111111-1");
        var cajaId = Insert(rut: "22222222-2");
        _repository.SendToCaja(cajaId, DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnGet(null, null, null, null, null);

        Assert.Equal([keptId], model.Requests.Select(r => r.Id));
    }

    [Fact]
    public void OnGet_StillShowsSinCarpetaCases()
    {
        var id = Insert(rut: "11111111-1");
        _repository.SetSinCarpeta(id, true);
        var model = CreateModel();

        model.OnGet(null, null, null, null, null);

        Assert.Single(model.Requests);
        Assert.True(model.Requests[0].SinCarpeta);
    }

    [Fact]
    public void OnPostSetSinCarpeta_MarksCase()
    {
        var id = Insert();
        var model = CreateModel();

        model.OnPostSetSinCarpeta(id);

        Assert.True(_repository.FindById(id)!.SinCarpeta);
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
    public void OnPostSendToCaja_SendsCaseToQueueAndRemovesFromIndex()
    {
        var id = Insert();
        var model = CreateModel();

        model.OnPostSendToCaja(id);

        Assert.NotNull(_repository.FindById(id)!.CajaTransferredAt);
        model.OnGet(null, null, null, null, null);
        Assert.Empty(model.Requests);
    }
}
