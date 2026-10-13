#!/usr/bin/env python3
"""Read an import Google Sheet exactly the way the importer does, lint it,
and build the batchUpdate requests that make it readable on Google Sheets without
changing a single value the importer sees.

Commands
  fetch     download the sheet as .xlsx (the same export the importer downloads)
  snapshot  write what the importer sees (class, fields, rows after fill-down) as JSON
  diff      compare two snapshots; exit 1 if the importer would read anything different
  lint      report rows/columns the importer will misread
  style     write update_spreadsheet request batches into a folder

Every command that reads the sheet takes --id SPREADSHEET_ID (downloads it, the sheet
must be shared "anyone with the link can view", same as the importer) or --xlsx FILE.
"""
import argparse
import io
import json
import os
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
REL = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PKG = "{http://schemas.openxmlformats.org/package/2006/relationships}"

SKIP_PREFIX = "/"
HELPER_TAB_PREFIX = "_"
ENUMS_TAB = "_Enums"
GUIDE_TAB = "_Guide"
KNOWN_PREFIXES = {"n", "by", "sh", "us", "ui", "l", "ul", "d", "b", "s", "f", "sp", "spine", "skeDat", "pref"}
IDENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
HEX = re.compile(r"^#[0-9a-fA-F]{6}$")

DEFAULT_SPEC = {
    "extra_columns": 10,
    "min_columns": 26,
    "extra_rows": 30,
    "enum_sources": {},
    "enum_colors": {
        "Rarity": {
            "Common": ["#e8eaed", "#3c4043"], "Rare": ["#c2e7ff", "#0b57d0"],
            "Epic": ["#e8d5f5", "#6a1b9a"], "Legend": ["#ffe8a3", "#7a5200"],
            "Mythic": ["#ffd0cc", "#b3261e"], "Godly": ["#ffd8b5", "#a14500"],
            "Secret": ["#3c4043", "#ffffff"], "Divine": ["#d3f2c4", "#1e6b1e"],
            "Hacked": ["#1f1f1f", "#39ff14"], "Celestial": ["#c9f5f5", "#006a6a"],
        },
        "Variant": {
            "Normal": ["#e8eaed", "#3c4043"], "Golden": ["#ffe8a3", "#7a5200"],
            "Diamond": ["#c2e7ff", "#0b57d0"], "Rainbow": ["#a8f0f0", "#00585c"],
        },
        "BuyType": {
            "Free": ["#d3f2c4", "#1e6b1e"], "Video": ["#ffd0cc", "#b3261e"],
            "Cash": ["#ffe8a3", "#7a5200"], "IAP": ["#e8d5f5", "#6a1b9a"],
        },
    },
    "refs": {},
    "notes": {},
}

STYLE = {
    "row1_bg": "#f3f3f3", "row1_fg": "#5f6368",
    "class_bg": "#d9d9d9", "class_fg": "#202124",
    "header_bg": "#c9daf8", "header_fg": "#1c4587", "header_size": 13,
    "skip_bg": "#fce5cd", "skip_fg": "#7f4f00",
    "missing_bg": "#f4c7c3",
}

GUIDE_LINES = [
    "Cách đọc và sửa sheet data này",
    "Tab này và tab _Enums do skill sheet-data sinh ra. Tool import không đọc 2 tab này (không có Raw asset).",
    "",
    "Tool import đọc gì",
    "Dòng 1: chỉ có tên class ở A1, không sửa. Mô tả từng cột nằm trong note của ô tên field ở dòng 2.",
    "Dòng 2: tên field dạng prefix_Ten (n_ int, f_ float, d_ double, l_ long, b_ bool, s_ string, s_Ten:Namespace.Enum là enum).",
    "Từ dòng 3: mỗi dòng có chữ là 1 phần tử data.",
    "Ô trống ở cột field = lấy giá trị ô phía trên. Dòng trống giữa bảng cũng thành 1 bản sao của dòng trên.",
    "Gạch ngang (strikethrough) toàn bộ dòng = tool bỏ qua dòng đó.",
    "",
    "Tool import bỏ qua gì",
    "Màu nền, màu chữ, cỡ chữ, in đậm, độ rộng cột, dropdown, checkbox, ghi chú (note) và comment.",
    "Cột có tên ở dòng 2 bắt đầu bằng / (ví dụ /Ghi chú): cả cột bị bỏ qua. Dùng cột này để ghi chú theo từng dòng.",
    "",
    "Quy tắc khi sửa",
    "Thêm cột field mới: gõ prefix_Ten vào dòng 2, rồi Generate Script và Generate Asset trong Unity.",
    "Thêm cột ghi chú: gõ /Ten vào dòng 2. Chỉ ghi ở các dòng đang có data.",
    "Thêm dòng data: chuột phải vào 1 dòng trong bảng > Chèn 1 hàng, để dòng mới có sẵn dropdown và checkbox. Dòng trống dưới bảng không có dropdown.",
    "Không gõ gì vào dòng nằm dưới data hoặc dòng trống giữa bảng: tool sẽ đọc thành thêm 1 phần tử copy dòng trên.",
    "Cột enum có dropdown. Giá trị lấy từ tab _Enums hoặc cột khoá của tab chủ (xem dòng 2 của _Enums).",
    "Rarity, Variant, BuyType là danh sách cố định có màu; thêm giá trị mới thì nhờ chạy lại skill để cập nhật dropdown.",
    "Giá trị enum mới: thêm vào cột tương ứng ở _Enums (hoặc thêm dòng ở tab chủ), dùng trong data, rồi bấm Update Enum trong Unity.",
    "Ô có tam giác đỏ ở góc là giá trị không có trong danh sách dropdown. Tool vẫn đọc nhưng enum có thể sai tên.",
    "Rê chuột vào ô tên field (dòng 2) có tam giác đen ở góc để đọc ghi chú.",
]


