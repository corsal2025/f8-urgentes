# Dashboard Specification

## Purpose

Provides a visual list-and-filter surface for urgent requests, replicating the outlook-comuna-router dashboard style, including a review view for import-flagged rows, with tab-based filtering and inline editing capabilities.

## Requirements

### Requirement: Request Listing

The system MUST display all urgent requests in a list/table view with RUT, name, ESTADO, ESTADO ACTUAL, request date, and F8 code visible.

#### Scenario: View dashboard with existing requests
- GIVEN the store has imported and manually created requests
- WHEN a user opens the dashboard
- THEN all requests are listed with their key fields visible

### Requirement: Filtering by Month

The system MUST allow filtering the request list by the month of FECHA PETICION.

#### Scenario: Filter by month
- GIVEN requests exist across MAYO, JUNIO, and JULIO
- WHEN a user filters by JUNIO
- THEN only requests with FECHA PETICION in June are shown

### Requirement: Filtering by ESTADO

The system MUST allow filtering the request list by ESTADO and/or ESTADO ACTUAL value.

#### Scenario: Filter by ESTADO
- GIVEN requests exist with varying ESTADO values
- WHEN a user filters by ESTADO `SUBIR CON F8`
- THEN only matching requests are shown

### Requirement: Filter tabs

The Index page SHALL show four tabs: Todos, Pendientes, Subidas, Requiere revisión. Requiere revisión SHALL show a badge with the count of flagged (NeedsReview) requests.

#### Scenario: Pendientes tab excludes uploaded cases
- GIVEN requests with EstadoActual "SUBIDA A CONASET" and others without it
- WHEN the Pendientes tab is selected
- THEN only requests whose EstadoActual is not "SUBIDA A CONASET" are shown

#### Scenario: Subidas tab shows only uploaded cases
- GIVEN requests with and without EstadoActual "SUBIDA A CONASET"
- WHEN the Subidas tab is selected
- THEN only requests with EstadoActual "SUBIDA A CONASET" are shown

### Requirement: Flagged Row Review Surface

The system MUST provide a filter or dedicated view that shows only requests flagged during import, along with the flag reason(s). This is exposed via the "Requiere revisión" tab.

#### Scenario: View flagged requests
- GIVEN the import flagged rows for RUT mismatch and corrupt-cell reasons
- WHEN a user opens the flagged-review filter or Requiere revisión tab
- THEN only flagged requests are shown
- AND each shown request displays its flag reason(s)

### Requirement: Search by RUT or Name

The system MUST allow searching the request list by RUT (any input format, normalized before matching) or by partial applicant name. The header SHALL provide a search input filtering by RUT or name, reusing the existing repository search.

#### Scenario: Search by RUT with dots
- GIVEN a request exists with RUT `12345678-9`
- WHEN a user searches `12.345.678-9`
- THEN the matching request is shown

#### Scenario: Search by partial name
- GIVEN a request exists for applicant "Maria Perez"
- WHEN a user searches "Perez"
- THEN the matching request is shown

### Requirement: Quick-add form

The Index page SHALL provide an inline form to add one or more cases at once (Nombre completo, RUT, Estado), with a client-side "+ Agregar otro" control to add extra rows before submitting.

#### Scenario: Adding multiple cases in one submission
- GIVEN two valid name/rut rows submitted together
- WHEN the form posts to AddManualCases
- THEN both requests are inserted with today's FechaPeticion and Origin "Web"

### Requirement: Inline edit

Each row SHALL allow editing NOMBRE and RUT inline; on change, the value is persisted immediately via a POST handler.

### Requirement: Mark uploaded

Each Pending row SHALL have a "Marcar subida" button that sets EstadoActual to "SUBIDA A CONASET" and FechaDeSubida to today.

### Requirement: Delete

Each row SHALL have a delete action that permanently removes the request.

### Requirement: Countdown chip

Each row SHALL show a chip with business days remaining until the 15-business-day deadline computed from FechaPeticion — green when there is time, red when overdue.

### Requirement: Combined Filters

The system MUST allow combining month, ESTADO, flagged, tab filters, and search filters simultaneously.

#### Scenario: Combine filters
- GIVEN requests span multiple months and states
- WHEN a user filters by month JUNIO, ESTADO `PRIMERA LICENCIA`, and tab Pendientes
- THEN only requests matching all conditions are shown
