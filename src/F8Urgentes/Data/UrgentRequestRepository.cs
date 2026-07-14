using Microsoft.Data.Sqlite;
using F8Urgentes.Domain;

namespace F8Urgentes.Data;

/// <summary>
/// Raw-SQL repository over Microsoft.Data.Sqlite, mirroring the reference project's
/// pattern: sealed class + primary-constructor connection string, each method opens
/// its own connection, EnsureSchema runs idempotent CREATE TABLE IF NOT EXISTS.
/// </summary>
public sealed class UrgentRequestRepository(string connectionString) : IUrgentRequestRepository
{
    public void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS UrgentRequest (
                Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                FechaPeticion         TEXT NULL,
                Nombres               TEXT NULL,
                Apellidos             TEXT NULL,
                NombreCompleto        TEXT NULL,
                Rut                   TEXT NULL,
                RutRaw                TEXT NULL,
                FechaUltimaCarpeta    TEXT NULL,
                CodigoF8              TEXT NULL,
                FechaPenultimaCarpeta TEXT NULL,
                Estado                TEXT NULL,
                EstadoActual          TEXT NULL,
                FechaDeSubida         TEXT NULL,
                SourceSheet           TEXT NULL,
                SourceRowNumber       INTEGER NULL,
                Origin                TEXT NOT NULL,
                NeedsReview           INTEGER NOT NULL DEFAULT 0,
                CreatedAt             TEXT NOT NULL,
                UpdatedAt             TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_UrgentRequest_Rut ON UrgentRequest (Rut);
            CREATE INDEX IF NOT EXISTS IX_UrgentRequest_NeedsReview ON UrgentRequest (NeedsReview);

            CREATE TABLE IF NOT EXISTS ImportFlag (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                RequestId     INTEGER NOT NULL,
                ColumnName    TEXT NOT NULL,
                ReasonCode    TEXT NOT NULL,
                RawValue      TEXT NULL,
                CreatedAt     TEXT NOT NULL,
                FOREIGN KEY (RequestId) REFERENCES UrgentRequest (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_ImportFlag_RequestId ON ImportFlag (RequestId);

            CREATE TABLE IF NOT EXISTS ImportRun (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceFile   TEXT NOT NULL,
                CompletedAt  TEXT NOT NULL,
                RowsImported INTEGER NOT NULL,
                RowsFlagged  INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public long Insert(UrgentRequest request)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO UrgentRequest
                (FechaPeticion, Nombres, Apellidos, NombreCompleto, Rut, RutRaw, FechaUltimaCarpeta, CodigoF8,
                 FechaPenultimaCarpeta, Estado, EstadoActual, FechaDeSubida, SourceSheet, SourceRowNumber, Origin,
                 NeedsReview, CreatedAt, UpdatedAt)
            VALUES
                ($fechaPeticion, $nombres, $apellidos, $nombreCompleto, $rut, $rutRaw, $fechaUltimaCarpeta, $codigoF8,
                 $fechaPenultimaCarpeta, $estado, $estadoActual, $fechaDeSubida, $sourceSheet, $sourceRowNumber, $origin,
                 $needsReview, $createdAt, $updatedAt);
            SELECT last_insert_rowid();
            """;
        BindParameters(command, request);
        return (long)command.ExecuteScalar()!;
    }

    public UrgentRequest? FindById(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM UrgentRequest WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public void Update(UrgentRequest request)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE UrgentRequest
            SET FechaPeticion = $fechaPeticion, Nombres = $nombres, Apellidos = $apellidos,
                NombreCompleto = $nombreCompleto, Rut = $rut, RutRaw = $rutRaw,
                FechaUltimaCarpeta = $fechaUltimaCarpeta, CodigoF8 = $codigoF8,
                FechaPenultimaCarpeta = $fechaPenultimaCarpeta, Estado = $estado, EstadoActual = $estadoActual,
                FechaDeSubida = $fechaDeSubida, SourceSheet = $sourceSheet, SourceRowNumber = $sourceRowNumber,
                Origin = $origin, NeedsReview = $needsReview, UpdatedAt = $now
            WHERE Id = $id
            """;
        BindParameters(command, request);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", request.Id);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UrgentRequest> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM UrgentRequest ORDER BY Id";
        using var reader = command.ExecuteReader();
        var results = new List<UrgentRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public IReadOnlyList<UrgentRequest> Query(UrgentRequestFilter filter, string? search)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        var where = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Month))
        {
            where.Add("substr(FechaPeticion, 1, 7) = $month");
            command.Parameters.AddWithValue("$month", filter.Month);
        }

        if (!string.IsNullOrWhiteSpace(filter.Estado))
        {
            where.Add("Estado = $estado");
            command.Parameters.AddWithValue("$estado", filter.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filter.EstadoActual))
        {
            where.Add("EstadoActual = $estadoActual");
            command.Parameters.AddWithValue("$estadoActual", filter.EstadoActual);
        }

        if (filter.Flagged == true)
        {
            where.Add("NeedsReview = 1");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(Rut = $searchRut OR NombreCompleto LIKE $searchName OR Nombres LIKE $searchName OR Apellidos LIKE $searchName)");
            var normalizedRut = Rut.TryParse(search, out var rut) ? rut.ToString() : "\0no-match\0";
            command.Parameters.AddWithValue("$searchRut", normalizedRut);
            command.Parameters.AddWithValue("$searchName", $"%{search}%");
        }

        command.CommandText = "SELECT * FROM UrgentRequest" +
            (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty) +
            " ORDER BY Id";

        using var reader = command.ExecuteReader();
        var results = new List<UrgentRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public void AddFlag(long requestId, string columnName, string reasonCode, string? rawValue)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO ImportFlag (RequestId, ColumnName, ReasonCode, RawValue, CreatedAt)
                VALUES ($requestId, $columnName, $reasonCode, $rawValue, $createdAt)
                """;
            insert.Parameters.AddWithValue("$requestId", requestId);
            insert.Parameters.AddWithValue("$columnName", columnName);
            insert.Parameters.AddWithValue("$reasonCode", reasonCode);
            insert.Parameters.AddWithValue("$rawValue", (object?)rawValue ?? DBNull.Value);
            insert.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE UrgentRequest SET NeedsReview = 1 WHERE Id = $id";
            update.Parameters.AddWithValue("$id", requestId);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<ImportFlag> GetFlagsFor(long requestId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ImportFlag WHERE RequestId = $requestId ORDER BY Id";
        command.Parameters.AddWithValue("$requestId", requestId);
        using var reader = command.ExecuteReader();
        var results = new List<ImportFlag>();
        while (reader.Read())
        {
            results.Add(MapFlag(reader));
        }
        return results;
    }

    public IReadOnlyList<UrgentRequest> GetFlagged()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM UrgentRequest WHERE NeedsReview = 1 ORDER BY Id";
        using var reader = command.ExecuteReader();
        var results = new List<UrgentRequest>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public void ClearFlags(long requestId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM ImportFlag WHERE RequestId = $id";
            delete.Parameters.AddWithValue("$id", requestId);
            delete.ExecuteNonQuery();
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE UrgentRequest SET NeedsReview = 0 WHERE Id = $id";
            update.Parameters.AddWithValue("$id", requestId);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public bool HasCompletedImport(string sourceFile)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM ImportRun WHERE SourceFile = $sourceFile LIMIT 1";
        command.Parameters.AddWithValue("$sourceFile", sourceFile);
        return command.ExecuteScalar() is not null;
    }

    public void RecordImportRun(string sourceFile, int rowsImported, int rowsFlagged)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ImportRun (SourceFile, CompletedAt, RowsImported, RowsFlagged)
            VALUES ($sourceFile, $completedAt, $rowsImported, $rowsFlagged)
            """;
        command.Parameters.AddWithValue("$sourceFile", sourceFile);
        command.Parameters.AddWithValue("$completedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$rowsImported", rowsImported);
        command.Parameters.AddWithValue("$rowsFlagged", rowsFlagged);
        command.ExecuteNonQuery();
    }

    public void DeleteImportedRows()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM UrgentRequest WHERE Origin = 'Import'";
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON";
            pragma.ExecuteNonQuery();
        }
        return connection;
    }

    private static void BindParameters(SqliteCommand command, UrgentRequest request)
    {
        command.Parameters.AddWithValue("$fechaPeticion", (object?)request.FechaPeticion?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$nombres", (object?)request.Nombres ?? DBNull.Value);
        command.Parameters.AddWithValue("$apellidos", (object?)request.Apellidos ?? DBNull.Value);
        command.Parameters.AddWithValue("$nombreCompleto", (object?)request.NombreCompleto ?? DBNull.Value);
        command.Parameters.AddWithValue("$rut", (object?)request.Rut ?? DBNull.Value);
        command.Parameters.AddWithValue("$rutRaw", (object?)request.RutRaw ?? DBNull.Value);
        command.Parameters.AddWithValue("$fechaUltimaCarpeta", (object?)request.FechaUltimaCarpeta?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$codigoF8", (object?)request.CodigoF8 ?? DBNull.Value);
        command.Parameters.AddWithValue("$fechaPenultimaCarpeta", (object?)request.FechaPenultimaCarpeta?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$estado", (object?)request.Estado ?? DBNull.Value);
        command.Parameters.AddWithValue("$estadoActual", (object?)request.EstadoActual ?? DBNull.Value);
        command.Parameters.AddWithValue("$fechaDeSubida", (object?)request.FechaDeSubida?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceSheet", (object?)request.SourceSheet ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceRowNumber", (object?)request.SourceRowNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$origin", request.Origin);
        command.Parameters.AddWithValue("$needsReview", request.NeedsReview ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", request.CreatedAt == default ? DateTimeOffset.UtcNow.ToString("O") : request.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", (object?)request.UpdatedAt?.ToString("O") ?? DBNull.Value);
    }

    private static UrgentRequest Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("Id")),
        FechaPeticion = ReadDateOnly(reader, "FechaPeticion"),
        Nombres = ReadString(reader, "Nombres"),
        Apellidos = ReadString(reader, "Apellidos"),
        NombreCompleto = ReadString(reader, "NombreCompleto"),
        Rut = ReadString(reader, "Rut"),
        RutRaw = ReadString(reader, "RutRaw"),
        FechaUltimaCarpeta = ReadDateOnly(reader, "FechaUltimaCarpeta"),
        CodigoF8 = ReadString(reader, "CodigoF8"),
        FechaPenultimaCarpeta = ReadDateOnly(reader, "FechaPenultimaCarpeta"),
        Estado = ReadString(reader, "Estado"),
        EstadoActual = ReadString(reader, "EstadoActual"),
        FechaDeSubida = ReadDateOnly(reader, "FechaDeSubida"),
        SourceSheet = ReadString(reader, "SourceSheet"),
        SourceRowNumber = reader.IsDBNull(reader.GetOrdinal("SourceRowNumber")) ? null : reader.GetInt32(reader.GetOrdinal("SourceRowNumber")),
        Origin = reader.GetString(reader.GetOrdinal("Origin")),
        NeedsReview = reader.GetInt32(reader.GetOrdinal("NeedsReview")) == 1,
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
    };

    private static ImportFlag MapFlag(SqliteDataReader reader) => new(
        reader.GetInt64(reader.GetOrdinal("Id")),
        reader.GetInt64(reader.GetOrdinal("RequestId")),
        reader.GetString(reader.GetOrdinal("ColumnName")),
        reader.GetString(reader.GetOrdinal("ReasonCode")),
        ReadString(reader, "RawValue"),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))));

    private static string? ReadString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateOnly? ReadDateOnly(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : DateOnly.Parse(reader.GetString(ordinal));
    }
}
