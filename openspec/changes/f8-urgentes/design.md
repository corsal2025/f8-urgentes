# Design: F8 Urgent Requests Web App

## Context

Replace `URGENTES DIARIOS.xlsx` (~1,800 real rows across MAYO–JULIO) with a
.NET 10 / ASP.NET Core Razor Pages + SQLite web app that owns the data, validates
it, and preserves history via a one-time ClosedXML import. The stack, layering,
and dashboard visual language mirror the reference project
`outlook-comuna-router` (`src/OutlookComunaRouter/`). NO ORM — raw SQL
repositories over `Microsoft.Data.Sqlite`. This document defines the HOW at the
architectural level; concrete task steps are produced by the `tasks` phase.

The reference project was studied directly. Confirmed patterns to replicate:
- Web SDK csproj (`Microsoft.NET.Sdk.Web`), `net10.0`, `Nullable` + `ImplicitUsings` enabled, `InternalsVisibleTo` to the test project.
- Repository = interface + `sealed` class taking `connectionString` via primary constructor; each method opens its own `SqliteConnection` (`private SqliteConnection Open()`); a public `EnsureSchema()` runs `CREATE TABLE IF NOT EXISTS` idempotently.
- Additive schema evolution via `EnsureColumnExists` (PRAGMA `table_info` guard) — no migration framework.
- DI: repositories registered as singletons in `Program.cs` bound to `Data Source={path}`; `EnsureSchema()` called at startup before `app.Run()`.
- One-time / admin operations expressed as CLI flags on the same web executable, short-circuiting before the host starts (`--add-user`, `--smoke-test` precedent).
- Razor Pages rooted at `/Dashboard/Pages`; pages POST to named handlers (`asp-page-handler`); all UI text in Spanish; single shared `wwwroot/css/dashboard.css` drives the visual language.
- Dates persisted as ISO-8601 TEXT (`DateOnly` → `yyyy-MM-dd`, `DateTimeOffset` → `"O"`), NULL for absent; parsed back in a private `Map(reader)`.
- Domain = plain C# entities + enums + `static` parser/value helpers (e.g. `SpanishDate`); no framework leakage into Domain.
- Tests: xUnit, one repository test class per repository, `IDisposable` with a temp-file SQLite DB per test (`Path.GetTempPath()` + GUID), `EnsureSchema()` in the ctor.

## Goals / Non-Goals

Goals:
- Layered layout (Domain / Data / Import / Pages) in `src/F8Urgentes/` mirroring the reference.
- SQLite schema capturing every Excel column with correct typing + provenance + flags.
- Robust importer: header-name mapping, RUT normalization, "S/C" → typed null, corrupt-cell flagging (never drop), empty-sheet skip, idempotency guard.
- Web management: dashboard list + filters, create/edit request, flagged-review surface.
- Strict-TDD xUnit coverage per layer.

Non-Goals (from proposal): repeatable/scheduled re-import, auth/roles/audit, CONASET integration or automatic F8 generation, editing empty AUG–DEC template sheets, any write-back to Excel.

## Decisions

### D1 — Four-folder layered layout under `src/F8Urgentes/`

```
src/F8Urgentes/
  Domain/
    UrgentRequest.cs          # entity: all Excel columns + provenance + flags
    Rut.cs                    # value object: normalize + check-digit validation
    FolderDate.cs             # parse helper: "S/C"/blank -> null, ISO/dd-mm -> DateOnly
    EstadoCatalog.cs          # known ESTADO / ESTADO ACTUAL constants (open string, see D4)
    ImportFlag.cs             # flag enum/record (reason codes for dubious cells)
  Data/
    IUrgentRequestRepository.cs
    UrgentRequestRepository.cs  # raw SQL, EnsureSchema, EnsureColumnExists, Map
  Import/
    ExcelUrgentImporter.cs    # ClosedXML pipeline: sheet iter, header map, row map, flag accumulation
    HeaderCanonicalizer.cs    # canonical header name -> column resolution
    ImportResult.cs           # summary: rows read/imported/flagged/skipped per sheet
  Dashboard/
    Pages/
      Index.cshtml(.cs)       # dashboard list + filters
      Edit.cshtml(.cs)        # create / edit a request
      Review.cshtml(.cs)      # flagged-review surface
      _ViewImports.cshtml
  Configuration/
    F8Options.cs              # SqliteDbPath, ExcelSourcePath
  Program.cs
  appsettings.json
  wwwroot/css/dashboard.css   # copied/adapted from reference visual language
tests/F8Urgentes.Tests/
  Domain/ Data/ Import/       # mirror source folders
data/                         # generated SQLite file (git-ignored), like reference
```

