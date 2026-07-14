# Tasks: f8-dashboard-parity

## Backend

- [x] 1.1 Port `DeadlineCalculator` from reference (business-day math)
- [x] 1.2 Add `Delete(long id)` to `IUrgentRequestRepository` / `UrgentRequestRepository`
- [x] 1.3 `IndexModel`: add tab filtering (Todos/Pendientes/Subidas) over EstadoActual
- [x] 1.4 `IndexModel`: `OnPostSetPersonData` (inline Nombre/RUT edit)
- [x] 1.5 `IndexModel`: `OnPostMarkUploaded` (EstadoActual=SUBIDA A CONASET, FechaDeSubida=today)
- [x] 1.6 `IndexModel`: `OnPostDeleteCase`
- [x] 1.7 `IndexModel`: `OnPostAddManualCases` (batch insert)
- [x] 1.8 `IndexModel`: `DiasHabilesRestantes` countdown helper (15 business days)

## UI

- [x] 2.1 `_Layout.cshtml`: dark header brand block (already dark-gradient; parity satisfied)
- [x] 2.2 `Index.cshtml`: filter tabs with Requiere revisión badge
- [x] 2.3 `Index.cshtml`: header search form
- [x] 2.4 `Index.cshtml`: quick-add form with "+ Agregar otro" JS
- [x] 2.5 `Index.cshtml`: table with inline edit, countdown chip, estado pill, Marcar subida, delete

## Verification

- [x] 3.1 `dotnet test` green (89/89)
- [x] 3.2 Manual curl smoke test (`/`, tab query) — 200/200
