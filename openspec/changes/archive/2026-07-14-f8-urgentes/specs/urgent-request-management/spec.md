# Urgent Request Management Specification

## Purpose

Owns the urgent license-folder request domain: creation, persistence, and state transitions (ESTADO / ESTADO ACTUAL), replacing the manual spreadsheet workflow.

## Requirements

### Requirement: Request Creation

The system MUST allow creating a new urgent request with RUT, applicant name, F8 code, request date, last/previous folder dates, ESTADO, and ESTADO ACTUAL.

#### Scenario: Create a valid request
- GIVEN a user fills the request form with a valid RUT and required fields
- WHEN they submit the form
- THEN a new request is persisted in SQLite with a generated identifier
- AND the request appears in the dashboard list

#### Scenario: Reject request with invalid RUT check digit
- GIVEN a user enters a RUT whose check digit does not match the computed value
- WHEN they submit the form
- THEN the system MUST reject the submission and show a validation error
- AND no record is persisted

### Requirement: RUT Normalization

The system MUST normalize any RUT input to canonical form `NNNNNNNN-D` (digits, no dots, uppercase `K` check digit) before persisting or comparing.

#### Scenario: Normalize RUT with dots and lowercase k
- GIVEN a user enters `12.345.678-k`
- WHEN the value is validated
- THEN it MUST be stored as `12345678-K`

### Requirement: State Transitions

The system MUST allow changing ESTADO and ESTADO ACTUAL independently on an existing request, and MUST allow setting FECHA DE SUBIDA when ESTADO ACTUAL becomes `SUBIDA A CONASET`.

#### Scenario: Change ESTADO
- GIVEN an existing request with ESTADO `SUBIR CON F8`
- WHEN a user changes ESTADO to `CARPETA SUBIDA`
- THEN the request is updated and the change is persisted
- AND the dashboard reflects the new ESTADO immediately

#### Scenario: Set FECHA DE SUBIDA on CONASET upload
- GIVEN an existing request with ESTADO ACTUAL `PENDIENTE`
- WHEN a user sets ESTADO ACTUAL to `SUBIDA A CONASET` and provides an upload date
- THEN FECHA DE SUBIDA MUST be persisted with that date

### Requirement: Known Status Value Sets

The system MUST accept known ESTADO values (`SUBIR CON F8`, `PRIMERA LICENCIA`, `CAMBIO DE DOMICILIO`, `CREAR CERTIFICADO`, `CARPETA SUBIDA`) and known ESTADO ACTUAL values (`SUBIDA A CONASET`, `PENDIENTE`, `CREAR CERTIFICADO`, empty). It MUST also accept unrecognized values entered via editing, storing them verbatim and flagging the request for review.

#### Scenario: Enter an unrecognized ESTADO value
- GIVEN a user types an ESTADO value not in the known set
- WHEN they save the request
- THEN the value MUST be stored verbatim
- AND the request MUST be flagged for review

### Requirement: Request Editing

The system MUST allow editing any field of an existing request (name, dates, F8 code) without deleting the request's identity or import history.

#### Scenario: Edit applicant name
- GIVEN an existing request
- WHEN a user corrects the applicant name and saves
- THEN the stored name is updated
- AND the request's identifier and creation history remain unchanged