Rationale: matches the reference's Domain / Persistence / (feature) / Dashboard split. We rename `Persistence` → `Data` and add a dedicated `Import` boundary because the importer is a first-class, one-time capability that must be testable in isolation from both the web host and Excel I/O. `Application`-style services are intentionally omitted: the reference keeps orchestration thin (PageModels call repositories directly); for a single-entity CRUD + import app, an extra service layer would be ceremony without benefit. Reject alternative: separate class-library projects per layer — the reference is a single web project with folder-based layering, and a multi-project split adds build/reference overhead for no isolation gain here.

### D2 — Single `UrgentRequest` table with inline flag columns + a normalized `ImportFlag` child table

Schema bootstrap follows the reference exactly: `EnsureSchema()` on the repository runs `CREATE TABLE IF NOT EXISTS` at startup; later additive columns use the `EnsureColumnExists` PRAGMA guard. No EF/migrations.

```sql
CREATE TABLE IF NOT EXISTS UrgentRequest (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    -- Excel columns (typed) --
    FechaPeticion        TEXT NULL,   -- ISO yyyy-MM-dd or NULL (blank/"S/C"/unparseable+flag)
    Nombres              TEXT NULL,
    Apellidos            TEXT NULL,
    NombreCompleto       TEXT NULL,   -- Excel had a dedicated column; kept verbatim
    Rut                  TEXT NULL,   -- normalized canonical form NNNNNNNN-D (see D3); raw kept in RutRaw
    RutRaw               TEXT NULL,   -- exactly what was in the cell, for audit/flag review
    FechaUltimaCarpeta   TEXT NULL,   -- ISO or NULL ("S/C" => NULL, no flag: S/C is legitimate)
    CodigoF8             TEXT NULL,
    FechaPenultimaCarpeta TEXT NULL,  -- ISO or NULL
    Estado               TEXT NULL,   -- open string, canonicalized (see D4)
    EstadoActual         TEXT NULL,   -- open string, canonicalized (see D4)
    FechaDeSubida        TEXT NULL,   -- ISO or NULL (absent entirely on MAYO sheet)
    -- provenance --
    SourceSheet          TEXT NULL,   -- "MAYO" | "JUNIO" | "JULIO" (import) or NULL (web-created)
    SourceRowNumber      INTEGER NULL,-- 1-based Excel row, for review traceability
    Origin               TEXT NOT NULL, -- "Import" | "Web"
    -- flag rollup --
    NeedsReview          INTEGER NOT NULL DEFAULT 0, -- 1 if any ImportFlag exists for this row
    -- bookkeeping --
    CreatedAt            TEXT NOT NULL,
    UpdatedAt            TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_UrgentRequest_Rut ON UrgentRequest (Rut);
CREATE INDEX IF NOT EXISTS IX_UrgentRequest_NeedsReview ON UrgentRequest (NeedsReview);

CREATE TABLE IF NOT EXISTS ImportFlag (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    RequestId     INTEGER NOT NULL,
    ColumnName    TEXT NOT NULL,   -- e.g. "FechaUltimaCarpeta", "Rut"
    ReasonCode    TEXT NOT NULL,   -- e.g. "DATE_OUT_OF_RANGE", "RUT_CHECK_DIGIT", "UNPARSEABLE"
    RawValue      TEXT NULL,       -- the offending cell content
    CreatedAt     TEXT NOT NULL,
    FOREIGN KEY (RequestId) REFERENCES UrgentRequest (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_ImportFlag_RequestId ON ImportFlag (RequestId);

-- idempotency guard for the one-time import (see D5)
CREATE TABLE IF NOT EXISTS ImportRun (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    SourceFile  TEXT NOT NULL,
    CompletedAt TEXT NOT NULL,
    RowsImported INTEGER NOT NULL,
    RowsFlagged  INTEGER NOT NULL
);
```

