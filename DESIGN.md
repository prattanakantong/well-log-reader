# DESIGN.md — Well Log Reader

This document is the single source of truth for the design.
Decisions **D1–D18** were made by the developer. Items marked
**[default]** are implementation defaults that were not discussed in detail;
follow them, but tell the developer when you rely on one.
Items marked **[verify]** must be checked (against documentation or the real
files) before relying on them.

---

## 1. Goal

A console program that, for every `*.las` file in an input folder:

1. **Loads** the file into an in-memory model (a bad file must never crash the
   program; report what could not be read and why).
2. **Conditions** the data (missing values, depth order and sampling, units,
   curve names that mean the same measurement) and records every change.
3. **Plots** one PNG per well: depth on the vertical axis increasing downward,
   tracks for gamma ray, resistivity (log scale), and bulk density with neutron
   porosity on the same track. A well missing a curve still plots what it has.

The program must work on LAS files it has never seen. Do not tune it to the
sample files.

---

## 2. Solution layout

The developer already created a solution with two projects:

- a **console app** (the CLI, "front of the shop"),
- a **class library** (the core/backend, "the kitchen": parsing, conditioning,
  plotting, reporting).

Inspect the `.sln` file to learn their exact names and default namespaces and
use them as they are. **Do not rename anything.** The console project references
the library; the library must not reference the console project.

A class `Header` already exists in the library (`Header.cs`, see §3). Keep it.

Add a **test project** (net48) in milestone M2. Choose a test framework that
supports net48 [verify].

---

## 3. Data model (D1–D5)

### Header (exists)

One parsed header line from `~V`, `~W`, `~C` or `~P`.

| Property | Type | Meaning |
|---|---|---|
| `mnem` | string | mnemonic (text before the first dot) |
| `unit` | string | text between the first dot and the first space after it |
| `value` | string | text between that space and the last colon (kept as raw text) |
| `description` | string | text after the last colon |
| `line` | int | 1-based line number in the source file |

`value` is a string on purpose: header values can be numbers, names or dates.
Convert to a number only where needed.

### Curve

| Property | Type | Meaning |
|---|---|---|
| `Info` | Header | the `~C` line that defines this curve |
| `Values` | float[] | one value per row; same length as `Well.Depth`; missing = `float.NaN` |

Plus whatever conditioning needs (for example the curve family, see D15).

### Well

- source file path and name;
- LAS version text and wrap flag (file information, used by the reader and the
  report only);
- all header items, **as ordered lists per section, duplicates allowed** (D5);
- raw text of `~O` and of any unknown sections [default];
- `Depth`: `double[]`, **one shared array for all curves** (D2);
- depth unit after conditioning (`"m"` or unknown, see D13);
- list of `Curve`;
- convenience accessors for frequently used header values
  (well name `WELL`, `UWI`, `COMP`, `NULL`, `STRT`, `STOP`, `STEP`) so nobody has
  to type mnemonic strings elsewhere (D5). If a mnemonic appears more than once,
  the accessor returns the first one; the duplicate is reported (D5).

### Report entry

Every warning, error and conditioning change is one entry (D7):

| Field | Example |
|---|---|
| File name | `WELL-A.las` |
| Line number (0 if not about a line) | `14` |
| Section | `~W` |
| Raw line text (only for line problems) | `WELL   SMITH #12   WELL NAME` |
| Curve mnemonic (optional) | `GR` |
| Stage | Load / Condition / Plot |
| Severity | Fatal (file rejected) / Warning / Change |
| Message (the reason, in plain English) | `No dot or colon; line skipped` |

A line counter is a single `int` incremented while reading. Line numbers and
raw text are stored **only** when a problem is found.

---

## 4. Decisions

### D1 — Depth comes from the data, never from STRT/STEP
Depth of every row is read from the first column. `STRT`, `STOP`, `STEP` are
used only to **check** the data; mismatches are reported.
Reason: if a row is missing, computing depth from STEP shifts every later value
to the wrong depth without any visible sign.