# ---------------------------------------------------------------- xlsx reading

def download(spreadsheet_id):
    url = f"https://docs.google.com/spreadsheets/d/{spreadsheet_id}/export?format=xlsx"
    req = urllib.request.Request(url, headers={"User-Agent": "GoogleSheetImporter"})
    data = urllib.request.urlopen(req, timeout=120).read()
    if data[:2] != b"PK":
        sys.exit("Sheet is not shared 'anyone with the link can view' (the export returned HTML).")
    return data


def load_bytes(args):
    if getattr(args, "xlsx", None):
        with open(args.xlsx, "rb") as f:
            return f.read()
    if getattr(args, "id", None):
        return download(args.id)
    sys.exit("Pass --id SPREADSHEET_ID or --xlsx FILE")


def col_index(ref):
    idx = 0
    for ch in ref:
        if ch.isalpha():
            idx = idx * 26 + (ord(ch.upper()) - 64)
        else:
            break
    return idx - 1


def col_letter(i):
    s = ""
    i += 1
    while i:
        i, r = divmod(i - 1, 26)
        s = chr(65 + r) + s
    return s


def concat_text(el):
    return "".join(t.text or "" for t in el.iter(MAIN + "t"))


def parse_workbook(data):
    """Returns [{title, cells: {row: {col: (value, kind, struck)}}}] in tab order.

    Mirrors FeederGoogleSheetController: same value reading, same strikethrough rule."""
    z = zipfile.ZipFile(io.BytesIO(data))
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        root = ET.fromstring(z.read("xl/sharedStrings.xml"))
        shared = [concat_text(si) for si in root.findall(MAIN + "si")]

    strike_by_style = []
    if "xl/styles.xml" in z.namelist():
        root = ET.fromstring(z.read("xl/styles.xml"))
        font_strike = []
        fonts = root.find(MAIN + "fonts")
        for font in (fonts.findall(MAIN + "font") if fonts is not None else []):
            s = font.find(MAIN + "strike")
            font_strike.append(s is not None and s.get("val") not in ("0", "false"))
        xfs = root.find(MAIN + "cellXfs")
        for xf in (xfs.findall(MAIN + "xf") if xfs is not None else []):
            fid = int(xf.get("fontId", "0"))
            strike_by_style.append(0 <= fid < len(font_strike) and font_strike[fid])

    rels = {}
    root = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
    for rel in root.findall(PKG + "Relationship"):
        target = rel.get("Target").replace("\\", "/")
        rels[rel.get("Id")] = target.lstrip("/") if target.startswith("/") else "xl/" + target

    tabs = []
    wb = ET.fromstring(z.read("xl/workbook.xml"))
    for sheet in wb.find(MAIN + "sheets").findall(MAIN + "sheet"):
        title = sheet.get("name")
        path = rels.get(sheet.get(REL + "id"))
        cells = {}
        if path and path in z.namelist():
            sroot = ET.fromstring(z.read(path))
            sd = sroot.find(MAIN + "sheetData")
            for row in (sd.findall(MAIN + "row") if sd is not None else []):
                r = int(row.get("r")) - 1
                for c in row.findall(MAIN + "c"):
                    ci = col_index(c.get("r", ""))
                    if ci < 0:
                        continue
                    t = c.get("t")
                    v = c.find(MAIN + "v")
                    if t == "s":
                        val = shared[int(v.text)] if v is not None and v.text and v.text.isdigit() and int(v.text) < len(shared) else ""
                        kind = "s"
                    elif t == "inlineStr":
                        el = c.find(MAIN + "is")
                        val = concat_text(el) if el is not None else ""
                        kind = "s"
                    elif t == "b":
                        val = "TRUE" if v is not None and v.text == "1" else "FALSE"
                        kind = "b"
                    else:
                        val = v.text if v is not None and v.text is not None else ""
                        kind = "n" if t is None else t
                    if val == "":
                        continue
                    s = int(c.get("s", "0"))
                    struck = 0 <= s < len(strike_by_style) and strike_by_style[s]
                    cells.setdefault(r, {})[ci] = (val, kind, struck)
        tabs.append({"title": title, "cells": cells})
    return tabs


def tab_rows(tab):
    """The importer's List<List<string>>: rows up to the last row with content, trailing blanks trimmed."""
    cells = tab["cells"]
    if not cells:
        return [], set()
    last = max(cells)
    rows, struck = [], set()
    for r in range(last + 1):
        rc = cells.get(r, {})
        if not rc:
            rows.append([])
            continue
        width = max(rc) + 1
        rows.append([rc.get(c, ("", "", False))[0] for c in range(width)])
        if all(x[2] for x in rc.values()):
            struck.add(r)
    return rows, struck


# ---------------------------------------------------------------- importer model