Justification of "flag in a child table AND a `NeedsReview` rollup" (not flag-columns-only): the Excel has heterogeneous dubious cases (a bad serial date in H54, RUT check-digit failures, unparseable strings) each needing a *reason* and the *offending value* preserved for the review UI. A fixed set of boolean flag columns cannot carry that structured detail and would need one column per failure kind. The `ImportFlag` child table carries `(column, reasonCode, rawValue)` losslessly; the denormalized `NeedsReview` bit + index keeps the dashboard "requiere revisión" filter a single cheap predicate (same pattern the reference uses for `NeedsReview`). Reject: a single `Flags` TEXT/JSON blob — SQLite can store it but it's not queryable per-reason and breaks the raw-SQL, no-serializer discipline of the reference.

`RutRaw` + `Rut` dual column: we normalize into `Rut` for search/dedup but never destroy the original cell, so a flagged RUT can be shown as-typed in Review.

### D3 — `Rut` value object: normalize + validate check digit

`Rut` is a Domain value object (readonly struct or sealed record) with:
- `static bool TryParse(string? raw, out Rut rut)` — strips dots/spaces, splits body/check-digit on the last `-` (or last char if no dash), upper-cases `k`.
- Check digit computed with the standard Chilean módulo-11 algorithm; `IsValid` compares against the supplied digit.
- Canonical string form: `body-D` without dots (e.g. `15949558-2`), matching the "sin puntos" style seen in the Excel and the reference's copy-without-dots UX.

Import policy: always store the normalized `Rut` when the body is numeric; if the check digit is wrong, still store it but raise an `ImportFlag(column="Rut", reason="RUT_CHECK_DIGIT")` — flag-don't-drop. Blank RUT → NULL, flagged `RUT_MISSING` only if the row otherwise has data. Reject: rejecting invalid RUTs on import (violates the "never silently lose a row" success criterion).

### D4 — `Estado` / `EstadoActual` as canonicalized OPEN STRINGS, not enums

The explore analysis found real drift: ESTADO ∈ {SUBIR CON F8, PRIMERA LICENCIA, CAMBIO DE DOMICILIO, CREAR CERTIFICADO}; ESTADO ACTUAL ∈ {SUBIDA A CONASET, PENDIENTE, CREAR CERTIFICADO, blank}. These are operator-typed and WILL grow. Modeling as a C# enum would force the importer to reject or mis-bucket any unseen value — unacceptable for a faithful historical import.

Decision: store as trimmed/upper-cased free-text strings. Provide `EstadoCatalog` with the *known* values as constants to (a) populate dropdown `<option>`s in the Edit page and (b) let the importer flag genuinely empty/garbage values without rejecting novel-but-plausible ones. The dashboard filter offers the known set plus "otro". This mirrors the reference's pragmatism (it used a closed enum only where the state machine was truly fixed; here it is not). Reject: enum with an `Other` member — loses the actual typed text, which is exactly what a faithful import must preserve.

### D5 — Importer as a CLI flag on the web executable (`--import`), one-time, idempotent

Follow the reference's established precedent: one-off operations are CLI flags on the same web app that short-circuit before `app.Run()` (as `--add-user`/`--smoke-test` do). Invocation: `F8Urgentes.exe --import "path\to\URGENTES DIARIOS.xlsx"` (path optional; falls back to `F8Options.ExcelSourcePath`).

Rationale for CLI-flag over a separate console project: (1) it reuses the exact DI wiring, connection string, and `EnsureSchema()` already built for the web host — a separate project would duplicate configuration and repository references; (2) it matches the reference's proven pattern the user asked us to replicate; (3) the importer classes live in `Import/` and are fully unit-testable independent of the CLI entry point, so the flag is a thin adapter only. Reject: separate `F8Urgentes.Import` console project (config duplication, extra csproj, diverges from reference); reject a hidden web endpoint (import is not a web concern and must not be re-runnable by accident).

Idempotency guard: before importing, the flag handler checks the `ImportRun` table. If a completed run exists for the same `SourceFile`, it refuses and prints a message (override only with an explicit `--import --force` which first `DELETE`s prior `Origin='Import'` rows within a transaction). The whole import runs in a single SQLite transaction; on success it inserts one `ImportRun` row. This makes accidental re-runs a no-op and guarantees "re-running one-time import must not duplicate."

