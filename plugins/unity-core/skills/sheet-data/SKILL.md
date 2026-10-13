---
name: sheet-data
description: Read, edit and format a Google Sheet that the Google Sheet importer package (`com.feeder.editortools`, its GoogleSheet Importer window) turns into Raw*Data ScriptableObjects - the "Data Technical" sheets. Covers exactly what the importer reads and ignores, editing rows/columns/values without breaking an import, and styling a sheet for humans (bold header row, auto-fit widths, colored chip dropdowns for every enum, checkboxes, hex color swatches, header notes, _Enums and _Guide tabs, room for extra columns) with a script that proves the importer still reads the same data. Use for "sửa data technical", "thêm cột/dòng vào sheet data", "format lại sheet", "làm dropdown enum trên sheet", "ghi chú cho cột", "sheet import bị lỗi/sai dòng", or any Google Sheets change to a sheet whose row 2 holds n_/s_/f_/d_/b_ headers.
---

# Data sheets

A data tab is a Google Sheet tab the importer turns into `Raw<Name>` + `Raw<Name>Data`. This
skill keeps two promises at once: the sheet is pleasant for a designer on Google Sheets, and the
importer reads byte-for-byte the same data before and after any formatting change.

Read the `google-workspace` skill (Sheets reference) before the first connector call. Which
spreadsheet is the live import sheet is project knowledge: check the project's CLAUDE.md and memory
before touching one, and never format or edit a sheet the project marks as legacy unless asked.

## What the importer reads

Source: `Library/PackageCache/com.feeder.editortools@*/Editor/10.GoogleSheetImporter/`
(`FeederGoogleSheetController.cs`, `FeederSheetInfoView.BuildCells`, `FeederDataAssetGenerator`).
It downloads `export?format=xlsx` (so the sheet must be link-shared as Viewer) and reads only cell
values plus font strikethrough.

| Part | Rule |
|---|---|
| Row 1 | The first non-empty cell (A1) is the class name, `RawX` or `Namespace.RawX`. Every other row-1 cell is free description text. If A1 is empty, the next filled row-1 cell becomes the class name. |
| Row 2 | Field headers `prefix_Name`, enum `s_Name:Namespace.Enum`, component `pref_Name:Type`. Prefixes: `n` int, `l` long, `f` float, `d` double, `b` bool, `s` string, `sp` Sprite, `pref` GameObject, `li.x` List, `p<a,b>` pair. |
| Skipped column | Row-2 cell starting with `/` (no leading space): the whole column is ignored, by the asset generator and by Update Enum. |
| Width | Only columns up to the last non-empty row-2 cell exist. An empty row-2 cell between fields is a "header rỗng" warning; a header without `_` is skipped. |
| Rows 3+ | Every row from 3 to the last row with any content is one element. |
| Empty cell | Inherits the value from the row above in that column. An empty cell with nothing above fails Generate Asset. |
| Blank row inside the table | Becomes a full copy of the row above. |
| Row with only notes / text outside the fields | Also becomes a copy of the row above. |
| Strikethrough | A row whose every non-empty cell is struck through is skipped (rows 3+). |
| Ignored | Fill, text color, font size, bold, widths, heights, frozen panes, notes, comments, data validation, conditional formatting, number format (the raw stored value is read). |
| Enums | Update Enum collects every distinct value of each `s_X:Enum` column and adds missing members to the C# enum. Values must be C# identifiers. |
| Generate All Assets | Only touches tabs that already have `Raw<TabName>.asset`, so helper tabs like `_Enums` are skipped. |

## The script

`${CLAUDE_SKILL_DIR}/scripts/sheet_data.py` (Python 3 stdlib only) mirrors the importer's parser.

| Command | Use |
|---|---|
| `snapshot --id ID --out a.json` | What the importer sees per tab (class, fields, rows after strike removal and fill-down). |
| `diff a.json b.json` | Exit 1 and list differences if the importer would read anything different. |
| `lint --id ID --cs-root Assets` | Blank rows, note-only rows, text right of the last header, empty headers, unknown prefixes, enum values that are not identifiers or not yet in C#, bool columns holding text. |
| `style --id ID --meta meta.json --spec spec.json --out-dir out` | Writes `update_spreadsheet` batches. Add `--only TabA,_Enums` to emit some tabs, `--types setDataValidation,...` to keep some request types, `--widths-only` to re-fit widths, `--max-kb 24` to keep each file printable. |
| `fetch --id ID --out x.xlsx` | Save the export once and pass `--xlsx x.xlsx` to the other commands. |

On Windows Git Bash, Python does not see `/tmp`; use a project-relative path or `$(cygpath -w /tmp/x)`.
`cat` truncates output past about 30 KB, which is why batches default to small files.

## Every change, in this order

1. `snapshot` before. `lint` too: fix real findings first or report them.
2. Make the change (sections below).
3. `snapshot` after and `diff`. A formatting-only change must print `identical`. A data change must
   differ only where intended: read the diff lines, do not just check the exit code.
4. Report what changed, link the spreadsheet, and say the link is unchanged.

## Editing data

- **Values:** `update_values` parses like the UI. Send numbers as numbers and enum names as plain
  strings. Prefix text that looks like a number or date with `'`. Booleans go as `true`/`false`.
- **New row:** insert inside the table (`insert_dimension`, `inheritFromBefore: true`) so the row
  keeps the column's dropdown, checkbox and formatting, then write the values. Rows below the data
  carry no dropdown on purpose; a row appended there gets one when `style` runs again.