### D2 — One shared depth array; curves are aligned by row index
`Curve.Values[i]` belongs to `Well.Depth[i]`. Depth values are not used as keys
(they can repeat, and floating-point equality is unreliable).
Reasons: less memory; re-ordering is done once with one permutation applied to
every curve, so curves can never drift apart.
Limitation: merging curves from several files with different sampling is not
supported.

### D3 — Missing values become NaN at read time; rows are not deleted
Any value equal to the file's NULL value becomes `NaN`. Rows are kept so curves
stay aligned with depth. `0` or the original sentinel are not used because they
can look like real values; `NaN` makes careless calculations fail visibly.
Check with `float.IsNaN` / `double.IsNaN`, never `==`.
Compare against the NULL value **as parsed doubles** before converting to
float [default].

### D4 — Curve values are `float`, depth is `double`
LAS values have about 4–5 significant digits, so `float` is enough and halves
the memory of the largest data. Depth is a single array, so `double` costs
about 4.5% extra memory and removes precision problems in depth comparison,
unit conversion and step checks.

### D5 — One header-line splitter and one `Header` type for all sections
`~V`, `~W`, `~C`, `~P` lines share the same format and use the same code.
Split rule (LAS 2.0): first `.` → mnemonic; first space after it → unit;
**last** `:` → value / description. Trim all parts.
All lines are stored in file order; duplicates are kept and reported.

LAS 1.2 note (verified against the lasio LAS 1.2 sample file): in LAS 1.2 `~W`,
items other than STRT/STOP/STEP/NULL put the value after the colon, and the text
before the colon is a label (`WELL.  WELL:  ANY ET AL OIL WELL #12`). When `VERS`
starts with `1.`, for those items **always** use `description` as the value.

### D6 — A header line that cannot be split is skipped, never guessed
The program continues reading. The line is reported. The file owner fixes the
source file.
A line cannot be split when it has no dot, no colon, its first dot comes after
its last colon (the dot is only in the description), or nothing is before the
dot (empty mnemonic). LAS 2.0: the mnemonic is required and contains no dots,
and the description is always to the right of the colon. Guessing would add special cases forever and make results less
trustworthy.

### D7 — Report entries are actionable
Every entry has file name, line number, section, raw line, reason, severity
(see §3).

### D8 — Process one well at a time
For each file: load → condition → plot → write its report part → release the
large arrays, then the next file. Only a small per-well summary (name, status,
counts, report entries) is kept until the end to build the final report.
Reason: memory use does not grow with the number of wells (thousands of wells).
Note for the README: the assignment says "read all files into an in-memory
model"; explain why this design was chosen.

### D9 — No-colon lines are skipped in every section, including `~C`
If skipping a `~C` line makes the number of curves differ from the number of
data columns, the file is rejected. The report must show both the rejection
reason **and** the line that caused it, e.g.
`~C has 2 curves but data has 3 columns` and
`line 22 (~C): no colon, line skipped "GR  .GAPI"`.
In a **wrapped** file the column count cannot be checked, so if any `~C` line
was skipped the file is rejected (otherwise every value would silently shift
into the wrong curve).

### D10 — A data row with the wrong number of values is skipped
Because depth is read from the data (D1), skipping a row never shifts other
rows. Report the first **20** bad rows in detail, then a total count
[default].
Wrapped files (`WRAP. YES`): read values as one stream and cut it into rows of
N values (N = number of curves). Leftover values at the end are reported and
dropped [default].
A single token that is not a number, in a row with the correct count, becomes
`NaN` and is counted in the report [default] (the column is known, so this is
not a guess).

### D11 — No `NULL` line → use -999.25 and warn

### D12 — When a file is rejected (Fatal)
- cannot be opened, or is empty;
- no `~C` section or no `~A` section;
- number of curves in `~C` ≠ number of data columns;
- no usable data row at all.