def parse_header(raw):
    """(prefix, field_name, type_token) or None when the importer cannot make a field of it."""
    if not raw or not raw.strip() or "_" not in raw:
        return None
    prefix = raw[:raw.index("_")].strip()
    rest = raw[raw.index("_") + 1:]
    token = None
    if ":" in rest:
        rest, token = rest.split(":", 1)
        token = token.strip()
    return prefix, rest.strip(), token


def is_skip_header(raw):
    return bool(raw) and raw.strip() != "" and raw[0] == SKIP_PREFIX


def enum_header(raw):
    """(field_name, enum_token) for an s_Name:Enum header, else None."""
    h = None if is_skip_header(raw) else parse_header(raw)
    return (h[1], h[2]) if h and h[0] == "s" and h[2] else None


def is_data_tab(tab):
    rows, _ = tab_rows(tab)
    return (not tab["title"].startswith(HELPER_TAB_PREFIX)) and len(rows) >= 2 and class_name(rows) != ""


def class_name(rows):
    for v in rows[0] if rows else []:
        if v.strip():
            return v
    return ""


def importer_view(tab):
    """What FeederSheetInfoView.BuildCells + FeederDataAssetGenerator would produce."""
    rows, struck = tab_rows(tab)
    if len(rows) < 2:
        return None
    field_row = rows[1]
    cols = [c for c in range(len(field_row)) if not is_skip_header(field_row[c])]
    fields = [field_row[c] for c in cols]
    data, errors = [], []
    last_seen = [None] * len(cols)
    for r in range(2, len(rows)):
        if r in struck:
            continue
        out = []
        for i, c in enumerate(cols):
            v = rows[r][c] if c < len(rows[r]) else ""
            if v.strip() == "":
                if last_seen[i] is None:
                    if parse_header(fields[i]) is not None:
                        errors.append(f"row {r + 1} col {col_letter(c)} '{fields[i]}': empty with nothing above to inherit, Generate Asset fails")
                    v = ""
                else:
                    v = last_seen[i]
            else:
                last_seen[i] = v
            out.append(v)
        data.append({"row": r + 1, "values": out})
    return {"class": class_name(rows), "fields": fields, "columns": [col_letter(c) for c in cols],
            "rows": data, "errors": errors}


# ---------------------------------------------------------------- C# enums

def scan_enums(root):
    """{full_name: [members]} from every .cs file under root."""
    enums = {}
    if not root:
        return enums
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in ("Library", "Temp", "obj", ".git")]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            try:
                with open(os.path.join(dirpath, fn), encoding="utf-8-sig", errors="replace") as f:
                    text = f.read()
            except OSError:
                continue
            if "enum" not in text:
                continue
            text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
            text = re.sub(r"//[^\n]*", "", text)
            spaces = [(m.start(), m.group(1)) for m in re.finditer(r"\bnamespace\s+([\w.]+)", text)]
            for m in re.finditer(r"\benum\s+(\w+)\s*(?::\s*\w+)?\s*\{([^}]*)\}", text):
                ns = ""
                for pos, name in spaces:
                    if pos < m.start():
                        ns = name
                members = []
                for part in m.group(2).split(","):
                    mm = re.match(r"\s*(?:\[[^\]]*\]\s*)*@?(\w+)", part)
                    if mm:
                        members.append(mm.group(1))
                full = f"{ns}.{m.group(1)}" if ns else m.group(1)
                enums.setdefault(full, members)
    return enums


def short_enum(token):
    return token.split(".")[-1]


# ---------------------------------------------------------------- lint / snapshot / diff

def cmd_fetch(args):
    data = download(args.id)
    with open(args.out, "wb") as f:
        f.write(data)
    print(f"saved {len(data)} bytes to {args.out}")


def cmd_snapshot(args):
    tabs = parse_workbook(load_bytes(args))
    snap = {}
    for tab in tabs:
        if not is_data_tab(tab):
            continue
        view = importer_view(tab)
        snap[tab["title"]] = {"class": view["class"], "fields": view["fields"],
                              "rows": [r["values"] for r in view["rows"]]}
    text = json.dumps(snap, ensure_ascii=False, indent=0)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(text)
        print(f"{len(snap)} tabs -> {args.out}")
    else:
        print(text)


def cmd_diff(args):
    with open(args.before, encoding="utf-8") as f:
        a = json.load(f)
    with open(args.after, encoding="utf-8") as f:
        b = json.load(f)
    problems = []
    for name in sorted(set(a) | set(b)):
        if name not in a:
            problems.append(f"{name}: new data tab")
            continue
        if name not in b:
            problems.append(f"{name}: data tab gone")
            continue
        for key in ("class", "fields"):
            if a[name][key] != b[name][key]:
                problems.append(f"{name}: {key} changed {a[name][key]} -> {b[name][key]}")
        ra, rb = a[name]["rows"], b[name]["rows"]
        if len(ra) != len(rb):
            problems.append(f"{name}: row count {len(ra)} -> {len(rb)}")
        for i, (x, y) in enumerate(zip(ra, rb)):
            if x != y:
                problems.append(f"{name}: data row {i + 1} {x} -> {y}")
                break
    if problems:
        print("IMPORTER WOULD READ DIFFERENT DATA:")
        print("\n".join(problems))
        sys.exit(1)
    print(f"identical: {len(a)} tabs, importer reads the same data")


