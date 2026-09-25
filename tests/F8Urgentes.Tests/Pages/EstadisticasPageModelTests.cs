using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class EstadisticasPageModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 24); // Thursday

    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public EstadisticasPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-estadisticas-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private void Insert(DateOnly? peticion, DateOnly? subida = null, string? matrizSector = null)
    {
        _repository.Insert(new UrgentRequest
        {
            NombreCompleto = "Juan Perez",
            Rut = "15949558-2",
            FechaPeticion = peticion,
            EstadoActual = subida is null ? null : "SUBIDA A CONASET",
            FechaDeSubida = subida,
            MatrizSector = matrizSector,
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    private EstadisticasModel Load()
    {
        var model = new EstadisticasModel(_repository);
        model.Load(Today);
        return model;
    }

    [Fact]
    public void Load_CountsOverduePendingCases()
    {
        Insert(new DateOnly(2026, 8, 1));  // way over 15 business days
        Insert(new DateOnly(2026, 9, 22)); // 2 business days
        Insert(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)); // uploaded, not pending

        var model = Load();

        Assert.Equal(1, model.PendientesVencidos);
    }

    [Fact]
    public void Load_ComputesMedianDaysToConfirmation()
    {
        Insert(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));  // 2
        Insert(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5));  // 4
        Insert(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 21)); // 20

        var model = Load();

        Assert.Equal(4, model.MedianaDiasConfirmacion);
    }

    [Fact]
    public void Load_BuildsLastTwelveMonthsOfIngresosAndSubidas()
    {
        Insert(new DateOnly(2026, 8, 10), new DateOnly(2026, 9, 2));
        Insert(new DateOnly(2026, 9, 1));
        Insert(new DateOnly(2024, 1, 1)); // outside window

        var model = Load();

        Assert.Equal(12, model.MensualIngresosSubidas.Count);
        var sep = model.MensualIngresosSubidas[^1];
        var aug = model.MensualIngresosSubidas[^2];
        Assert.Equal((1, 1), (sep.Ingresos, sep.Subidas));
        Assert.Equal((1, 0), (aug.Ingresos, aug.Subidas));
    }

    [Fact]
    public void Load_GroupsIngresosByWeekday()
    {
        Insert(new DateOnly(2026, 9, 21)); // Monday
        Insert(new DateOnly(2026, 9, 28)); // Monday
        Insert(new DateOnly(2026, 9, 23)); // Wednesday

        var model = Load();

        Assert.Equal(2, model.IngresosPorDiaSemana.Single(d => d.Label == "Lun").Count);
        Assert.Equal(1, model.IngresosPorDiaSemana.Single(d => d.Label == "Mié").Count);
        Assert.Equal(0, model.IngresosPorDiaSemana.Single(d => d.Label == "Vie").Count);
    }

    [Fact]
    public void Load_SplitsSectorOficinaIntoSubidasAndPendientes()
    {
        Insert(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "PLACILLA");
        Insert(new DateOnly(2026, 9, 1), null, "PLACILLA");
        Insert(new DateOnly(2026, 9, 1), null, "PLACILLA");

        var model = Load();

        var placilla = model.SectorOficinaEstado.Single(s => s.Label == "PLACILLA");
        Assert.Equal((1, 2), (placilla.Subidas, placilla.Pendientes));
    }

    [Fact]
    public void Load_ClassifiesFoldersBySinCarpetaQueueAndBox()
    {
        Insert(new DateOnly(2026, 9, 1)); // sin clasificar
        Insert(null);                     // sin clasificar, no penultimate date — still counted
        var all = _repository.GetAll();
        _repository.SetSinCarpeta(all[0].Id, true);
        Insert(new DateOnly(2026, 9, 1));
        Insert(new DateOnly(2026, 9, 1));
        var ids = _repository.GetAll().Where(r => !r.SinCarpeta).Select(r => r.Id).Take(2).ToList();
        _repository.SendToCaja(ids[0], DateTimeOffset.UtcNow);
        _repository.CloseBox("A1-PUC", DateTimeOffset.UtcNow);
        _repository.SendToCaja(ids[1], DateTimeOffset.UtcNow);

        var model = Load();

        var c = model.ClasificacionCarpetas.ToDictionary(x => x.Label, x => x.Count);
        Assert.Equal(1, c["En caja cerrada"]);
        Assert.Equal(1, c["En cola de caja"]);
        Assert.Equal(1, c["Sin carpeta"]);
        Assert.Equal(1, c["Sin clasificar"]);
        Assert.Equal(1, model.CajasCerradas);
        Assert.Equal(("A1-PUC", 1), model.CarpetasPorCaja.Single());
    }

    [Fact]
    public void Load_ComputesLegalDeadlineComplianceByUploadMonth()
    {
        Insert(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));  // on time
        Insert(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 10)); // late

        var model = Load();

        var sep = model.CumplimientoPorMes[^1];
        Assert.Equal((1, 1), (sep.Dentro, sep.Fuera));
    }
}
