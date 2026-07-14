# Spec: Dashboard Parity

## Requirement: Filter tabs

The Index page SHALL show four tabs: Todos, Pendientes, Subidas, Requiere
revisión. Requiere revisión SHALL show a badge with the count of flagged
(NeedsReview) requests.

### Scenario: Pendientes tab excludes uploaded cases
- GIVEN requests with EstadoActual "SUBIDA A CONASET" and others without it
- WHEN the Pendientes tab is selected
- THEN only requests whose EstadoActual is not "SUBIDA A CONASET" are shown

### Scenario: Subidas tab shows only uploaded cases
- GIVEN requests with and without EstadoActual "SUBIDA A CONASET"
- WHEN the Subidas tab is selected
- THEN only requests with EstadoActual "SUBIDA A CONASET" are shown

## Requirement: Search

The header SHALL provide a search input filtering by RUT or name, reusing the
existing repository search.

## Requirement: Quick-add form

The Index page SHALL provide an inline form to add one or more cases at once
(Nombre completo, RUT, Estado), with a client-side "+ Agregar otro" control to
add extra rows before submitting.

### Scenario: Adding multiple cases in one submission
- GIVEN two valid name/rut rows submitted together
- WHEN the form posts to AddManualCases
- THEN both requests are inserted with today's FechaPeticion and Origin "Web"

## Requirement: Inline edit

Each row SHALL allow editing NOMBRE and RUT inline; on change, the value is
persisted immediately via a POST handler.

## Requirement: Mark uploaded

Each Pending row SHALL have a "Marcar subida" button that sets EstadoActual to
"SUBIDA A CONASET" and FechaDeSubida to today.

## Requirement: Delete

Each row SHALL have a delete action that permanently removes the request.

## Requirement: Countdown chip

Each row SHALL show a chip with business days remaining until the 15-business-day
deadline computed from FechaPeticion — green when there is time, red when
overdue.
