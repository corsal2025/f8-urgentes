# Tasks: F8 Urgent Requests Web App

Strict TDD applies to every code task below: write the failing test first, run
`dotnet test`, then implement the minimum to go green, then refactor. "Test
first" is called out explicitly per task; scaffold/config tasks have no test
of their own but must not break the suite.

Legend: **[P]** = can run in parallel with sibling tasks in the same group
(no file overlap, dependencies already satisfied). Unmarked = sequential,
depends on the immediately preceding task(s) unless stated otherwise.

---

## Group 0 — Solution Scaffold (sequential, blocks everything)

### T0.1 [x] — Create solution and project skeleton
- Run `dotnet new sln -n F8Urgentes` at repo root (if not already present).
- `dotnet new web -n F8Urgentes -o src/F8Urgentes -f net10.0` (Razor Pages
  enabled: add `AddRazorPages()` / `MapRazorPages()` in `Program.cs`).
- `dotnet new xunit -n F8Urgentes.Tests -o tests/F8Urgentes.Tests -f net10.0`.
- Add both projects to the `.sln`; add project reference
  `tests/F8Urgentes.Tests` → `src/F8Urgentes`.
- Add package references to `src/F8Urgentes/F8Urgentes.csproj`:
  `Microsoft.Data.Sqlite` 10.0.9, `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3,
  `ClosedXML` (latest MIT-compatible stable).
- Add package references to `tests/F8Urgentes.Tests/F8Urgentes.Tests.csproj`:
  `xunit` 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1, `coverlet.collector` 6.0.4
  (replace default template versions if the `dotnet new xunit` template pins
  different ones).
- Add `<ItemGroup><InternalsVisibleTo Include="F8Urgentes.Tests" /></ItemGroup>`
  (via `AssemblyInfo` attribute or csproj `InternalsVisibleTo` item) to
  `F8Urgentes.csproj`.
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
  in both csproj files.
- Create empty folders: `src/F8Urgentes/Domain/`, `Data/`, `Import/`,
  `Dashboard/Pages/`, `Configuration/`; mirrored `tests/F8Urgentes.Tests/Domain/`,
  `Data/`, `Import/`.
- Create `data/` folder at repo root with a `.gitignore` entry for the
  generated SQLite file.
- Verify: `dotnet build` succeeds; `dotnet test` runs (0 tests, green).
- Satisfies: design D1 (layout), proposal Dependencies section.

---

## Group 1 — Domain Layer (test-first, no I/O)

Depends on: T0.1. Tasks in this group are **[P]** with each other (separate
files, no shared state) but each individually is test-first (red before
green).

### T1.1 [x] — `Rut` value object [P]
- Test first: `tests/.../Domain/RutTests.cs` — cases: dots stripped,
  no-dots input, lowercase `k` → uppercase `K`, valid módulo-11 check digit,
  invalid check digit (`IsValid=false` but parse still succeeds), blank/null
  input → `TryParse` returns false, canonical string form `NNNNNNNN-D`.
- Implement `src/F8Urgentes/Domain/Rut.cs`: `TryParse`, `IsValid`,
  canonical `ToString()`, módulo-11 algorithm.
- Satisfies: spec urgent-request-management "RUT Normalization"; design D3.

### T1.2 [x] — `FolderDate` parse helper [P]
- Test first: `tests/.../Domain/FolderDateTests.cs` — `"S/C"` → null (no
  flag signal needed at this layer, just null), blank → null, ISO string →
  `DateOnly`, valid Excel serial → `DateOnly`, out-of-range serial
  (`29262516`, the real MAYO H54 value) → null + "out of range" result,
  unparseable text → null + "unparseable" result.
- Implement `src/F8Urgentes/Domain/FolderDate.cs`: static `Parse`/`TryParse`
  returning a small result type (value + outcome enum: `Ok`, `SinCarpeta`,
  `OutOfRange`, `Unparseable`) so the importer (T3.x) can decide flagging.
- Satisfies: spec historical-import "S/C Date Handling", "Corrupt Cell
  Flagging"; design D6.

### T1.3 [x] — `EstadoCatalog` [P]
- Test first: `tests/.../Domain/EstadoCatalogTests.cs` — known ESTADO set
  membership (`SUBIR CON F8`, `PRIMERA LICENCIA`, `CAMBIO DE DOMICILIO`,
  `CREAR CERTIFICADO`, `CARPETA SUBIDA`), known ESTADO ACTUAL set membership
  (`SUBIDA A CONASET`, `PENDIENTE`, `CREAR CERTIFICADO`, empty),
  canonicalization (trim/upper-case) of a known value in mixed case, and
  `IsKnown` returning false for `"OTRO VALOR"`.
- Implement `src/F8Urgentes/Domain/EstadoCatalog.cs`: constants +
  `IsKnownEstado(string)` / `IsKnownEstadoActual(string)` + `Canonicalize`.
- Satisfies: spec urgent-request-management "Known Status Value Sets";
  design D4.

### T1.4 [x] — `UrgentRequest` entity and `ImportFlag` record
- No independent test (plain data holder); covered indirectly by T1.1–T1.3
  and Data-layer tests. Depends on T1.1–T1.3 for field types.
- Implement `src/F8Urgentes/Domain/UrgentRequest.cs` (all Excel columns +
  provenance + `NeedsReview`, per design D2 schema) and
  `src/F8Urgentes/Domain/ImportFlag.cs` (`ColumnName`, `ReasonCode`,
  `RawValue`).
- Satisfies: design D2.

---

## Group 2 — Data Layer (test-first, temp-file SQLite)

Depends on: T1.4 (entity shapes must exist first). Sequential within the
group because all tests exercise the same `UrgentRequestRepository` class
file.

### T2.1 [x] — `EnsureSchema` + repository skeleton
- Test first: `tests/.../Data/UrgentRequestRepositoryTests.cs` — ctor test:
  construct repository against a temp-file SQLite DB
  (`Path.GetTempPath()` + GUID), call `EnsureSchema()`, assert
  `UrgentRequest`, `ImportFlag`, `ImportRun` tables exist (query
  `sqlite_master`). Class implements `IDisposable` deleting the temp file.
- Implement `src/F8Urgentes/Data/IUrgentRequestRepository.cs` (interface per
  design D7 method list) and `src/F8Urgentes/Data/UrgentRequestRepository.cs`
  with `EnsureSchema()` running the exact `CREATE TABLE IF NOT EXISTS` DDL
  from design D2, plus indexes.
- Satisfies: design D2, D7 (repository contract).

### T2.2 [x] — Insert / FindById / Update / GetAll
- Test first: add cases to `UrgentRequestRepositoryTests` — insert returns
  generated Id; `FindById` round-trips all fields including nullable dates;
  `Update` persists a field change and updates `UpdatedAt`; `GetAll` returns
  inserted rows.
- Implement `Insert`, `FindById`, `Update`, `GetAll`, and the private
  `Map(reader)` helper (ISO-8601 TEXT parsing per design's date convention).
- Satisfies: spec urgent-request-management "Request Creation", "Request
  Editing".

### T2.3 [x] — `Query(filter, search)` — month / ESTADO / ESTADO ACTUAL / search
- Test first: cases — filter by month of `FechaPeticion`, filter by ESTADO,
  filter by ESTADO ACTUAL, search by normalized RUT (with dots in the search
  input), search by partial name, combined filter (month + ESTADO
  simultaneously).
- Implement `Query` with a raw SQL `WHERE` builder (parameterized, no string
  concatenation of values) covering the above predicates.
- Satisfies: spec dashboard "Filtering by Month", "Filtering by ESTADO",
  "Search by RUT or Name", "Combined Filters".

### T2.4 [x] — Flag operations: `AddFlag`, `GetFlagsFor`, `GetFlagged`, `ClearFlags`
- Test first: cases — `AddFlag` inserts a child row and sets parent
  `NeedsReview=1`; `GetFlagsFor(id)` returns flags for a request;
  `GetFlagged()` returns only `NeedsReview=1` rows; `ClearFlags(id)` deletes
  child rows and resets `NeedsReview=0` in one transaction; cascade delete
  behavior when a request row's flags are queried after clearing.
- Implement the four methods per design D7.
- Satisfies: spec dashboard "Flagged Row Review Surface"; design D2
  (NeedsReview rollup).

### T2.5 [x] — Import bookkeeping: `HasCompletedImport`, `RecordImportRun`, `DeleteImportedRows`
- Test first: cases — `HasCompletedImport(sourceFile)` false before any run,
  true after `RecordImportRun` for the same `SourceFile`, false for a
  different `SourceFile`; `DeleteImportedRows()` removes only
  `Origin='Import'` rows and leaves `Origin='Web'` rows untouched.
- Implement the three methods per design D5.
- Satisfies: design D5 (idempotency guard).

---

## Group 3 — Import Layer (test-first, fixture-driven)

Depends on: Group 2 (repository is the import's write target). T3.1 and
T3.2 are **[P]** (different files); T3.3 depends on both.

### T3.1 [x] — `HeaderCanonicalizer` [P]
- Test first: `tests/.../Import/HeaderCanonicalizerTests.cs` — canonicalizes
  case/whitespace/accent variants of a header to the same key; maps the
  JUNIO/JULIO 11-column header row to all canonical fields including `FECHA
  DE SUBIDA`; maps the MAYO 10-column header row (reordered, `FECHA
  PETICION` last, no `FECHA DE SUBIDA`) to the correct fields with `FECHA DE
  SUBIDA` absent from the map (not present as a key, not an error).
- Implement `src/F8Urgentes/Import/HeaderCanonicalizer.cs`: pure function,
  no ClosedXML dependency in its public surface if feasible (accepts a
  `string[]` of header cell texts), returns `IReadOnlyDictionary<string,int>`
  canonical-name → column index.
- Satisfies: spec historical-import "Header-Name Column Mapping" (both
  scenarios); design D6.

### T3.2 [x] — `ImportResult` [P]
- No independent test (plain summary DTO). Implement
  `src/F8Urgentes/Import/ImportResult.cs`: per-sheet rows read / imported /
  flagged / skipped counts + totals.
- Satisfies: spec historical-import "Import Completeness" (reporting
  vehicle).

### T3.3 [x] — Pinned fixture tests: MAYO / JUNIO / JULIO header + row layouts
- Test first, before the importer pipeline exists: build small in-repo
  `.xlsx` fixtures under `tests/F8Urgentes.Tests/Fixtures/` —
  `mayo-sample.xlsx` (10 columns, reordered, `FECHA PETICION` last, no
  `FECHA DE SUBIDA`, including the real out-of-range serial `29262516` in a
  date cell), `junio-sample.xlsx` and `julio-sample.xlsx` (11 columns,
  standard order), each with a handful of rows covering: a clean row, an
  `S/C` date, a bad-RUT-check-digit row, an unrecognized ESTADO value, and
  one fully-empty padding row. Include an `agosto-empty-sample.xlsx` with
  only a header row (or no data rows) to pin the empty-sheet-skip case.
- Write `ExcelUrgentImporterTests` asserting (against these fixtures, once
  T3.4 exists) exact row counts and column-to-field assignments per sheet —
  this task creates the fixtures and the test skeletons/assertions; T3.4
  makes them pass.
- Satisfies: spec historical-import "Sheet Selection", "Header-Name Column
  Mapping" (MAYO scenario explicitly required by user), design D6 pinned
  layouts note.

### T3.4 [x] — `ExcelUrgentImporter` pipeline
- Test first: extend `ExcelUrgentImporterTests` (from T3.3) plus new cases —
  RUT normalization + flag-on-mismatch (not drop), `S/C` → null no flag,
  out-of-range serial → null + `DATE_OUT_OF_RANGE` flag with `RawValue`
  preserved, unparseable text → null + `UNPARSEABLE` flag, unrecognized
  ESTADO/ESTADO ACTUAL → stored verbatim + flagged, fully-empty row skipped
  (no record, no flag), empty template sheet (AGOSTO) skipped entirely,
  running the importer twice with the same `SourceFile` is a no-op the
  second time (uses `HasCompletedImport`), `--force` semantics (via a
  boolean parameter) delete-and-reimport.
- Implement `src/F8Urgentes/Import/ExcelUrgentImporter.cs`: full pipeline
  per design D6 — open workbook, iterate sheets in `{MAYO, JUNIO, JULIO}`,
  header row via `HeaderCanonicalizer`, per-row mapping using `Rut.TryParse`,
  `FolderDate.Parse`, `EstadoCatalog.Canonicalize`/`IsKnown*`, flag
  accumulation, single transaction, insert `ImportRun`, return
  `ImportResult`.
- Satisfies: spec historical-import — all requirements (Sheet Selection,
  Header-Name Column Mapping, RUT Normalization and Flagging, "S/C" Date
  Handling, Corrupt Cell Flagging, Unknown Status Value Handling, Empty Row
  Skipping, Import Completeness).

---

## Group 4 — CLI Import Flag + DI Wiring (sequential)

Depends on: Group 3.

### T4.1 [x] — `F8Options` + `Program.cs` DI wiring
- No new unit test (composition root); covered by manual smoke verification
  in T6.1. Implement `src/F8Urgentes/Configuration/F8Options.cs`
  (`SqliteDbPath`, `ExcelSourcePath`) bound from `appsettings.json`;
  `Program.cs` registers `IUrgentRequestRepository` as a singleton bound to
  `Data Source={SqliteDbPath}`, calls `EnsureSchema()` before `app.Run()`,
  sets `ContentRootPath = AppContext.BaseDirectory`.
- Add `appsettings.json` with default `SqliteDbPath` (`data/f8urgentes.db`)
  and `ExcelSourcePath`.
- Satisfies: design D5, D7 integration points; proposal Approach (SQLite via
  Microsoft.Data.Sqlite, no ORM).

### T4.2 [x] — `--import` CLI flag handler
- Test first where feasible: a small unit test on the *argument-parsing*
  function in isolation (e.g. `TryParseImportArgs(string[] args)` →
  `(path, force)` tuple or null) under `tests/.../ImportCliArgsTests.cs`.
  The end-to-end CLI invocation itself is exercised in T6.1 (real run), not
  as an xUnit test, since it starts a process.
- Implement the flag branch in `Program.cs`: detect `--import [path]`
  (optional path arg, falling back to `F8Options.ExcelSourcePath`) and
  `--force`, short-circuit before `app.Run()`, call
  `ExcelUrgentImporter.Import(...)`, print `ImportResult` summary to
  console, exit.
- Satisfies: spec historical-import (entry point for all import
  requirements); design D5.

---

## Group 5 — Razor Pages / Dashboard (test-first for PageModel logic)

Depends on: Group 2 (repository), T4.1 (DI). T5.1/T5.2/T5.3 PageModels are
**[P]** with each other; the CSS task (T5.4) is **[P]** with all of them.

### T5.1 [x] — Index page (list + filters + inline ESTADO edits) [P]
- Test first: `tests/.../Pages/IndexPageModelTests.cs` (or under a
  `Dashboard` test folder mirroring source) — against a real temp-file
  repository: `OnGet` with no filters returns all requests; with month
  filter returns only matching; with ESTADO filter returns only matching;
  with `flagged=true` returns only `NeedsReview` rows; with `search`
  returns RUT/name matches; `OnPostSetEstado` / `OnPostSetEstadoActual`
  handlers persist the change and redirect/re-render.
- Implement `src/F8Urgentes/Dashboard/Pages/Index.cshtml` +
  `Index.cshtml.cs`: table view using `wwwroot/css/dashboard.css` classes
  (`app-header`, `nav-group`, `card`, `table-card`, `cases`, `badge`,
  `row-needs-review`), nav-group filters (Todos / ESTADO / ESTADO ACTUAL /
  Requiere revisión with count badge), free-text search box, inline
  per-cell POST handlers for ESTADO/ESTADO ACTUAL.
- Satisfies: spec dashboard — Request Listing, Filtering by Month, Filtering
  by ESTADO, Search by RUT or Name, Combined Filters; spec
  urgent-request-management "State Transitions".

### T5.2 [x] — Edit page (create / edit) [P]
- Test first: `tests/.../Pages/EditPageModelTests.cs` — `OnGet` with no id
  produces an empty/create form model; `OnGet ?id=` loads an existing
  request; `OnPost` with a valid RUT and required fields inserts a new
  request (`Origin='Web'`, `SourceSheet=null`) and no `ImportFlag`s;
  `OnPost` with an invalid RUT check digit rejects the submission (no
  record persisted, validation error surfaced); `OnPost` with `?id=` for an
  existing request updates fields and preserves `Id`/`CreatedAt`; setting
  ESTADO ACTUAL to `SUBIDA A CONASET` with an upload date persists `FECHA DE
  SUBIDA`.
- Implement `src/F8Urgentes/Dashboard/Pages/Edit.cshtml` + `.cshtml.cs`:
  full field form, ESTADO/ESTADO ACTUAL `<select>` from `EstadoCatalog` plus
  free-text fallback, server-side `Rut.TryParse`+`IsValid` gate on save.
- Satisfies: spec urgent-request-management — Request Creation (both
  scenarios), RUT Normalization, State Transitions (both scenarios), Known
  Status Value Sets (unrecognized-value scenario via free-text fallback),
  Request Editing.

### T5.3 [x] — Review page (flagged-review surface) [P]
- Test first: `tests/.../Pages/ReviewPageModelTests.cs` — `OnGet` lists only
  `NeedsReview=1` requests joined with their `ImportFlag` rows
  (`ColumnName`, `ReasonCode`, `RawValue` visible); after a correcting
  `Edit` save that calls `ClearFlags`, the row no longer appears in Review
  results.
- Implement `src/F8Urgentes/Dashboard/Pages/Review.cshtml` + `.cshtml.cs`:
  lists flagged rows + flag detail, links to Edit.
- Satisfies: spec dashboard "Flagged Row Review Surface".

### T5.4 [x] — `dashboard.css` + shared layout [P]
- No test (static asset). Copy/adapt `wwwroot/css/dashboard.css` from
  `src/OutlookComunaRouter/` (or wherever the reference project's stylesheet
  lives), preserving class names used above (`app-header`, `nav-group`,
  `card`, `table-card`, `cases`, `badge`, `row-needs-review`). Adapt content
  (branding/text) to F8 Urgentes; add `_ViewImports.cshtml` /
  `_ViewStart.cshtml` / a shared `_Layout.cshtml` referencing the CSS.
- Satisfies: spec dashboard (visual replica requirement in Purpose); design
  D1 (`wwwroot/css/dashboard.css`), D7 (class names).

---

## Group 6 — Real Data Validation (sequential, final)

Depends on: Group 4 (importer CLI) and Group 5 (Review page, to eyeball
flags). Must run after all other code is green.

### T6.1 [x] — Real import run against `URGENTES DIARIOS.xlsx`
- Not a unit test — an operational verification step, run manually /
  scripted against the real workbook (outside `dotnet test`).
- Run `dotnet run --project src/F8Urgentes -- --import "<path to URGENTES
  DIARIOS.xlsx>"`.