def lint_tab(tab, enums):
    out = []
    rows, struck = tab_rows(tab)
    title = tab["title"]
    if len(rows) < 2:
        return [f"{title}: fewer than 2 rows, not a data tab"]
    if rows[0] and rows[0][0].strip() == "" and class_name(rows):
        out.append(f"{title}: A1 is empty, the importer takes '{class_name(rows)}' as the class name")
    field_row = rows[1]
    nfield = len(field_row)
    real_cols = []
    for c, raw in enumerate(field_row):
        if is_skip_header(raw):
            continue
        h = parse_header(raw)
        if raw.strip() == "":
            out.append(f"{title}!{col_letter(c)}2: empty header between fields, column is skipped with a warning")
        elif h is None:
            out.append(f"{title}!{col_letter(c)}2 '{raw}': no '_', column is skipped")
        else:
            p = h[0]
            if p not in KNOWN_PREFIXES and not p.startswith("p") and not p.startswith("li."):
                out.append(f"{title}!{col_letter(c)}2 '{raw}': unknown prefix '{p}'")
            real_cols.append(c)
    for r in range(2, len(rows)):
        if r in struck:
            continue
        row = rows[r]
        if not row:
            out.append(f"{title}!{r + 1}: blank row inside the table, imported as a copy of the row above")
            continue
        if not any(c < len(row) and row[c].strip() for c in real_cols):
            out.append(f"{title}!{r + 1}: only notes / cells outside the fields, imported as a copy of the row above")
        if len(row) > nfield and any(v.strip() for v in row[nfield:]):
            out.append(f"{title}!{r + 1}: text right of the last header (col {col_letter(nfield)}+) - give that column a /Name header")
    view = importer_view(tab)
    out += [f"{title}: {e}" for e in view["errors"]]
    for i, c in enumerate(real_cols):
        h = parse_header(field_row[c])
        values = [rows[r][c] for r in range(2, len(rows)) if r not in struck and c < len(rows[r]) and rows[r][c].strip()]
        if h[0] == "s" and h[2]:
            members = enums.get(h[2])
            bad = sorted({v for v in values if not IDENT.match(v.strip())})
            if bad:
                out.append(f"{title}!{col_letter(c)} enum {h[2]}: not valid C# names {bad[:8]}")
            if members is not None:
                missing = sorted({v.strip() for v in values} - set(members))
                if missing:
                    out.append(f"{title}!{col_letter(c)} enum {h[2]}: {len(missing)} values not in C# yet (Update Enum adds them): {missing[:8]}")
            elif enums:
                out.append(f"{title}!{col_letter(c)} enum {h[2]}: no such enum in the C# scan")
        if h[0] == "b":
            bad = sorted({v for v in values if v.upper() not in ("TRUE", "FALSE")})
            if bad:
                out.append(f"{title}!{col_letter(c)} bool: values {bad[:5]} are not TRUE/FALSE")
    return out


def cmd_lint(args):
    tabs = parse_workbook(load_bytes(args))
    enums = scan_enums(args.cs_root)
    total = 0
    for tab in tabs:
        if tab["title"].startswith(HELPER_TAB_PREFIX):
            continue
        rows, struck = tab_rows(tab)
        msgs = lint_tab(tab, enums)
        view = importer_view(tab)
        n = len(view["rows"]) if view else 0
        print(f"[{tab['title']}] class={class_name(rows)!r} fields={len(view['fields']) if view else 0} rows={n} struck={len(struck)}")
        for m in msgs:
            print("  - " + m)
        total += len(msgs)
    print(f"{total} findings")


# ---------------------------------------------------------------- style

def rgb(hex_color):
    h = hex_color.lstrip("#")
    return {"red": round(int(h[0:2], 16) / 255, 4), "green": round(int(h[2:4], 16) / 255, 4), "blue": round(int(h[4:6], 16) / 255, 4)}


def contrast(hex_color):
    c = rgb(hex_color)
    lum = 0.299 * c["red"] + 0.587 * c["green"] + 0.114 * c["blue"]
    return "#000000" if lum > 0.55 else "#ffffff"


def text_px(s, pt, bold=False):
    em = pt * 96 / 72
    w = 0.0
    for ch in s:
        if ch in "il.,:;'|!`":
            f = 0.26
        elif ch in "jftr()[]{}- ":
            f = 0.33
        elif ch in "mwMW@%":
            f = 0.86
        elif ch.isupper():
            f = 0.68
        elif ch.isdigit() or ch in "_#$+=<>":
            f = 0.56
        else:
            f = 0.54
        w += f * em
    return w * (1.08 if bold else 1.0)


def shown(value, kind):
    """Roughly what Sheets displays for a cell in the default number format."""
    if kind != "n":
        return value
    try:
        x = float(value)
    except ValueError:
        return value
    if x.is_integer() and abs(x) < 1e15:
        return str(int(x))
    if abs(x) >= 1e15:
        return f"{x:.5G}"
    return f"{x:.10g}"


def dropdown(sheet_id, col, values, kind, end_row):
    """Chip dropdown on the data rows of one column. ONE_OF_LIST values may carry dropdownStyle (chip colors),
    an API field the public docs leave out; ONE_OF_RANGE cannot color its chips."""
    return {"setDataValidation": {"range": grid(sheet_id, 2, end_row, col, col + 1), "rule": {
        "condition": {"type": kind, "values": values}, "showCustomUi": True, "customUiMode": "CHIP", "strict": False}}}


def clear_validation(sheet_id, col, start_row):
    """Empty rows below the data must not show chips or checkboxes."""
    return {"setDataValidation": {"range": grid(sheet_id, start_row, None, col, col + 1)}}


