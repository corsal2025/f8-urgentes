# Historical Import Specification

## Purpose

One-time ClosedXML import of `URGENTES DIARIOS.xlsx` (sheets MAYO–JULY, 633 rows) into the request store, normalizing data and flagging anything dubious instead of dropping it.

## Requirements

### Requirement: Sheet Selection

The system MUST import only sheets with data (MAYO, JUNIO, JULIO) and MUST skip empty template sheets (AGOSTO–DICIEMBRE) without error.

#### Scenario: Skip empty template sheet
- GIVEN the workbook contains an AGOSTO sheet with only header rows
- WHEN the import runs
- THEN AGOSTO is skipped
- AND no rows are created or flagged for it

### Requirement: Header-Name Column Mapping

The system MUST map columns by canonicalized header name (trimmed, whitespace-collapsed, case-insensitive, accent-insensitive), not by column position, because MAYO uses a different column order and set than JUNIO/JULIO.

#### Scenario: Import MAYO sheet with reordered columns
- GIVEN the MAYO sheet has 10 columns with FECHA PETICION last and no FECHA DE SUBIDA column
- WHEN the import runs
- THEN each value is assigned to the correct field by its header name
- AND FECHA DE SUBIDA is left as typed null for all MAYO rows

#### Scenario: Import JUNIO/JULIO sheet with standard columns
- GIVEN a sheet has 11 columns starting with FECHA PETICION
- WHEN the import runs
- THEN all 11 fields including FECHA DE SUBIDA are mapped correctly

### Requirement: RUT Normalization and Flagging

The system MUST normalize imported RUTs (strip dots, uppercase K) to canonical `NNNNNNNN-D` form. If the check digit does not match, the system MUST still import the row and flag it for review — it MUST NOT reject or drop the row.

#### Scenario: Import row with valid RUT
- GIVEN an Excel row with RUT `12.345.678-9` where the check digit is correct
- WHEN the import runs
- THEN the row is imported with RUT `12345678-9` and not flagged for RUT reasons

#### Scenario: Import row with mismatched RUT check digit
- GIVEN an Excel row with a RUT whose check digit does not match
- WHEN the import runs
- THEN the row MUST still be imported
- AND the row MUST be flagged for review with a RUT-check-digit reason

### Requirement: "S/C" Date Handling

The system MUST convert the literal value `S/C` ("sin carpeta") in any date field to a typed null, not an error or a flag.

#### Scenario: Import row with S/C in a date field
- GIVEN a row has `S/C` in FECHA CARPETA ANTERIOR
- WHEN the import runs
- THEN the field is stored as null
- AND the row is not flagged for that field

### Requirement: Corrupt Cell Flagging

The system MUST import rows containing corrupt or unparseable cells (e.g. a date serial out of valid range, or non-date text in a date column) using a best-effort or null value for that field, and MUST flag the row for review rather than rejecting the entire row.

#### Scenario: Import row with out-of-range date serial
- GIVEN a cell contains the numeric serial `29262516` in a date column
- WHEN the import runs
- THEN the row is imported with that field left null
- AND the row MUST be flagged for review with a corrupt-cell reason

#### Amendment Note (W2)
In the real workbook, MAYO row H54 contains an unrecoverable corrupt cell (throws when ClosedXML's DateTime.FromOADate tries to parse the raw serial). The importer flags this row with the reason `UNPARSEABLE` and stores a `CORRUPT_CELL_VALUE` sentinel in the affected field, importing the row without loss. This satisfies the requirement: the row is imported, flagged, and never silently dropped.

### Requirement: Unknown Status Value Handling

The system MUST import rows with ESTADO or ESTADO ACTUAL values outside the known sets, storing the value verbatim and flagging the row for review.

#### Scenario: Import row with unrecognized ESTADO
- GIVEN a row has ESTADO `OTRO VALOR` not in the known set
- WHEN the import runs
- THEN the row is imported with ESTADO stored verbatim as `OTRO VALOR`
- AND the row MUST be flagged for review

### Requirement: Empty Row Skipping

The system MUST silently skip rows that are fully empty (padding rows), without creating a record or a flag.

#### Scenario: Skip fully empty row
- GIVEN a row in JUNIO has no values in any column
- WHEN the import runs
- THEN no request record is created for that row
- AND it is not counted as flagged

### Requirement: Import Completeness

The system MUST NOT silently drop any non-empty row: every non-empty row MUST result in either a clean imported record or a flagged imported record.

#### Scenario: Import summary accounts for all rows
- GIVEN the workbook has 633 non-empty data rows across MAYO–JULY (verified: MAYO 441, JUNE 110, JULY 82)
- WHEN the import completes
- THEN the sum of clean-imported and flagged-imported rows MUST equal 633

#### Amendment Note (W1)
The initial estimate of "~1,800" rows was an over-count based on template padding rows. Verification of the actual workbook OOXML against the importer logs confirms 633 non-empty rows: MAYO 441, JUNE 110, JULY 82. The invariant holds: clean + flagged = 633, 0 silent drops.
