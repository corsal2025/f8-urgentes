# Dashboard Specification

## Purpose

Provides a visual list-and-filter surface for urgent requests, replicating the outlook-comuna-router dashboard style, including a review view for import-flagged rows.

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

### Requirement: Flagged Row Review Surface

The system MUST provide a filter or dedicated view that shows only requests flagged during import, along with the flag reason(s).

#### Scenario: View flagged requests
- GIVEN the import flagged rows for RUT mismatch and corrupt-cell reasons
- WHEN a user opens the flagged-review filter
- THEN only flagged requests are shown
- AND each shown request displays its flag reason(s)

### Requirement: Search by RUT or Name

The system MUST allow searching the request list by RUT (any input format, normalized before matching) or by partial applicant name.

#### Scenario: Search by RUT with dots
- GIVEN a request exists with RUT `12345678-9`
- WHEN a user searches `12.345.678-9`
- THEN the matching request is shown

#### Scenario: Search by partial name
- GIVEN a request exists for applicant "Maria Perez"
- WHEN a user searches "Perez"
- THEN the matching request is shown

### Requirement: Combined Filters

The system MUST allow combining month, ESTADO, flagged, and search filters simultaneously.

#### Scenario: Combine filters
- GIVEN requests span multiple months and states
- WHEN a user filters by month JUNIO and ESTADO `PRIMERA LICENCIA`
- THEN only requests matching both conditions are shown