def grid(sheet_id, r0=None, r1=None, c0=None, c1=None):
    g = {"sheetId": sheet_id}
    if r0 is not None:
        g["startRowIndex"] = r0
    if r1 is not None:
        g["endRowIndex"] = r1
    if c0 is not None:
        g["startColumnIndex"] = c0
    if c1 is not None:
        g["endColumnIndex"] = c1
    return g


def repeat(sheet_id, rng, fmt, fields):
    return {"repeatCell": {"range": grid(sheet_id, *rng), "cell": {"userEnteredFormat": fmt}, "fields": fields}}


def text_cell(value, bg=None, fg=None, bold=None, note=None):
    cell = {"userEnteredValue": {"stringValue": value}}
    fmt = {}
    if bg:
        fmt["backgroundColor"] = rgb(bg)
    tf = {}
    if fg:
        tf["foregroundColor"] = rgb(fg)
    if bold is not None:
        tf["bold"] = bold
    if tf:
        fmt["textFormat"] = tf
    if fmt:
        cell["userEnteredFormat"] = fmt
    if note:
        cell["note"] = note
    return cell


def load_spec(path):
    spec = json.loads(json.dumps(DEFAULT_SPEC))
    if path:
        with open(path, encoding="utf-8") as f:
            user = json.load(f)
        for k, v in user.items():
            if k == "enum_colors":
                for e, palette in v.items():
                    spec["enum_colors"].setdefault(e, {}).update(palette)
            else:
                spec[k] = v
    return spec


def resolve_source(spec_value):
    """'Tab!A' -> (tab, col_index)."""
    tab, col = spec_value.split("!")
    return tab, col_index(col)


