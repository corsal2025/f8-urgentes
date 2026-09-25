using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class ImpresionMensualPageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public ImpresionMensualPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-impresionmensual-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private long Insert(DateOnly? fechaPenultimaCarpeta, DateOnly fechaDeSubida)
    {
        return _repository.Insert(new UrgentRequest
        {
            NombreCompleto = "Juan Perez",
            Rut = "15949558-2",
            FechaPenultimaCarpeta = fechaPenultimaCarpeta,
            EstadoActual = "SUBIDA A CONASET",
            FechaDeSubida = fechaDeSubida,
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public void OnGet_ExcludesPeopleWithoutPenultimateFolder()
    {
        var withFolder = Insert(new DateOnly(2026, 7, 10), new DateOnly(2026, 8, 5));
        Insert(null, new DateOnly(2026, 8, 6)); // no penultimate folder — excluded

        var model = new ImpresionMensualModel(_repository);
        model.OnGet("2026-08");

        Assert.Equal([withFolder], model.Cases.Select(c => c.Id));
    }

    [Fact]
    public void OnPostMarkPrinted_SkipsPeopleWithoutPenultimateFolder()
    {
        var noFolder = Insert(null, new DateOnly(2026, 8, 6));

        var model = new ImpresionMensualModel(_repository);
        model.OnPostMarkPrinted("2026-08");

        Assert.Null(_repository.FindById(noFolder)!.ImpresoMensualAt);
    }
}