### D6 — Importer pipeline (header-canonicalized, flag-accumulating)

```
ExcelUrgentImporter.Import(workbookPath):
  open workbook (ClosedXML)
  for each worksheet:
     if sheet name not in {MAYO, JUNIO, JULIO}: skip           # empty AUG–DEC templates
     locate header row (first non-empty row)
     build columnMap = HeaderCanonicalizer(headerCells)        # name -> column index
        # canonicalization: upper-case, collapse whitespace, strip accents/punctuation;
        # map synonyms to canonical keys. MAYO lacks FECHA DE SUBIDA and reorders cols —
        # resolved by NAME, never by index.
     for each data row below header:
        if row is entirely empty: skip (padding rows)
        map cells by columnMap into a raw DTO
        normalize each field:
           - Rut.TryParse (D3) -> Rut + RutRaw, flag on check-digit fail
           - FolderDate.Parse for date columns:
                "S/C"/blank -> NULL (no flag; legitimate "sin carpeta")
                Excel serial in valid range -> DateOnly
                out-of-range serial (e.g. MAYO H54 = 29262516) -> NULL + flag DATE_OUT_OF_RANGE, RawValue kept
                unparseable text -> NULL + flag UNPARSEABLE
           - Estado/EstadoActual -> trim/upper (D4), flag only if garbage
        accumulate flags in a per-row list
        insert UrgentRequest (Origin='Import', SourceSheet, SourceRowNumber, NeedsReview = flags.Any())
        insert each ImportFlag with RequestId
     record per-sheet counts in ImportResult
  commit transaction; insert ImportRun; return ImportResult (printed to console)
```

`HeaderCanonicalizer` is pure and unit-tested against both the JUNIO/JULIO layout and the divergent MAYO layout (10 columns, FECHA PETICION last, no FECHA DE SUBIDA). Missing-column handling: if a canonical column is absent (FECHA DE SUBIDA on MAYO), the mapped field is simply NULL — not a flag.

### D7 — Razor Pages surface

Three pages under `/Dashboard/Pages`, reusing `wwwroot/css/dashboard.css` classes (`app-header`, `nav-group`, `card`, `table-card`, `cases`, `badge`, `row-needs-review`) verbatim so the visual language matches the reference:

- **Index (dashboard list + filters)**: table of `UrgentRequest` rows. Filter nav-group: Todos / by ESTADO / by ESTADO ACTUAL / "Requiere revisión" (with a count badge driven by `NeedsReview`). Free-text search by RUT or name (server-side `WHERE` like the reference). Rows flagged `NeedsReview` get the `row-needs-review` class and a ⚠ marker. Inline edit of ESTADO/ESTADO ACTUAL via POST handlers (`SetEstado`, `SetEstadoActual`) mirroring the reference's inline-form-per-cell approach.
- **Edit (create / edit a request)**: full form for all fields; ESTADO/ESTADO ACTUAL as `<select>` populated from `EstadoCatalog` plus a free-text fallback; RUT validated client-visibly on save via `Rut.TryParse` (server-side; invalid RUT blocks web creation — unlike import, web entry can and must be clean). `GET ?id=` edits; no id = create. Web-created rows get `Origin='Web'`, `SourceSheet=NULL`.
- **Review (flagged-review surface)**: lists rows with `NeedsReview=1` joined to their `ImportFlag` rows, showing `ColumnName`, `ReasonCode`, and `RawValue` next to the current value. Each row links to Edit to correct it; saving a corrected value and clearing the flags sets `NeedsReview=0` (repository `ClearFlags(requestId)` deletes the child rows and updates the rollup in one transaction).

PageModels call the repository directly (no service layer, per D1). Repository methods needed (raw SQL, mirroring reference naming): `EnsureSchema`, `Insert`, `Update`, `FindById`, `GetAll`, `Query(filter, search)`, `GetFlagged`, `GetFlagsFor(id)`, `AddFlag`, `ClearFlags`, `HasCompletedImport(sourceFile)`, `RecordImportRun`, `DeleteImportedRows`.

## Data Flow

