using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Pages;

public sealed class ReviewPageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public ReviewPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-review-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void OnGet_ListsOnlyNeedsReviewRequestsWithFlagDetail()
    {
        var flaggedId = _repository.Insert(new UrgentRequest { Rut = "15949558-9", RutRaw = "15949558-9", NombreCompleto = "Juan Perez", Origin = "Import", CreatedAt = DateTimeOffset.UtcNow });
        _repository.AddFlag(flaggedId, "Rut", ImportFlag.ReasonCodes.RutCheckDigit, "15949558-9");
        _repository.Insert(new UrgentRequest { Rut = "7654321-K", NombreCompleto = "Ana Lopez", Origin = "Import", CreatedAt = DateTimeOffset.UtcNow });

        var model = new ReviewModel(_repository);
        model.OnGet();

        var row = Assert.Single(model.FlaggedRequests);
        Assert.Equal(flaggedId, row.Request.Id);
        Assert.Contains(row.Flags, f => f.ColumnName == "Rut" && f.ReasonCode == ImportFlag.ReasonCodes.RutCheckDigit && f.RawValue == "15949558-9");
    }

    [Fact]
    public void AfterEditSaveClearsFlags_RowNoLongerAppearsInReview()
    {
        var id = _repository.Insert(new UrgentRequest { Rut = "15949558-9", NombreCompleto = "Juan Perez", Origin = "Import", CreatedAt = DateTimeOffset.UtcNow });
        _repository.AddFlag(id, "Rut", ImportFlag.ReasonCodes.RutCheckDigit, "15949558-9");

        _repository.ClearFlags(id);

        var model = new ReviewModel(_repository);
        model.OnGet();
        Assert.Empty(model.FlaggedRequests);
    }
}