- **New field column:** insert the column right of the last field, write `prefix_Name` in row 2 and
  its description as the row-2 note (and in the spec), fill every data row (an empty first data cell
  fails the import).
  Then the project must run Generate Script and Generate Asset in Unity; say so.
- **Notes per row:** a column whose row-2 header starts with `/`. Write only on rows that hold data.
- **Never:** write below the data, leave a blank row inside a table, rename A1 or a row-2 header
  (that renames the C# class or field), or reorder rows of a lookup table without checking how code
  reads it (some tables are sorted thresholds).

## Formatting a sheet

1. Read tab metadata: `get_spreadsheet` with `fields: ["sheets.properties", "sheets.conditionalFormats.ranges", "sheets.conditionalFormats.booleanRule.condition.type"]`.
   A large result is saved to a file by the app; pass that file as `--meta`. A small one, write to a
   file yourself.
2. Write or update the project's spec (below), then run `style`.
3. Send each `batch_NN.json` in order, one `update_spreadsheet` call each, exactly as written. The
   first batch adds `_Enums` / `_Guide` and grows the grids; later batches reference them.
4. Verify: `snapshot` + `diff`, and a grid read of one tab (`dataValidation`, `note`, widths).

What `style` does, all invisible to the importer:

- Grows every grid to at least 26 columns and adds empty rows, so new columns need no "add column".
- Row 1 keeps only the class name in A1. Any other row-1 text is moved into the note of its row-2
  header (unless the spec already has a note for that column) and cleared, so descriptions are not
  shown twice. Fold anything worth keeping into the spec first: a re-run writes the spec note.
- Row 2: bold 13 pt on a blue fill, 30 px tall, frozen.
- Column widths from the widest of header (13 pt bold) and displayed values; enum columns get room
  for the chip.
- Dropdowns and checkboxes cover the data rows only and are cleared below them, so the empty rows
  under a table show no empty chips. Every `s_X:Enum` column gets a chip dropdown (`customUiMode: CHIP`, `strict: false`, so a new
  value is allowed but flagged). Enums with a palette (`Rarity`, `Variant`, `BuyType` by default)
  use a list with a colored chip per value. Others point at their column in `_Enums`, or at the
  owner tab's key column when the spec maps one (`enum_sources`). Foreign keys that are numbers get
  a dropdown through `refs`.
- `b_` columns holding real booleans get checkboxes (a checkbox clicked in an empty row would also
  create a phantom element).
- `s_` columns whose every value is `#rrggbb` are filled with their own color (static: re-run after
  editing the codes).
- Notes from the spec on row-2 headers (`_table` goes on A1).
- `_Enums`: one column per enum, C# members in declaration order (minus `None`) plus values only the
  sheet has (red, with a note to run Update Enum) plus values already typed into `_Enums`. Row 2
  lists the tabs using it. `_Guide`: the rules above in Vietnamese for designers.
- Removes `TEXT_EQ` conditional formats left on enum columns by older runs.

Re-running `style` is safe: it overwrites its own formatting, validation, notes and helper tabs and
leaves user fills elsewhere alone. It does overwrite notes it owns, so put note edits in the spec.

### Hidden API fields

The Sheets API docs omit two fields the API accepts and returns: `DataValidationRule.customUiMode`
(`CHIP` | `ARROW`) and `ConditionValue.dropdownStyle` (`backgroundColorStyle`,
`foregroundColorStyle`, only for `ONE_OF_LIST`). A `ONE_OF_RANGE` dropdown cannot color its chips.
To copy a chip palette someone set by hand, read a cell with
`fields: ["sheets.data.rowData.values.dataValidation"]`.

## Spec file

One JSON per spreadsheet, kept in the project (for example `Docs/GoogleSheets/<Game>.sheet-spec.json`).
Fields are matched by name without prefix (`BossMaxSpeed`) or by the full header.

```json
{
  "cs_root": "Assets",
  "enum_sources": { "StealAnEgg.EggType": "RaPEggData!A" },
  "refs": { "RaPNestData": { "ZoneID": "RaPZoneData!A" } },
  "enum_colors": { "Rarity": { "Common": ["#e8eaed", "#3c4043"] } },
  "notes": { "RaPSpeedData": { "_table": "Bảng tra...", "FakeSpeed": "Mốc tăng dần." } },
  "extra_columns": 10, "min_columns": 26, "extra_rows": 30
}
```

Write notes for what a designer cannot guess from the name: units, fake vs real values, weights that
are not percentages, which tab a key points to, rows that must stay sorted, columns used only for one
`BuyType`. Write them in the team's language, short, from the code that reads the field; never
invent behavior you did not read.

## Failures

| Symptom | Cause | Fix |
|---|---|---|
| Unity shows duplicated last element | Text below the data, or a blank row | `lint`, clear that row |
| Generate Asset "Ô trống ... không có giá trị phía trên" | Empty first data cell in a field column | Fill it |
| Script shows "header rỗng" or skips a column | Empty or `_`-less row-2 cell between fields | Name it, or start it with `/` |
| Dropdown shows a red corner | Value not in the list | Fix the typo, or add the value to `_Enums` and run Update Enum |
| `Unable to parse range` in a dropdown | `_Enums` missing | Send the setup batch first |
| `diff` reports changes after formatting | A request wrote a value | Undo it from the before-snapshot, find the request |
