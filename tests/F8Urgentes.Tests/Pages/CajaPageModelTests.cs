using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class CajaPageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public CajaPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-cajapage-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private long Insert(DateOnly? fecha = null, string rut = "15949558-2") =>
        _repository.Insert(new UrgentRequest
        {
            NombreCompleto = "Juan Perez",
            Rut = rut,
            FechaPenultimaCarpeta = fecha,
            Estado = "PRIMERA LICENCIA",
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });

    private CajaModel CreateModel() => new(_repository)
    {
        PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        },
        TempData = new FakeTempData(),
    };

    // Minimal ITempDataDictionary stand-in so handlers that set [TempData] properties (Message)
    // don't need a full session/provider pipeline wired up just for a unit test.
    private sealed class FakeTempData : Dictionary<string, object?>, Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary
    {
        public void Keep() { }
        public void Keep(string key) { }
        public void Load() { }
        public void Save() { }
        public object? Peek(string key) => TryGetValue(key, out var v) ? v : null;
    }

    [Fact]
    public void OnGet_LoadsQueueAndClosedBoxes()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnGet(null);

        Assert.Single(model.Queue);
        Assert.Empty(model.ClosedBoxes);
    }

    [Fact]
    public void OnPostCerrarCaja_ClosesQueueIntoNewBox()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnPostCerrarCaja("1", null);

        Assert.Single(_repository.GetBoxes());
        Assert.Empty(_repository.GetCajaQueue());
    }

    [Fact]
    public void OnPostReopenBox_ReturnsCasesToQueue()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var box = _repository.CloseBox("", DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnPostReopenBox(box.Id);

        Assert.Null(_repository.FindBoxById(box.Id));
        Assert.Single(_repository.GetCajaQueue());
    }

    [Fact]
    public void OnPostRemoveFromClosedBox_ReturnsCaseToCasos()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var box = _repository.CloseBox("", DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnPostRemoveFromClosedBox(id, box.Id);

        Assert.Null(_repository.FindById(id)!.CajaTransferredAt);
    }

    [Fact]
    public void OnPostUndoSingle_ReturnsCaseToCasos()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnPostUndoSingle(id);

        Assert.Null(_repository.FindById(id)!.CajaTransferredAt);
    }

    [Fact]
    public void OnPostUndoBatch_ReturnsAllSelectedCases()
    {
        var id1 = Insert(new DateOnly(2024, 1, 1));
        var id2 = Insert(new DateOnly(2024, 1, 2), rut: "7654321-K");
        _repository.SendToCaja(id1, DateTimeOffset.UtcNow);
        _repository.SendToCaja(id2, DateTimeOffset.UtcNow);
        var model = CreateModel();

        model.OnPostUndoBatch([id1, id2]);

        Assert.Empty(_repository.GetCajaQueue());
    }

    [Theory]
    [InlineData(null, 3, "A3-PUC")]
    [InlineData("5", 1, "A5-PUC")]
    [InlineData("B2", 1, "B2-PUC")]
    [InlineData("A7-PUC", 1, "A7-PUC")]
    [InlineData("7-PUC", 1, "A7-PUC")]
    public void FormatBoxCode_FormatsVariousInputs(string? input, int defaultNumber, string expected)
    {
        Assert.Equal(expected, CajaModel.FormatBoxCode(input, defaultNumber));
    }
}