def cmd_style(args):
    tabs = parse_workbook(load_bytes(args))
    with open(args.meta, encoding="utf-8") as f:
        meta = json.load(f)
    spec = load_spec(args.spec)
    enums = scan_enums(args.cs_root or spec.get("cs_root"))
    props = {s["properties"]["title"]: s for s in meta["sheets"]}
    os.makedirs(args.out_dir, exist_ok=True)
    for old in os.listdir(args.out_dir):
        if old.endswith(".json"):
            os.remove(os.path.join(args.out_dir, old))

    data_tabs = [t for t in tabs if is_data_tab(t) and t["title"] in props]
    enum_cols = {}          # token -> [(tab, col)]
    enum_values = {}        # token -> ordered values seen in data
    for tab in data_tabs:
        rows, struck = tab_rows(tab)
        for c, raw in enumerate(rows[1]):
            h = None if is_skip_header(raw) else parse_header(raw)
            if h and h[0] == "s" and h[2]:
                enum_cols.setdefault(h[2], []).append((tab["title"], c))
                seen = enum_values.setdefault(h[2], [])
                for r in range(2, len(rows)):
                    if r not in struck and c < len(rows[r]):
                        v = rows[r][c].strip()
                        if v and v not in seen:
                            seen.append(v)

    sources = {}            # token -> (tab, col) owner column, others get a dropdown to it
    for token, src in spec.get("enum_sources", {}).items():
        sources[token] = resolve_source(src)

    existing_enums_tab = next((t for t in tabs if t["title"] == ENUMS_TAB), None)
    old_enum_lists = {}
    if existing_enums_tab:
        rows, _ = tab_rows(existing_enums_tab)
        if rows:
            for c, token in enumerate(rows[0]):
                if token.strip():
                    old_enum_lists[token.strip()] = [rows[r][c].strip() for r in range(2, len(rows)) if c < len(rows[r]) and rows[r][c].strip()]

    enum_tokens = sorted(t for t in enum_cols if t not in sources)
    enum_lists = {}
    for token in enum_tokens:
        members = [m for m in enums.get(token, []) if m != "None"]
        for v in enum_values.get(token, []) + old_enum_lists.get(token, []):
            if v not in members:
                members.append(v)
        enum_lists[token] = members

    used_ids = {s["properties"]["sheetId"] for s in meta["sheets"]}

    def new_id(base):
        i = base
        while i in used_ids:
            i += 1
        used_ids.add(i)
        return i

    setup = []
    enums_id = props[ENUMS_TAB]["properties"]["sheetId"] if ENUMS_TAB in props else None
    guide_id = props[GUIDE_TAB]["properties"]["sheetId"] if GUIDE_TAB in props else None
    if enum_lists and enums_id is None:
        enums_id = new_id(900001)
        setup.append({"addSheet": {"properties": {"sheetId": enums_id, "title": ENUMS_TAB,
                                                  "gridProperties": {"rowCount": max(len(v) for v in enum_lists.values()) + 30,
                                                                     "columnCount": max(26, len(enum_lists) + 5),
                                                                     "frozenRowCount": 2},
                                                  "tabColorStyle": {"rgbColor": rgb("#9aa0a6")}}}})
    if guide_id is None:
        guide_id = new_id(900101)
        setup.append({"addSheet": {"properties": {"sheetId": guide_id, "title": GUIDE_TAB,
                                                  "gridProperties": {"rowCount": len(GUIDE_LINES) + 5, "columnCount": 3},
                                                  "tabColorStyle": {"rgbColor": rgb("#9aa0a6")}}}})

    for tab in data_tabs:
        p = props[tab["title"]]["properties"]
        rows, _ = tab_rows(tab)
        g = p.get("gridProperties", {})
        used_cols = max(len(r) for r in rows)
        want_cols = max(g.get("columnCount", 0), used_cols + spec["extra_columns"], spec["min_columns"])
        want_rows = max(g.get("rowCount", 0), len(rows) + spec["extra_rows"])
        setup.append({"updateSheetProperties": {
            "properties": {"sheetId": p["sheetId"], "gridProperties": {"columnCount": want_cols, "rowCount": want_rows, "frozenRowCount": 2}},
            "fields": "gridProperties.columnCount,gridProperties.rowCount,gridProperties.frozenRowCount"}})
    batches = [("setup", setup)]

    enum_col_index = {token: i for i, token in enumerate(enum_tokens)}

    def dropdown_formula(token):
        if token in sources:
            stab, scol = sources[token]
            return f"='{stab}'!${col_letter(scol)}$3:${col_letter(scol)}"
        letter = col_letter(enum_col_index[token])
        return f"='{ENUMS_TAB}'!${letter}$3:${letter}"

    report = []
    for tab in data_tabs:
        title = tab["title"]
        sid = props[title]["properties"]["sheetId"]
        rows, struck = tab_rows(tab)
        field_row = rows[1]
        last_data = len(rows)
        req = []
        notes = spec.get("notes", {}).get(title, {})
        refs = spec.get("refs", {}).get(title, {})

        req.append(repeat(sid, (0, 1, None, None),
                          {"backgroundColor": rgb(STYLE["row1_bg"]), "wrapStrategy": "OVERFLOW_CELL", "verticalAlignment": "MIDDLE",
                           "textFormat": {"italic": False, "bold": False, "fontSize": 10, "foregroundColor": rgb(STYLE["row1_fg"])}},
                          "userEnteredFormat.backgroundColor,userEnteredFormat.wrapStrategy,userEnteredFormat.verticalAlignment,"
                          "userEnteredFormat.textFormat.italic,userEnteredFormat.textFormat.bold,userEnteredFormat.textFormat.fontSize,"
                          "userEnteredFormat.textFormat.foregroundColor"))
        req.append(repeat(sid, (0, 1, 0, 1),
                          {"backgroundColor": rgb(STYLE["class_bg"]),
                           "textFormat": {"italic": False, "bold": True, "foregroundColor": rgb(STYLE["class_fg"])}},
                          "userEnteredFormat.backgroundColor,userEnteredFormat.textFormat.italic,userEnteredFormat.textFormat.bold,"
                          "userEnteredFormat.textFormat.foregroundColor"))
        req.append(repeat(sid, (1, 2, None, None),
                          {"backgroundColor": rgb(STYLE["header_bg"]), "verticalAlignment": "MIDDLE",
                           "textFormat": {"bold": True, "italic": False, "fontSize": STYLE["header_size"], "foregroundColor": rgb(STYLE["header_fg"])}},
                          "userEnteredFormat.backgroundColor,userEnteredFormat.verticalAlignment,userEnteredFormat.textFormat.bold,"
                          "userEnteredFormat.textFormat.italic,userEnteredFormat.textFormat.fontSize,userEnteredFormat.textFormat.foregroundColor"))

        widths = {}
        for c in range(len(field_row)):
            raw = field_row[c]
            skip = is_skip_header(raw)
            h = None if skip else parse_header(raw)
            if skip:
                req.append(repeat(sid, (1, 2, c, c + 1),
                                  {"backgroundColor": rgb(STYLE["skip_bg"]), "textFormat": {"italic": True, "foregroundColor": rgb(STYLE["skip_fg"])}},
                                  "userEnteredFormat.backgroundColor,userEnteredFormat.textFormat.italic,userEnteredFormat.textFormat.foregroundColor"))
            values = [rows[r][c] for r in range(2, len(rows)) if c < len(rows[r]) and rows[r][c]]
            displayed = [shown(tab["cells"][r][c][0], tab["cells"][r][c][1])
                         for r in range(2, len(rows)) if c in tab["cells"].get(r, {})]
            is_enum = bool(h and h[0] == "s" and h[2])
            ref = refs.get(h[1]) if h else None
            data_px = max([text_px(v, 10) for v in displayed] or [0]) + 18 + (34 if is_enum or ref else 0)
            head_px = text_px(raw, STYLE["header_size"], bold=True) + 24
            widths[c] = int(min(max(data_px, head_px, 64), 520))
            old_desc = rows[0][c].strip() if 0 < c < len(rows[0]) else ""

            if h is None:
                if old_desc:
                    req.append({"updateCells": {"rows": [{"values": [{"note": old_desc}]}], "fields": "note",
                                                "start": {"sheetId": sid, "rowIndex": 1, "columnIndex": c}}})
                continue
            name = h[1]
            note = notes.get(name) or notes.get(raw) or old_desc
            if note:
                req.append({"updateCells": {"rows": [{"values": [{"note": note}]}], "fields": "note",
                                            "start": {"sheetId": sid, "rowIndex": 1, "columnIndex": c}}})
            if is_enum or ref or h[0] == "b":
                req.append(clear_validation(sid, c, last_data))

            if ref:
                rtab, rcol = resolve_source(ref)
                req.append(dropdown(sid, c, [{"userEnteredValue": f"='{rtab}'!${col_letter(rcol)}$3:${col_letter(rcol)}"}], "ONE_OF_RANGE", last_data))
            elif is_enum:
                token = h[2]
                palette = spec["enum_colors"].get(short_enum(token))
                if sources.get(token) == (title, c):
                    pass
                elif palette and token in enum_lists:
                    items = []
                    for v in enum_lists[token]:
                        item = {"userEnteredValue": v}
                        if v in palette:
                            item["dropdownStyle"] = {"backgroundColorStyle": {"rgbColor": rgb(palette[v][0])},
                                                     "foregroundColorStyle": {"rgbColor": rgb(palette[v][1])}}
                        items.append(item)
                    req.append(dropdown(sid, c, items, "ONE_OF_LIST", last_data))
                else:
                    req.append(dropdown(sid, c, [{"userEnteredValue": dropdown_formula(token)}], "ONE_OF_RANGE", last_data))
            elif h[0] == "b":
                kinds = {tab["cells"].get(r, {}).get(c, ("", "", False))[1] for r in range(2, len(rows)) if c < len(rows[r]) and rows[r][c]}
                if kinds == {"b"}:
                    req.append({"setDataValidation": {"range": grid(sid, 2, last_data, c, c + 1),
                                                      "rule": {"condition": {"type": "BOOLEAN"}, "showCustomUi": True, "strict": False}}})
                elif kinds:
                    report.append(f"{title}!{col_letter(c)}: bool column holds text {sorted(kinds)}, no checkbox (retype as TRUE/FALSE first)")
            elif h[0] == "s" and values and all(HEX.match(v.strip()) for v in values):
                cells = []
                for r in range(2, last_data):
                    v = rows[r][c].strip() if c < len(rows[r]) else ""
                    if HEX.match(v):
                        cells.append({"values": [{"userEnteredFormat": {"backgroundColor": rgb(v), "textFormat": {"foregroundColor": rgb(contrast(v))}}}]})
                    else:
                        cells.append({"values": [{}]})
                req.append({"updateCells": {"rows": cells, "fields": "userEnteredFormat.backgroundColor,userEnteredFormat.textFormat.foregroundColor",
                                            "start": {"sheetId": sid, "rowIndex": 2, "columnIndex": c}}})

        if len(rows[0]) > 1:
            req.append({"updateCells": {"rows": [{"values": [{} for _ in range(len(rows[0]) - 1)]}], "fields": "userEnteredValue",
                                        "start": {"sheetId": sid, "rowIndex": 0, "columnIndex": 1}}})
        if notes.get("_table"):
            req.append({"updateCells": {"rows": [{"values": [{"note": notes["_table"]}]}], "fields": "note",
                                        "start": {"sheetId": sid, "rowIndex": 0, "columnIndex": 0}}})
        for c, w in widths.items():
            req.append({"updateDimensionProperties": {"range": {"sheetId": sid, "dimension": "COLUMNS", "startIndex": c, "endIndex": c + 1},
                                                      "properties": {"pixelSize": w}, "fields": "pixelSize"}})
        req.append({"updateDimensionProperties": {"range": {"sheetId": sid, "dimension": "ROWS", "startIndex": 0, "endIndex": 1},
                                                  "properties": {"pixelSize": 21}, "fields": "pixelSize"}})
        req.append({"updateDimensionProperties": {"range": {"sheetId": sid, "dimension": "ROWS", "startIndex": 1, "endIndex": 2},
                                                  "properties": {"pixelSize": 30}, "fields": "pixelSize"}})

        managed = [c for c in range(len(field_row)) if enum_header(field_row[c])]
        deletes = []
        for i, rule in enumerate(props[title].get("conditionalFormats", [])):
            cond = rule.get("booleanRule", {}).get("condition", {})
            rngs = rule.get("ranges", [])
            if cond.get("type") == "TEXT_EQ" and rngs and all(
                    rg.get("startColumnIndex") in managed and rg.get("endColumnIndex") == rg.get("startColumnIndex") + 1 for rg in rngs):
                deletes.append(i)
        req = [{"deleteConditionalFormatRule": {"sheetId": sid, "index": i}} for i in sorted(deletes, reverse=True)] + req
        if args.widths_only:
            req = [r for r in req if "updateDimensionProperties" in r]
        batches.append((title, req))

    if args.widths_only:
        batches = [b for b in batches if b[0] != "setup"]
    elif enum_lists:
        req = []
        header, used, values_rows = [], [], []
        for token in enum_tokens:
            header.append(text_cell(token, STYLE["header_bg"], STYLE["header_fg"], True))
            where = sorted({t for t, _ in enum_cols[token]})
            used.append(text_cell("Dùng ở: " + ", ".join(where)))
        depth = max(len(v) for v in enum_lists.values())
        for i in range(depth):
            row = []
            for token in enum_tokens:
                vals = enum_lists[token]
                if i >= len(vals):
                    row.append({})
                    continue
                v = vals[i]
                palette = spec["enum_colors"].get(short_enum(token), {})
                in_cs = token not in enums or v in enums[token]
                if v in palette:
                    row.append(text_cell(v, palette[v][0], palette[v][1]))
                elif not in_cs:
                    row.append(text_cell(v, STYLE["missing_bg"], None, None, "Chưa có trong enum C#. Bấm Update Enum trong Unity để thêm."))
                else:
                    row.append(text_cell(v))
            values_rows.append({"values": row})
        req.append({"updateCells": {"rows": [{"values": header}, {"values": used}] + values_rows,
                                    "fields": "userEnteredValue,userEnteredFormat.backgroundColor,userEnteredFormat.textFormat.foregroundColor,"
                                              "userEnteredFormat.textFormat.bold,note",
                                    "range": grid(enums_id, 0, None, 0, None)}})
        req.append(repeat(enums_id, (1, 2, None, None), {"wrapStrategy": "WRAP", "verticalAlignment": "TOP",
                                                          "textFormat": {"italic": True, "fontSize": 9, "foregroundColor": rgb(STYLE["row1_fg"])}},
                          "userEnteredFormat.wrapStrategy,userEnteredFormat.verticalAlignment,userEnteredFormat.textFormat.italic,"
                          "userEnteredFormat.textFormat.fontSize,userEnteredFormat.textFormat.foregroundColor"))
        for i, token in enumerate(enum_tokens):
            w = max([text_px(token, 10, True)] + [text_px(v, 10) for v in enum_lists[token]]) + 24
            req.append({"updateDimensionProperties": {"range": {"sheetId": enums_id, "dimension": "COLUMNS", "startIndex": i, "endIndex": i + 1},
                                                      "properties": {"pixelSize": int(min(max(w, 90), 360))}, "fields": "pixelSize"}})
        req.append({"autoResizeDimensions": {"dimensions": {"sheetId": enums_id, "dimension": "ROWS", "startIndex": 0, "endIndex": 2}}})
        batches.append((ENUMS_TAB, req))

    if not args.widths_only:
        add_guide(batches, guide_id)
    write_batches(args, batches, report, enum_tokens, enums)