- Verify: console `ImportResult` summary reports MAYO+JUNIO+JULIO row counts
  summing to the real non-empty row count (~1,800 per proposal); clean +
  flagged imported counts sum to that total (no silent drops, per spec
  historical-import "Import Completeness"); spot-check the known MAYO H54
  out-of-range-serial row appears in the Review page with
  `DATE_OUT_OF_RANGE`; re-running the same command without `--force`
  refuses/no-ops; re-running with `--force` re-imports without duplicating
  `Origin='Web'` rows.
- Record the actual counts observed in this task's notes (update this file
  or the apply-progress artifact) for verify-phase reference.
- Satisfies: proposal Success Criteria ("All ~1,800 MAYO–JULIO rows
  imported; dubious cells flagged, none silently lost"); spec
  historical-import "Import Completeness".

**Actual results (recorded during apply, real workbook at repo root)**:
```
MAYO:  read=441 imported=441 flagged=2 skipped=0
JUNIO: read=423 imported=110 flagged=1 skipped=313
JULIO: read=423 imported=82  flagged=3 skipped=341
TOTAL: read=1287 imported=633 flagged=6 skipped=654
```
Cross-checked against the raw OOXML (`sheetData` row count with any non-blank
cell, independent of the importer): MAYO 441 data rows, JUNIO 110, JULIO 82 —
exact match to `imported` above. **The proposal's "~1,800" estimate does not
match the real workbook**; the actual non-empty MAYO–JULIO row count is 633,
not ~1,800. `skipped=654` rows are genuinely blank padding rows inside each
sheet's used range (the workbook's used range extends well past the last
data row, likely from conditional formatting/filter ranges) — confirmed via
independent inspection, not an importer bug. Recommend updating the proposal's
Success Criteria to "633" (or "the real non-empty row count") before/at
verify time.

