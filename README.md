# Well Log Reader

A C# console program that reads LAS 2.0 well-log files, cleans (conditions) the
data, and saves one log plot (PNG) per well plus a conditioning report.

For every `*.las` file in an input folder it:

1. **loads** the file into an in-memory model (a bad file never crashes the program),
2. **conditions** the data (missing values, depth order and sampling, units, curve
   names that mean the same measurement) and records every change,
3. **plots** one PNG per well: gamma ray, resistivity (log scale) and bulk density
   with neutron porosity, depth increasing downward,
4. **reports** everything it found in `report.md` and `report.csv`.

No LAS-parsing library is used; the reader is written from the LAS 2.0 rules.

---

## Requirements

- Windows with .NET Framework 4.8 (the program targets `net48`, C# 10).
- A .NET SDK to build (tested with SDK 9.0.313).
- NuGet packages: [ScottPlot 5.1.59](https://www.nuget.org/packages/ScottPlot/5.1.59)
  (plotting, uses SkiaSharp) and NUnit for the tests. They are restored by `dotnet build`.

## Build and test

```
dotnet build
dotnet test
```

## Run

```
well-log-reader\bin\Debug\net48\well-log-reader.exe <inputFolder> <outputFolder>
```

Example with the sample data:

```
well-log-reader\bin\Debug\net48\well-log-reader.exe TestData output
```

- Reads every `*.las` file in `inputFolder` (case-insensitive, not recursive).
- Writes `outputFolder/plots/<file>.png`, `outputFolder/report.md` and
  `outputFolder/report.csv`. Folders are created when needed.
- Prints one progress line per file and a summary at the end:

```
[  1/203] 39399.LAS  OK  (3 warnings)
...
[ 62/203] 4200345990_20140426_LAS(masmp).LAS  REJECTED  (2 warnings)
...
Files:          203
  OK:           10
  With warnings: 187
  Not plotted:  0
  Rejected:     6
Total time:     164.5 s  (load 83.5 s, condition 0.8 s, plot 80.1 s)
```

Exit codes: `0` the run completed (even if some files were rejected),
`2` invalid arguments, `1` unexpected failure of the whole run.

The sample files (`TestData/`, about 1 GB) and the program output (`output/`) are
not committed to git.

---

## Solution layout

| Project | Role |
|---|---|
| `well-log-reader` | Console app: arguments, file loop, progress, run summary. |
| `well-log-reader-backend` | Class library: model, reader, conditioning, catalog, plotting, reports. |
| `well-log-reader-backend-tests` | NUnit tests (72), with small LAS samples written as strings. |

Main classes in the library:

| Area | Classes |
|---|---|
| Model | `Header`, `Curve`, `Well`, `ReportEntry`, `Stage`, `Severity`, `CurveFamily` |
| Reading | `LasReader` (sections, header, rejection rules), `DataSectionReader` (`~A` data, wrap, NULL), `HeaderLineSplitter`, `LasText` |
| Conditioning | `Conditioner` runs the steps; one small class per step implementing `IConditioningStep` |
| Curve catalog | `CurveCatalog` (the one file to edit), `CurveFamilyInfo` |
| Plot | `LogPlotRenderer`, `PlotSettings` (all sizes and scales) |
| Run | `WellProcessor` (one file through all stages), `WellSummary`, `RunStatistics`, `ReportWriter` |

## Processing pipeline

For each file, one after another:

1. **Load** (`LasReader`): read line by line, split header lines, read the data,
   turn NULL values into NaN, reject files that cannot be used.
2. **Condition** (`Conditioner`), in this order:
   1. NULL → NaN (already done while reading)
   2. skip rows without depth
   3. depth unit → metres (also STRT/STOP/STEP)
   4. stable sort by depth (shallow to deep)
   5. remove duplicate depths, keep the first in file order
   6. sampling checks: median step vs STEP, STRT/STOP vs data, gaps
   7. identify gamma ray, resistivity, density and neutron curves with the catalog
   8. unit conversion for those four families
   9. physical checks (impossible values → NaN, unusual values → warning)
3. **Plot** (`LogPlotRenderer`): one PNG per depth-indexed well.
4. Keep a small summary; release the large arrays; next file.
5. At the end, write `report.md` and `report.csv`.

Each conditioning step is a small class with one method that changes the well and
returns its report entries, so steps can be added or reordered easily.

---

## Design decisions

### Data model

- **D1 – Depth comes from the data, never from STRT/STEP.** STRT, STOP and STEP are
  used only to check the data. If a row is missing, computing depth from STEP would
  shift every later value to the wrong depth without any visible sign.
- **D2 – One shared depth array; curves are aligned by row index.**
  `Curve.Values[i]` belongs to `Well.Depth[i]`. Re-ordering or dropping rows is done
  once with one list of row indexes applied to every curve (`WellRows.Keep`), so
  curves can never drift away from their depth. Depth values are not used as keys
  because they can repeat and floating-point equality is unreliable.
- **D3 – Missing values become NaN at read time; rows are not deleted.** `0` or the
  original sentinel could look like real values; NaN makes careless calculations
  fail visibly. NULL is compared as a parsed `double` before converting to `float`.
- **D4 – Curve values are `float`, depth is `double`.** LAS values have about 4–5
  significant digits, so `float` halves the memory of the largest data. Depth is a
  single array, so `double` is cheap and avoids precision problems in comparisons
  and unit conversion.
- **D5 – One header-line splitter and one `Header` type for all sections.** LAS 2.0
  rule: first `.` ends the mnemonic, first space after it ends the unit, **last** `:`
  separates value and description (times such as `13:45:10` contain colons). All
  lines are kept in file order; duplicates are kept and reported. For LAS 1.2 files
  the `~W` values (except STRT/STOP/STEP/NULL) are taken from after the colon, as in
  `WELL.  WELL:  ANY ET AL OIL WELL #12` (checked against a real LAS 1.2 sample).

### Reading

- **D6 – A header line that cannot be split is skipped, never guessed.** Cases: no
  dot, no colon, the first dot comes after the last colon, or nothing before the
  dot. Guessing would add special cases forever and make results less trustworthy;
  the report tells the file owner which line to fix.
- **D7 – Report entries are actionable.** Every entry has file, line number, section,
  raw line, curve, stage, severity and a plain-English reason.
- **D8 – Process one well at a time.** The assignment says "read all files into an
  in-memory model". Each well *is* fully loaded into the in-memory model, but only
  one well at a time: load → condition → plot → keep a small summary → release the
  arrays. Memory use therefore does not grow with the number of wells (the sample
  set is 1 GB; a real project can have thousands of wells). The reports are written
  at the end from the small summaries.
- **D9 – No-colon lines are skipped in every section, including `~C`.** If that
  makes the number of curves differ from the number of data columns, the file is
  rejected and the report shows both the reason and the line that caused it. In a
  **wrapped** file the column count cannot be checked, so any skipped `~C` line
  rejects the file; otherwise every value would silently shift into the wrong curve.
- **D10 – A data row with the wrong number of values is skipped.** Because depth
  comes from the data (D1), skipping a row never shifts other rows. The data column
  count is the count that most rows have, so one bad first row does not reject the
  file. The first 20 bad rows are reported in detail, then a total. Wrapped data is
  read as one stream and cut into rows; leftover values are reported and dropped.
  A single non-numeric token becomes NaN and is counted.
- **D11 – No `NULL` line → use −999.25 and warn.**
- **D12 – When a file is rejected:** cannot be opened or empty; no `~C` or `~A`;
  curve count ≠ data column count; no usable data row. A file whose first column is
  not depth (for example TIME) is loaded but not plotted. A file with none of the
  four target curves is still plotted with "No data" tracks.

### Conditioning

- **D13 – Depth is converted to metres.** Unit from the first `~C` curve, else from
  STRT. Feet × 0.3048. If the unit is unknown the depth is not converted, a warning
  is written and the plot says "unit unknown".
- **D14 – Depth order, duplicates, missing depth, sampling.** Rows without depth are
  skipped. Rows are sorted shallow to deep with a **stable** sort, so for repeated
  depths the row that came first in the file is kept. **No resampling**; the median
  step is compared with STEP (1 % tolerance; STEP 0 means irregular sampling),
  STRT/STOP with the data range, and steps larger than 1.5 × the median are reported
  as gaps.
- **D15 – Finding the four curves.** The alias table is one C# file
  (`CurveCatalog.cs`). A curve is selected by mnemonic (case-insensitive) **and**
  unit: a curve with a unit that is not allowed for the family is not selected and a
  warning is written. Lookalikes such as `DRHO`, `DPHI`, `POTA/THOR/URAN` are simply
  not listed. Gamma ray, density and neutron use the first match in catalog order;
  **all** resistivity curves are plotted, grouped deep / medium / shallow. Density
  `K/M3`, `KG/M3` → g/cc and neutron `%`, `PU` → v/v; `CFCF`, `FT3/FT3`, `M3/M3` are
  accepted as v/v. Catalog meanings were checked where possible against published
  mnemonic lists (LDEO's list of SLB mnemonics, Geologic's LAS mnemonic list; the SLB
  Curve Mnemonic Dictionary site was unreachable); aliases were not added just
  because they appear in the sample files.
- **D16 – Impossible vs unusual values.** Impossible (resistivity ≤ 0, density ≤ 0,
  GR < 0) → NaN with a count. Unusual but possible (GR > 500, density outside
  1.0–3.5 g/cc, neutron outside −0.15–1.0 v/v) → warning only. Thresholds live in
  the catalog.

### Plot and output

- **D17 – ScottPlot 5, fixed image size (1100 × 2000 px).** Each track is its own
  ScottPlot plot with the same fixed padding and depth range; the tracks are drawn
  side by side on one SkiaSharp canvas. Depth increases downward (y-limits set with
  the deepest value at the bottom). Resistivity is drawn as log10 values with tick
  labels 0.2–2000. Density uses the bottom axis (1.95–2.95 g/cc) and neutron the top
  axis, reversed (0.45 → −0.15 v/v). Curves are split at NaN so gaps are never
  joined. Values outside a scale are clipped at the track edge. Solid lines with one
  colour per group are used, because dashed lines turn into noise on dense log data.
- **D18 – Outputs and run mode.** Sequential processing. Plot name = LAS name with
  `.png`; the well name is in the title. `report.md` for people, `report.csv` for
  Excel (RFC 4180 quoting, UTF-8 with BOM, control characters replaced by spaces).

---

## Results on the sample data

203 files in `TestData`, about 164 s on the development machine (load 84 s,
conditioning 1 s, plot 80 s):

| Result | Files |
|---|---|
| OK | 10 |
| OK with warnings | 187 |
| Not plotted | 0 |
| Rejected | 6 |

Most warnings are "No ... curve found" and duplicate mnemonics. Of the 197 plotted wells: gamma ray found in 119,
resistivity in 70, density in 88, neutron in 62; 47 have none of the four.

The 6 rejected files and why:

| File | Reason |
|---|---|
| `4200340122MAINCOR.las` | `WRAP. NO` but the data is wrapped (~C has 26 curves, rows have 6 values). |
| `4200345990_20140426_LAS(masmp).LAS` | Wrapped file with `~C` lines whose mnemonic starts with a dot (`.DT57`), which were skipped (D9). |
| `...9365LATST2_2_0).las` | A `~C` line with an empty mnemonic, so 33 curves for 34 columns (D9). |
| 3 × LAS 3.0 files (`..._Mud_log...`, `...9365LAT_3_0).las`) | Comma-delimited data; LAS 3.0 is not supported. |

---

## Known limitations

- Wells are processed sequentially (no parallelism).
- One depth index per file; curves from several files of the same well are not merged.
- No resampling.
- Only the four plotted families get unit conversion; other curves keep their units.
- The alias table is compiled into the code (moving it to JSON is future work).
- Some real resistivity curves are not in the catalog because their meaning could
  not be verified (for example HRLA `RLA1`–`RLA5`, HRLT `RT_HRLT`, `RXO_HRLT`), so
  those wells show fewer or no resistivity curves.
- Non-standard unit spellings (`DECP`, `PERC`, `DECI`, `AAPI`, …) are not accepted;
  such curves are reported but not plotted.
- Several resistivity curves of the same group share one colour.
- LAS 3.0 is read only as far as the LAS 2.0 rules allow (comma-delimited data is rejected).
- A file whose first column is not depth is loaded but not plotted.
- Rendering depends on ScottPlot / SkiaSharp.

---

## How I used AI