```
[Excel .xlsx] --(--import CLI, one-time)--> ExcelUrgentImporter --> UrgentRequestRepository --> [SQLite: UrgentRequest + ImportFlag + ImportRun]
                                                     |
                                                     +--> flags for dubious cells (never drops rows)

[Operator browser] <--> Razor PageModels <--> UrgentRequestRepository <--> [SQLite]
   Index  : list/filter/search + inline ESTADO edits
   Edit   : create/edit (RUT validated, web rows clean)
   Review : flagged cells + correct-and-clear
```

Integration points: `Program.cs` wires `F8Options` (from `appsettings.json`: `SqliteDbPath`, `ExcelSourcePath`), registers `IUrgentRequestRepository` singleton, calls `EnsureSchema()` at startup, and branches to the importer when `--import` is present (before host start). ClosedXML is referenced only by the `Import/` folder. `ContentRootPath = AppContext.BaseDirectory` (reference pattern) so relative `data/` and Excel paths resolve regardless of launch method.

## Testing Strategy (strict TDD, per layer)

Red-Green-Refactor per unit; each test written before its implementation.

- **Domain** (`tests/.../Domain/`): `RutTests` — normalization (dots, no-dots, lowercase k), módulo-11 check-digit valid/invalid, blank; `FolderDateTests` — "S/C" → null, ISO parse, Excel-serial in/out of range (incl. the real `29262516`), unparseable; `EstadoCatalogTests` — known-value membership, canonicalization. Pure, no I/O — fastest suite.
- **Data** (`tests/.../Data/`): `UrgentRequestRepositoryTests` — temp-file SQLite per test (`Path.GetTempPath()` + GUID, `IDisposable`), `EnsureSchema()` in ctor (reference pattern). Cover insert/find/update, filter+search queries, flag insert/clear, `NeedsReview` rollup, `HasCompletedImport` idempotency, cascade delete of flags.
- **Import** (`tests/.../Import/`): `HeaderCanonicalizerTests` — JUNIO/JULIO layout, divergent MAYO layout, missing FECHA DE SUBIDA, accent/whitespace variants. `ExcelUrgentImporterTests` — built against small in-repo fixture `.xlsx` files (a few crafted rows incl. an S/C, an out-of-range serial, a bad-check-digit RUT, an empty padding row, and an empty template sheet): assert row counts, that flags are raised with correct `ReasonCode`/`RawValue`, that no row is dropped, and that a second run is a no-op (idempotency).
- **Pages**: PageModel logic kept thin; where a handler carries real branching (e.g. Review's correct-and-clear), test the PageModel against a real temp-file repository. Full HTTP integration is out of scope for strict-TDD unit coverage but the handlers are exercised through their repository calls.

`InternalsVisibleTo("F8Urgentes.Tests")` in the web csproj so internal importer/canonicalizer members are testable without being public API, exactly as the reference does.

## Risks / Trade-offs

- **ClosedXML date-cell ambiguity**: Excel stores dates as serials; a cell typed as text vs. number changes how ClosedXML reads it. Mitigation: `FolderDate` handles both numeric serials and text; out-of-range serials are flagged, not trusted. Validate against the real workbook during apply.
- **Header canonicalization false matches**: overly-aggressive synonym mapping could bind the wrong column. Mitigation: canonicalization is conservative (accent/case/whitespace only + an explicit small synonym table), and the MAYO/JUNIO/JULIO layouts are pinned by fixture tests.
- **No service layer**: PageModels touch the repository directly. Acceptable for single-entity CRUD; if orchestration grows (unlikely per non-goals), a service layer can be introduced without schema change.
- **`Estado` open strings** mean the filter/dropdown can drift from stored data. Mitigation: `EstadoCatalog` is the single source for known values; a "otro" bucket surfaces anything unexpected so it stays visible.
- **Idempotency depends on `ImportRun` matching `SourceFile` string**: a renamed/moved workbook would not be recognized as already-imported. Mitigation: match is intentionally on the logical one-time nature; `--force` exists for deliberate re-runs and transactionally clears prior `Origin='Import'` rows.

## Migration / Rollback

Greenfield, no production data. Rollback = delete the scaffold and the generated SQLite file under `data/`. The source `.xlsx` is opened read-only by ClosedXML and never modified. Re-running `--import` without `--force` is a safe no-op.