def add_guide(batches, guide_id):
    guide = [{"values": [text_cell(line, None, None, True if i == 0 or (line and GUIDE_LINES[i - 1] == "") else None)]} for i, line in enumerate(GUIDE_LINES)]
    batches.append((GUIDE_TAB, [
        {"updateCells": {"rows": guide, "fields": "userEnteredValue,userEnteredFormat.textFormat.bold", "range": grid(guide_id, 0, None, 0, 1)}},
        {"updateDimensionProperties": {"range": {"sheetId": guide_id, "dimension": "COLUMNS", "startIndex": 0, "endIndex": 1},
                                       "properties": {"pixelSize": 900}, "fields": "pixelSize"}},
    ]))


def write_batches(args, batches, report, enum_tokens, enums):
    if args.only:
        keep = {n.strip() for n in args.only.split(",")}
        batches = [b for b in batches if b[0] in keep]
    if args.types:
        types = {t.strip() for t in args.types.split(",")}
        batches = [(n, [r for r in reqs if next(iter(r)) in types]) for n, reqs in batches]
        batches = [b for b in batches if b[1]]
    merged, cur_names, cur = [], [], []
    limit = args.max_kb * 1024
    for name, reqs in batches:
        size = len(json.dumps(cur + reqs, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        if cur and size > limit:
            merged.append((cur_names, cur))
            cur_names, cur = [], []
        cur_names.append(name)
        cur = cur + reqs
    if cur:
        merged.append((cur_names, cur))
    for i, (names, reqs) in enumerate(merged):
        path = os.path.join(args.out_dir, f"batch_{i + 1:02d}.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(reqs, f, ensure_ascii=False, separators=(",", ":"))
        print(f"{path}: {len(reqs)} requests, {os.path.getsize(path) // 1024} KB ({', '.join(names)})")
    print("send the batches in order, one update_spreadsheet call each")
    for line in report:
        print("note: " + line)
    unknown = [t for t in enum_tokens if enums and t not in enums]
    if unknown:
        print("note: enums not found in the C# scan, dropdown lists only hold the sheet's own values: " + ", ".join(unknown))


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    def src(p):
        p.add_argument("--id", help="spreadsheet id (downloads the xlsx export)")
        p.add_argument("--xlsx", help="a saved export instead of downloading")

    p = sub.add_parser("fetch")
    p.add_argument("--id", required=True)
    p.add_argument("--out", required=True)
    p.set_defaults(fn=cmd_fetch)

    p = sub.add_parser("snapshot")
    src(p)
    p.add_argument("--out")
    p.set_defaults(fn=cmd_snapshot)

    p = sub.add_parser("diff")
    p.add_argument("before")
    p.add_argument("after")
    p.set_defaults(fn=cmd_diff)

    p = sub.add_parser("lint")
    src(p)
    p.add_argument("--cs-root", help="folder scanned for C# enums, usually the Unity project's Assets")
    p.set_defaults(fn=cmd_lint)

    p = sub.add_parser("style")
    src(p)
    p.add_argument("--meta", required=True, help="get_spreadsheet result with fields sheets.properties and sheets.conditionalFormats")
    p.add_argument("--spec", help="per-spreadsheet JSON: notes, refs, enum_sources, enum_colors")
    p.add_argument("--cs-root", help="folder scanned for C# enums")
    p.add_argument("--out-dir", required=True)
    p.add_argument("--only", help="comma-separated batch names to emit (tab titles, setup, _Enums, _Guide), to resume after a failure")
    p.add_argument("--types", help="comma-separated request types to keep, e.g. setDataValidation,deleteConditionalFormatRule")
    p.add_argument("--widths-only", action="store_true", help="only column widths, for re-fitting after data changed")
    p.add_argument("--max-kb", type=int, default=40, help="merge consecutive tab batches up to this size per file")
    p.set_defaults(fn=cmd_style)

    args = ap.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
