using F8Urgentes.Domain;

namespace F8Urgentes.Data;

public sealed record UrgentRequestFilter(string? Month, string? Estado, string? EstadoActual, bool? Flagged);

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

    bool HasCompletedImport(string sourceFile);
    void RecordImportRun(string sourceFile, int rowsImported, int rowsFlagged);
    void DeleteImportedRows();

    void SetMarked(long id, bool marked);
    void SetPendienteCarpeta(long id, bool pendienteCarpeta);
    void SetSectorPdfGenerated(long id, DateTimeOffset generatedAt);
    void SetImpresoMensual(long id, DateTimeOffset? generatedAt);

    UrgentRequest? FindByRut(string rut);
    void SetPendienteEscrituraExcel(long id, bool pendiente);
    IReadOnlyList<UrgentRequest> GetPendingEscrituraExcel();

    // User management
    Usuario? FindUserByUsername(string username);
    void InsertUser(Usuario usuario);
    void UpdateUserPassword(long id, string passwordHash);

    // Caja module
    void SetSinCarpeta(long id, bool sinCarpeta);

    /// <summary>Sends a case to the Caja open queue (Destination-equivalent transition).</summary>
    void SendToCaja(long id, DateTimeOffset transferredAt);

    /// <summary>Removes a single case from the Caja open queue and returns it to Casos.</summary>
    void UndoCajaQueue(long id);

    /// <summary>Cases waiting to be packed — CajaTransferredAt set, CajaBoxId still null — ordered
    /// by Fecha penúltima carpeta (cases without a date sort last).</summary>
    IReadOnlyList<UrgentRequest> GetCajaQueue();

    /// <summary>Closes the current Caja queue into a new, sequentially-numbered box with a manual
    /// or default code (e.g. A1-F8) and returns it.</summary>
    Box CloseBox(string code, DateTimeOffset closedAt);

    /// <summary>Reopens a closed box: unpacks all its cases back into the open Caja queue and
    /// removes the closed box record.</summary>
    void ReopenBox(long boxId);

    /// <summary>Removes an individual case from a closed box and returns it to Casos.</summary>
    void RemoveCaseFromClosedBox(long id);

    /// <summary>Every closed box, most recently closed first.</summary>
    IReadOnlyList<Box> GetBoxes();

    Box? FindBoxById(long id);

    /// <summary>Cases packed into a given closed box, in the same order they were queued.</summary>
    IReadOnlyList<UrgentRequest> GetCasesByBoxId(long boxId);
}
