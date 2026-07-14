# Proposal: f8-dashboard-parity

## Intent

Bring the F8Urgentes dashboard (`/Index`) to visual and functional parity with the
reference OutlookComunaRouter dashboard: dark header with brand block, filter
tabs (Todos / Pendientes / Subidas / Requiere revisión), header search, an
inline quick-add form for multiple cases, and per-row inline edit / mark
uploaded / delete actions with a business-day countdown chip.

## Scope

- `Dashboard/Pages/_Layout.cshtml` — dark header brand text.
- `Dashboard/Pages/Index.cshtml` + `Index.cshtml.cs` — tabs, search, quick-add
  form, inline edit, mark uploaded, delete, countdown chip.
- `Domain/DeadlineCalculator.cs` — already ported (business-day math).
- `Data/IUrgentRequestRepository.cs` / `UrgentRequestRepository.cs` — `Delete`
  already added.

## Out of scope

- Comuna directory / email sync features from the reference app (F8 has no
  comuna routing).
- Authentication/login (F8 dashboard has none, reference does — not ported).