Not rejected:
- **First column is not depth** (e.g. TIME): load the well, do **not** plot it,
  report "not depth-indexed; log plot skipped". The first column counts as depth
  if its mnemonic looks like depth (`DEPT`, `DEPTH`, `MD`, ...) **or** its unit
  is a length (`M`, `F`, `FT`, ...) [default].
- **None of the four target curves found**: load and plot; tracks show
  "No data".

### D13 — Depth is converted to metres
Unit source: unit of the first `~C` curve; if empty, unit of `STRT` in `~W`.
Feet (`F`, `FT`, `FEET`, ...) × 0.3048. Metres (`M`, `METER(S)`, `METRE(S)`)
unchanged. `STRT/STOP/STEP` are converted too before comparison (D1).
If neither gives a unit: **do not convert**, label the plot "unit unknown",
and warn.

### D14 — Depth order, duplicates, missing depth, sampling
- Rows whose depth is missing (NULL / NaN / not a number) are **skipped** and
  reported (a row without depth has no position).
- Depth is always re-ordered to **increasing** (shallow → deep) with a
  **stable** sort; the same permutation is applied to every curve; reported.
- Duplicate depths: keep the row that comes **first in the source file**, drop
  the others; report the count.
- **No resampling.** Only check and report: median step vs header STEP, and
  gaps larger than [default] 1.5 × median step.

### D15 — Finding the four curves; units
- The alias table lives in **one C# file** (a "curve catalog") that is easy to
  edit. Moving it to JSON is future work.
- A curve is selected by **mnemonic** (case-insensitive, trimmed) **and** a
  **unit check**: if the curve has a unit and it is not an allowed unit for the
  family, it is not selected and a warning is written.
- Lookalikes such as `DRHO` (density correction), `DPHI` (density porosity),
  `POTA`/`THOR`/`URAN` (elements) and count-rate curves are simply **not listed**.
- For GR, density and neutron: if several curves match, pick the first one in
  the catalog order and report which one was chosen.
- **Resistivity: plot every matching curve** on the same track, grouped as
  deep / medium / shallow, each group with its own colour/style and a legend.
- Unit conversions:
  - density `K/M3`, `KG/M3` → g/cc (÷ 1000);
  - neutron `%`, `PU` → v/v (÷ 100);
  - GR and resistivity keep their units.
- A curve with **no unit**: do not convert, warn.
- Curves that are not in the four families keep their original units
  (limitation for the README).

Starting catalog [verify — check meanings against Energistics PWLS
(curves.energistics.org) or the SLB Curve Mnemonic Dictionary; do **not** add an
alias just because it appears in the sample files]:

| Family | Mnemonics (priority order) | Allowed units |
|---|---|---|
| Gamma ray | GR, GRC, ECGR, SGR, HSGR, GAM, GAMMA, CGR, HCGR | GAPI, API |
| Resistivity deep | ILD, RILD, IDPH, LLD, RLLD, RT, RD, RDEP, AT90, AHT90, AF90, AO90, M2R9 | OHMM, OHM.M, OHM-M, OHM_M |
| Resistivity medium | ILM, RILM, IMPH, AT60, AT30 | same |
| Resistivity shallow | LLS, SFLU, SFL, SFLA, AT20, AT10, MSFL, RXOZ, RXO8, MCFL | same |
| Bulk density | RHOB, RHOZ, DEN, DENS, ZDEN | G/C3, G/CC, GM/CC, G/CM3, K/M3, KG/M3 |
| Neutron porosity | NPHI, TNPH, NPOR, CNL, NPLS, NPSS | V/V, DEC, FRAC, CFCF, FT3/FT3, M3/M3, %, PU |

(`NPSS` is sandstone-matrix; if it is chosen, say so in the report.)

### D16 — Physically impossible vs unusual values
- Impossible → **NaN** and reported with a count:
  resistivity ≤ 0, density ≤ 0, GR < 0.
- Unusual but possible → **warning only**, data unchanged:
  GR > 500 GAPI, density outside 1.0–3.5 g/cc, neutron outside −0.15–1.0 v/v.
  (Thresholds are general knowledge [verify] and live in the catalog file.)

