# Proposal: F8 Urgent Requests Web App

## Intent

The Depto. de Licencias (Chile) tracks daily urgent driver-license-folder requests in a single spreadsheet, `URGENTES DIARIOS.xlsx` (~1,800 real rows). It is fragile: inconsistent RUT formats, corrupt cells, no validation, no concurrency control, no history guarantees. Replace it with a web app that owns the data, enforces validation, and preserves the full history via a one-time import.

## Scope

### In Scope
- .NET 10 / ASP.NET Core Razor Pages + SQLite web app mirroring the outlook-comuna-router stack and dashboard visuals
- One-time ClosedXML historical importer (MAYO–JULIO sheets, ~1,800 rows)
- Full web management: create requests, change ESTADO / ESTADO ACTUAL, dashboard list and filter
- A review surface exposing dubious imported cells
- xUnit strict-TDD coverage

### Out of Scope (Non-goals)
- Repeatable or scheduled re-import (import is one-time)
- Authentication, roles, or audit log
- CONASET integration or automatic F8-code generation (statuses stay manual, as in the Excel)
- Editing the empty AUG–DEC template sheets (skipped on import)
- Any write-back to the original Excel

## Capabilities

### New Capabilities
- `urgent-request-management`: request domain + persistence (RUT, names, last/previous folder dates, F8 code, ESTADO, ESTADO ACTUAL, request/upload dates); create and state transitions
- `historical-import`: one-time ClosedXML importer — header-name column mapping, RUT normalization, "S/C" to typed null, corrupt-cell flagging, empty-sheet skip
- `dashboard`: visual replica of the outlook-comuna-router dashboard; list, filter, and review-flag surface

### Modified Capabilities
- None (greenfield project)

## Approach

Replicate outlook-comuna-router's layered layout (Domain / Application / Presentation). Model the request as a domain entity with value objects (Rut; FolderDate allowing a typed null). The importer reads by header name because the MAY sheet reorders columns and omits FECHA DE SUBIDA; it normalizes and writes flagged rows rather than rejecting them. SQLite via Microsoft.Data.Sqlite, no ORM. All artifacts in English; fixed Spanish domain terms (RUT, CODIGO F8, CONASET) kept verbatim.

## Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Corrupt/ambiguous cells (e.g. MAY H54 serial) | High | Import and flag for review, never silently drop |
| MAY sheet has a different column order | High | Header-name mapping, not positional |
| RUT format drift (dots, lowercase k check digit) | Med | Normalize and validate check digit on import |

## Rollback Plan

Greenfield, no production data. Rollback is deleting the scaffold and the generated SQLite file under `data/`. The source `.xlsx` stays untouched — the importer is read-only on Excel.

## Dependencies

Microsoft.Data.Sqlite 10.0.9, SQLitePCLRaw.bundle_e_sqlite3 3.0.3, ClosedXML (MIT), xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.14.1 + coverlet.collector 6.0.4.

## Success Criteria

- [ ] All ~1,800 MAYO–JULIO rows imported; dubious cells flagged, none silently lost
- [ ] Users create requests and change states via web; changes persist in SQLite
- [ ] Dashboard visually matches outlook-comuna-router
- [ ] `dotnet test` passes under strict TDD