Spot-check: the real MAYO H54 corrupt cell (row 54, "DAMARIS CONSUELO PAVEZ
VILCHES") appears in Review with `FechaPenultimaCarpeta=UNPARSEABLE,
raw=CORRUPT_CELL_VALUE` — **not** `DATE_OUT_OF_RANGE` as the task anticipated.
Reason: ClosedXML's cell was typed `DateTime` with an underlying OLE
Automation value out of .NET's representable range; every ClosedXML string/
numeric accessor for that cell (`GetDateTime`, `GetDouble`, `GetString`,
`CachedValue.ToString()`) throws `ArgumentException`/`InvalidCastException`
internally (`DateTime.FromOADate`) before the raw serial digits are ever
reachable — there is no accessor that recovers the actual raw value. The
importer catches this and substitutes a `CORRUPT_CELL_VALUE` sentinel so
`FolderDate.Parse` classifies it `Unparseable` (not `OutOfRange`) — the row is
still imported and flagged, never dropped, satisfying the "flag-don't-drop"
requirement, but with a different (accurate, not lost) reason code than the
task predicted from `FolderDateTests`'s synthetic 29262516-string case (which
*is* recoverable as a plain string and correctly yields `DATE_OUT_OF_RANGE`).

Idempotency verified: re-running `--import` without `--force` printed "Import
skipped: this source file was already imported. Use --force to re-import."
and made no DB changes. Re-running with `--force` reproduced identical counts
(633 imported) with no duplication (repository's `DeleteImportedRows` clears
prior `Origin='Import'` rows before re-inserting; no `Origin='Web'` rows
existed during this test).

Also discovered and fixed during this run:
- Real workbook sheet name is `"JUNIO "` (trailing space) — importer sheet
  matching is now trim+case-insensitive.
- Date cells are ClosedXML-typed (`DateTime`/`Number`), not plain text —
  `GetString()` alone returns a locale-formatted display string that
  `FolderDate` can't parse, which was flagging ~100% of MAYO rows before the
  fix. `ExcelUrgentImporter` now reads typed date/number cells explicitly.
- `Microsoft.Data.Sqlite` resolves relative connection-string paths against
  `Environment.CurrentDirectory`, not `AppContext.BaseDirectory`/
  `ContentRootPath` — a bare relative `SqliteDbPath` was landing inside
  `src/F8Urgentes/Data/` (case-insensitively colliding with the Data/ source
  folder) when launched via `dotnet run` from the project directory. Fixed by
  resolving to an absolute path against `AppContext.BaseDirectory` in
  `Program.cs`.

---

## Dependency Graph Summary

```
T0.1
 └─> T1.1 [P] T1.2 [P] T1.3 [P]
      └─> T1.4
           └─> T2.1 -> T2.2 -> T2.3 -> T2.4 -> T2.5
                └─> T3.1 [P] T3.2 [P]
                     └─> T3.3 -> T3.4
                          └─> T4.1 -> T4.2
                               └─> T5.1 [P] T5.2 [P] T5.3 [P] T5.4 [P]
                                    └─> T6.1
```

## Requirement Coverage Map

| Spec Requirement | Task(s) |
|---|---|
| Request Creation | T2.2, T5.2 |
| RUT Normalization | T1.1, T2.2, T5.2 |
| State Transitions | T2.2, T2.3, T5.1, T5.2 |
| Known Status Value Sets | T1.3, T5.2 |
| Request Editing | T2.2, T5.2 |
| Sheet Selection | T3.4, T3.3 |
| Header-Name Column Mapping | T3.1, T3.3, T3.4 |
| RUT Normalization and Flagging (import) | T3.4 |
| "S/C" Date Handling | T1.2, T3.4 |
| Corrupt Cell Flagging | T1.2, T3.4 |
| Unknown Status Value Handling | T3.4 |
| Empty Row Skipping | T3.4 |
| Import Completeness | T3.4, T6.1 |
| Request Listing | T5.1 |
| Filtering by Month | T2.3, T5.1 |
| Filtering by ESTADO | T2.3, T5.1 |
| Flagged Row Review Surface | T2.4, T5.3 |
| Search by RUT or Name | T2.3, T5.1 |
| Combined Filters | T2.3, T5.1 |

---

## Review Workload Forecast

- **Estimated changed files**: ~38 (2 csproj, 1 sln, 5 Domain, 2 Data, 4
  Import, 1 Configuration, ~8 Dashboard Pages/Layout/CSS, ~15 test files +
  fixtures, `Program.cs`, `appsettings.json`, `.gitignore`).
- **Estimated changed lines**: ~2,400–3,000 across the full change (raw SQL
  repository + schema ≈ 350, domain value objects ≈ 250, importer pipeline
  + canonicalizer ≈ 400, CLI wiring ≈ 100, three Razor Pages + CSS ≈ 700,
  test suite (unit + fixture-driven) ≈ 900–1,200). This is a full greenfield
  vertical slice, not an incremental patch.
- **400-line budget risk**: High if delivered as a single PR/commit set —
  every group individually likely exceeds 400 lines (e.g. Import layer
  alone ≈ 400, Pages+CSS ≈ 700). Group boundaries (0–6 above) are natural
  slice points, each independently testable and shippable.
- **Chained PRs recommended**: Yes — one slice per group (7 slices:
  Scaffold, Domain, Data, Import, CLI, Pages, Real-data validation) keeps
  each review under ~400 lines and matches the dependency graph so each
  slice is green (`dotnet test` passing) before the next starts.
- **Decision needed before apply**: Yes — per delivery_strategy
  `ask-on-risk`, the orchestrator MUST stop and ask whether to (a) proceed
  as 7 chained commits/PRs at the group boundaries above, or (b) proceed as
  a single `size:exception` commit given this is a solo greenfield project
  with direct-to-master commits and no PR review process currently in use.
  Note: since this project has no established PR flow, "chained PRs" would
  in practice mean sequential commits at each group boundary rather than
  GitHub PRs — recommend confirming with the user whether group-boundary
  commits (with `dotnet test` green at each boundary) satisfy the intent of
  the guard, or whether an actual PR flow should be introduced first.
