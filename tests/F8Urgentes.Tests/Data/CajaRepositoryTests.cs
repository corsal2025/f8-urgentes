using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Data;

public sealed class CajaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public CajaRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-caja-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private long Insert(DateOnly? fechaPenultima = null, string rut = "15949558-2") =>
        _repository.Insert(new UrgentRequest
        {
            NombreCompleto = "Juan Perez",
            Rut = rut,
            FechaPenultimaCarpeta = fechaPenultima,
            Estado = "PRIMERA LICENCIA",
            Origin = "Web",
            CreatedAt = DateTimeOffset.UtcNow,
        });

    [Fact]
    public void GetCasesByBoxId_OrdersByTransferOrder_NotByFechaPenultima()
    {
        var t0 = DateTimeOffset.UtcNow;
        var recentFolder = Insert(new DateOnly(2025, 6, 1), "11111111-1");
        var oldFolder = Insert(new DateOnly(2020, 1, 1), "22222222-2");
        var noFolder = Insert(null, "33333333-3");
        _repository.SendToCaja(recentFolder, t0);
        _repository.SendToCaja(noFolder, t0.AddMinutes(1));
        _repository.SendToCaja(oldFolder, t0.AddMinutes(2));
        var box = _repository.CloseBox("A1-PUC", t0.AddMinutes(3));

        var ids = _repository.GetCasesByBoxId(box.Id).Select(c => c.Id).ToArray();

        Assert.Equal([recentFolder, noFolder, oldFolder], ids);
    }

    [Fact]
    public void GetCajaQueue_OrdersByTransferOrder_NotByFechaPenultima()
    {
        var t0 = DateTimeOffset.UtcNow;
        var recentFolder = Insert(new DateOnly(2025, 6, 1), "11111111-1");
        var oldFolder = Insert(new DateOnly(2020, 1, 1), "22222222-2");
        var noFolder = Insert(null, "33333333-3");
        _repository.SendToCaja(recentFolder, t0);
        _repository.SendToCaja(noFolder, t0.AddMinutes(1));
        _repository.SendToCaja(oldFolder, t0.AddMinutes(2));

        var ids = _repository.GetCajaQueue().Select(c => c.Id).ToArray();

        Assert.Equal([recentFolder, noFolder, oldFolder], ids);
    }

    [Fact]
    public void EnsureSchema_CreatesBoxTable()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Box'";
        Assert.NotNull(command.ExecuteScalar());
    }

    [Fact]
    public void SendToCaja_AddsCaseToQueue()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);

        var queue = _repository.GetCajaQueue();

        Assert.Single(queue);
        Assert.Equal(id, queue[0].Id);
    }

    [Fact]
    public void GetCajaQueue_ExcludesCaseNotSentToCaja()
    {
        Insert(new DateOnly(2024, 1, 1));

        Assert.Empty(_repository.GetCajaQueue());
    }

    [Fact]
    public void GetCajaQueue_ExcludesBoxedCases()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        _repository.CloseBox("", DateTimeOffset.UtcNow);

        Assert.Empty(_repository.GetCajaQueue());
    }

    [Fact]
    public void UndoCajaQueue_ReturnsCaseToCasos()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);

        _repository.UndoCajaQueue(id);

        Assert.Empty(_repository.GetCajaQueue());
        var found = _repository.FindById(id)!;
        Assert.Null(found.CajaTransferredAt);
    }

    [Fact]
    public void CloseBox_AssignsBoxIdAndNumbersSequentially()
    {
        var id1 = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id1, DateTimeOffset.UtcNow);
        var box1 = _repository.CloseBox("", DateTimeOffset.UtcNow);

        var id2 = Insert(new DateOnly(2024, 1, 2), rut: "7654321-K");
        _repository.SendToCaja(id2, DateTimeOffset.UtcNow);
        var box2 = _repository.CloseBox("", DateTimeOffset.UtcNow);

        Assert.Equal(1, box1.Number);
        Assert.Equal("A1-PUC", box1.Code);
        Assert.Equal(2, box2.Number);
        Assert.Equal("A2-PUC", box2.Code);

        Assert.Single(_repository.GetCasesByBoxId(box1.Id));
        Assert.Single(_repository.GetCasesByBoxId(box2.Id));
    }

    [Fact]
    public void CloseBox_WithManualCode_UsesGivenCode()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);

        var box = _repository.CloseBox("A5-PUC", DateTimeOffset.UtcNow);

        Assert.Equal("A5-PUC", box.Code);
    }

    [Fact]
    public void ReopenBox_ReturnsCasesToQueueAndDeletesBox()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var box = _repository.CloseBox("", DateTimeOffset.UtcNow);

        _repository.ReopenBox(box.Id);

        Assert.Null(_repository.FindBoxById(box.Id));
        Assert.Single(_repository.GetCajaQueue());
    }

    [Fact]
    public void RemoveCaseFromClosedBox_ReturnsCaseToCasos()
    {
        var id = Insert(new DateOnly(2024, 1, 1));
        _repository.SendToCaja(id, DateTimeOffset.UtcNow);
        var box = _repository.CloseBox("", DateTimeOffset.UtcNow);

        _repository.RemoveCaseFromClosedBox(id);

        var found = _repository.FindById(id)!;
        Assert.Null(found.CajaTransferredAt);
        Assert.Null(found.CajaBoxId);
        Assert.Empty(_repository.GetCasesByBoxId(box.Id));
    }

    [Fact]
    public void SetSinCarpeta_SetsAndReverts()
    {
        var id = Insert(new DateOnly(2024, 1, 1));

        _repository.SetSinCarpeta(id, true);
        Assert.True(_repository.FindById(id)!.SinCarpeta);

        _repository.SetSinCarpeta(id, false);
        Assert.False(_repository.FindById(id)!.SinCarpeta);
    }
}