### D17 — Plotting with ScottPlot; fixed image height
- Use **ScottPlot 5** (supports .NET Framework 4.6.2+, depends on SkiaSharp).
  Pin an exact version in the `.csproj` (e.g. 5.1.59 — confirm on nuget.org).
  [verify] SkiaSharp native DLLs work from a clean `dotnet build` on net48; if
  not, try `<PlatformTarget>x64</PlatformTarget>` and tell the developer.
- Every image has the **same height** [default: 2000 px; width ≈ 1100 px];
  constants live in one place.
- Track layout, left to right:
  1. depth labels;
  2. GR: 0–150 GAPI, linear;
  3. resistivity: 0.2–2000 ohm·m, **log scale**, all resistivity curves,
     legend by group;
  4. density 1.95–2.95 g/cc **and** neutron 0.45 → −0.15 v/v (**reversed**) on
     the same track (limestone-compatible scale); legend.
- All tracks share the same depth range; depth increases **downward**.
- Values outside a track's scale are clipped at the edge.
- NaN values make **gaps** in the line (do not connect across them).
- A track with no curve shows the text "No data".
- Title: well name (from `WELL`), source file name, depth unit
  ("m" or "unit unknown").
- [verify] how ScottPlot 5 handles: several aligned plots in one image, a log
  axis (often done by plotting log10 values with custom tick labels), a reversed
  axis, two different x-scales in one track, and NaN gaps. Choose the simplest
  approach that gives the result above and explain it to the developer.
- [default] Optional performance step if plotting is slow: reduce points to at
  most a min/max pair per pixel row. Measure first.

### D18 — Outputs and run mode
- Sequential processing (one well after another, no parallelism).
- Plot file name = LAS file name with `.png` (`WELL-A.las` → `WELL-A.png`).
  The well name goes in the image title.
- Reports: `report.md` (for people) and `report.csv` (for Excel filtering).

---

## 5. Program behaviour [default]

### Command line

```
<cli>.exe <inputFolder> <outputFolder>
```

- Reads `*.las` (case-insensitive) in `inputFolder` (not recursive).
- Writes `outputFolder/plots/*.png`, `outputFolder/report.md`,
  `outputFolder/report.csv`. Creates folders if needed.
- Prints one progress line per file, e.g.
  `[ 12/50] WELL-A.las  OK  (3 warnings)` and a summary at the end
  (files OK / with warnings / rejected / not plotted, total time).
- Exit code: `0` when the run completed (even if some files were rejected),
  `2` for invalid arguments, `1` for an unexpected failure of the whole run.

### Never crash on a file

Normal problems are reported through return values and report entries, **not**
exceptions. Around the processing of each single file there is one last-resort
`try/catch` that turns any unexpected exception into a Fatal report entry
("internal error: …") and continues with the next file.

### Reading details

- Read line by line with a `StreamReader` (never the whole file at once).
- Encoding: UTF-8 with BOM detection; invalid bytes must not throw.
  Tabs and other control characters count as spaces.
- `~` as the first non-space character starts a section; the letter after it
  (case-insensitive) is the section type. `#` as the first non-space character
  is a comment. Blank lines are ignored.
- Sections may come in any order except `~A`, which ends the header.
- Missing `WRAP` → assume `NO` and warn.
- LAS 3.0 (`VERS` 3.x) is not supported beyond what the 2.0 rules can read; warn.
- Numbers: `double.TryParse` with `NumberStyles.Float` and
  `CultureInfo.InvariantCulture`. No exceptions for control flow.
- Avoid LINQ and needless string allocations in the data-reading loop.
- Data arrays grow while reading; when `STRT/STOP/STEP` are valid, use them only
  as an **initial capacity estimate** (clamped to a sane maximum). Trim to the
  exact length at the end.

### Conditioning order

