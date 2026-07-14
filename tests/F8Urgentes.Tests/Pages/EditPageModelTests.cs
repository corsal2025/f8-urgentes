using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class EditPageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public EditPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-edit-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath}");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void OnGet_NoId_ProducesEmptyCreateForm()
    {
        var model = new EditModel(_repository);

        model.OnGet(null);

        Assert.Null(model.Input.Id);
        Assert.Null(model.Input.Rut);
    }

    [Fact]
    public void OnGet_WithId_LoadsExistingRequest()
    {
        var id = _repository.Insert(new UrgentRequest { Rut = "15949558-2", NombreCompleto = "Juan Perez", Origin = "Web", CreatedAt = DateTimeOffset.UtcNow });
        var model = new EditModel(_repository);

        model.OnGet(id);

        Assert.Equal("15949558-2", model.Input.Rut);
    }

    [Fact]
    public void OnPost_ValidRut_InsertsNewWebRequestWithNoFlags()
    {
        var model = new EditModel(_repository) { Input = new EditModel.InputModel { Rut = "15.949.558-2", NombreCompleto = "Juan Perez", Estado = "PRIMERA LICENCIA" } };

        var result = model.OnPost();

        Assert.NotNull(result);
        var inserted = Assert.Single(_repository.GetAll());
        Assert.Equal("Web", inserted.Origin);
        Assert.Null(inserted.SourceSheet);
        Assert.Empty(_repository.GetFlagsFor(inserted.Id));
    }

    [Fact]
    public void OnPost_InvalidRutCheckDigit_RejectsSubmission()
    {
        var model = new EditModel(_repository) { Input = new EditModel.InputModel { Rut = "15949558-9", NombreCompleto = "Juan Perez" } };

        model.OnPost();

        Assert.Empty(_repository.GetAll());
        Assert.False(model.ModelState.IsValid);
    }

    [Fact]
    public void OnPost_WithId_UpdatesExistingRequestPreservingIdAndCreatedAt()
    {
        var id = _repository.Insert(new UrgentRequest { Rut = "15949558-2", NombreCompleto = "Juan Perez", Origin = "Web", CreatedAt = DateTimeOffset.UtcNow });
        var original = _repository.FindById(id)!;
        var model = new EditModel(_repository)
        {
            Input = new EditModel.InputModel { Id = id, Rut = "15949558-2", NombreCompleto = "Juan Perez Actualizado", Estado = "CARPETA SUBIDA" },
        };

        model.OnPost();

        var updated = _repository.FindById(id)!;
        Assert.Equal(id, updated.Id);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal("Juan Perez Actualizado", updated.NombreCompleto);
    }

    [Fact]
    public void OnPost_EstadoActualSubidaAConasetWithUploadDate_PersistsFechaDeSubida()
    {
        var model = new EditModel(_repository)
        {
            Input = new EditModel.InputModel
            {
                Rut = "15949558-2",
                NombreCompleto = "Juan Perez",
                EstadoActual = "SUBIDA A CONASET",
                FechaDeSubida = new DateOnly(2024, 6, 10),
            },
        };

        model.OnPost();

        var inserted = Assert.Single(_repository.GetAll());
        Assert.Equal(new DateOnly(2024, 6, 10), inserted.FechaDeSubida);
    }
}
