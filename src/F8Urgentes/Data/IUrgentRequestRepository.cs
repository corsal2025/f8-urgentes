using F8Urgentes.Domain;

namespace F8Urgentes.Data;

public sealed record UrgentRequestFilter(
    string? Month = null,
    string? Estado = null,
    string? EstadoActual = null,
    bool? Flagged = null,
    DateOnly? FechaDesde = null,
    DateOnly? FechaHasta = null);

public interface IUrgentRequestRepository
{
    void EnsureSchema();

    long Insert(UrgentRequest request);
    UrgentRequest? FindById(long id);
    void Update(UrgentRequest request);
    void Delete(long id);
    IReadOnlyList<UrgentRequest> GetAll();
    IReadOnlyList<UrgentRequest> Query(UrgentRequestFilter filter, string? search);

    void AddFlag(long requestId, string columnName, string reasonCode, string? rawValue);
    IReadOnlyList<ImportFlag> GetFlagsFor(long requestId);
    IReadOnlyList<UrgentRequest> GetFlagged();
    void ClearFlags(long requestId);

    void SetMarked(long id, bool marked);

    UrgentRequest? FindByRut(string rut);

    // User management
    Usuario? FindUserByUsername(string username);
    void InsertUser(Usuario usuario);
    void UpdateUserPassword(long id, string passwordHash);

    /// <summary>Every closed box, most recently closed first.</summary>
    IReadOnlyList<Box> GetBoxes();
}