1. NULL → NaN (already done while reading, D3; counts reported).
2. Skip rows without depth (D14).
3. Depth unit → metres, including STRT/STOP/STEP (D13).
4. Stable sort by depth (D14).
5. Remove duplicate depths, keep first in file order (D14).
6. Sampling and header consistency checks (D1, D14).
7. Identify curves with the catalog (D15).
8. Unit conversion for the four families (D15).
9. Physical checks (D16).

Each step is a small class with one method that changes the well and returns
its report entries, so steps can be added or reordered easily.

### Report content

`report.md`:
- run summary (counts, total time, time per stage — use `Stopwatch`);
- table: file, well name, status, rows, curves, curves selected per family,
  warnings, plot file;
- one section per file listing its entries (Fatal first, then Warning, then
  Change).

`report.csv`: one row per entry, columns
`File,Well,Stage,Severity,Section,Line,Curve,Message,RawLine`.
Escape fields properly (RFC 4180 style quoting).

---

## 6. Coding conventions

- C# 10, `net48`, `ImplicitUsings` off, `Nullable` off.
- Simple, readable code over clever code. Small classes, clear names.
- XML doc comments on public types and members; comments explain **why**.
- `CultureInfo.InvariantCulture` for all parsing and formatting of numbers.
- No LAS-parsing libraries.

---

## 7. Tests (minimum)

Small LAS samples written as strings inside the tests.

- Header splitter: normal line; no unit; empty value; value containing dots;
  time value containing colons; no colon (skipped); no dot (skipped).
- Reader: unwrapped; wrapped; leftover wrapped values; row with too few / too
  many values (skipped, other depths unchanged); non-numeric token → NaN;
  NULL conversion; missing NULL → -999.25 + warning; duplicate `WELL` kept and
  reported; `~C` count ≠ data columns → rejected with the cause line reported;
  no `~A` → rejected; no usable rows → rejected; TIME index → loaded, not
  plotted.
- Conditioning: feet → metres; unknown depth unit not converted; decreasing
  depth reversed; out-of-order depth sorted stably; duplicates keep first;
  rows without depth skipped.
- Catalog: `DRHO` never selected as density; unit mismatch not selected;
  priority order respected; all resistivity curves grouped; kg/m3 → g/cc;
  % → v/v; impossible values → NaN; unusual values → warning only.

---

## 8. Milestones

Stop after each one (see CLAUDE.md).

| # | Milestone | Done when |
|---|---|---|
| M0 | Inspect the solution, confirm `dotnet build` works, explain the layout to the developer | developer understands the starting point |
| M1 | Model: `Curve`, `Well`, report entry, enums (keep `Header`) | builds |
| M2 | Test project + header-line splitter + its tests | tests pass |
| M3 | LAS reader (sections, header, data, wrap, NULL, rejection rules) + tests | tests pass |
| M4 | CLI: arguments, file loop, progress, summary, last-resort catch; run on the real files | runs on all 50 files without crashing |
| M5 | Conditioning steps 2–6 + tests | tests pass |
| M6 | Curve catalog, unit conversion, physical checks (steps 7–9) + tests | tests pass |
| M7 | Plot renderer with ScottPlot | PNGs look right for several wells |
| M8 | `report.md` and `report.csv`, timings | reports readable and correct |
| M9 | Full run on the real files; README draft (build/run, design, decisions, limitations); leave "How I used AI" empty for the developer | ready to submit |

---

## 9. Known limitations (for the README)

- Processes wells sequentially (no parallelism).
- One depth index per file; no merging of several files of the same well.
- Only the four target curve families get unit conversion.
- Alias table is compiled into the code.
- Some real resistivity curves are not in the catalog because their meaning could
  not be verified (e.g. HRLA `RLA1`–`RLA5`, HRLT `RT_HRLT`, `RXO_HRLT`); they are
  not plotted.
- No resampling.
- LAS 3.0 not fully supported.
- Non-depth-indexed files are loaded but not plotted.
- Depends on ScottPlot / SkiaSharp for rendering.
