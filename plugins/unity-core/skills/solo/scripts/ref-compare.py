"""Score a game capture against a reference image and write a side-by-side sheet.

    python ref-compare.py <current.png> <ref.png> [--before before.png] [--out sheet.png]
                          [--regions sky:0-0.12,far:0.12-0.32,...] [--grid 40x18]
                          [--json metrics.json --key A]

Prints, per region (fractions of the image height, optionally `@x0-x1` for a box, e.g.
`fall:0-0.6@0.35-0.65`): mean colour, contrast (std of luminance), luminance p10/p90, saturation
(max-min channel) and a 0-1 score; then a grid score from per-cell colour
difference on a coarse grid (where light, shadow and colour sit), and the mean difference per grid
row. The sheet is a labelled 2-column grid: current | ref, then [before |] grid difference heatmap. Look at the sheet
first every round: the numbers plateau while a large difference (a shadow pattern, a missing layer)
stays obvious side by side.

--json merges the numbers under --key into that file (one key per shot), so review-loop's
`metric` criteria can read them, e.g. "A.fall.contrast" or "A.grid".

Needs Pillow and numpy (pip install pillow numpy).
"""
import argparse
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

DEFAULT_REGIONS = "sky:0-0.12,far:0.12-0.32,mid:0.32-0.6,near:0.6-1"


def load(path, size, resample):
    return np.asarray(Image.open(path).convert("RGB").resize(size, resample), dtype=np.float32) / 255


def stats(px):
    lum = px.mean(2)
    p10, p90 = np.percentile(lum, [10, 90])
    return {"colour": [int(v) for v in px.reshape(-1, 3).mean(0) * 255], "contrast": float(lum.std()),
            "lum_p10": float(p10), "lum_p90": float(p90), "sat": float((px.max(2) - px.min(2)).mean())}


def region_scores(cur, ref, regions):
    h, w = cur.shape[:2]
    scores, out = [], {}
    for name, (y0, y1, x0, x1) in regions:
        box = (slice(int(y0 * h), int(y1 * h)), slice(int(x0 * w), int(x1 * w)))
        a, b = stats(ref[box]), stats(cur[box])
        score = max(0.0, 1 - np.abs(np.subtract(a["colour"], b["colour"])).mean() / 255 * 4
                    - abs(a["contrast"] - b["contrast"]) * 2 - abs(a["sat"] - b["sat"]) * 2)
        scores.append(score)
        out[name] = {"ref": a, "cur": b, "score": round(score, 3)}
        print(f"{name:6s} colour ref={a['colour']} cur={b['colour']} contrast ref={a['contrast']:.3f} "
              f"cur={b['contrast']:.3f} p10/p90 ref={a['lum_p10']:.2f}/{a['lum_p90']:.2f} "
              f"cur={b['lum_p10']:.2f}/{b['lum_p90']:.2f} sat ref={a['sat']:.3f} cur={b['sat']:.3f} score={score:.2f}")
    print(f"regions={np.mean(scores):.2f}")
    return out, float(np.mean(scores))


def grid_score(cur_path, ref_path, grid):
    a, b = load(ref_path, grid, Image.BOX), load(cur_path, grid, Image.BOX)
    cell = np.abs(a - b).mean(2)
    print(f"grid={max(0.0, 1 - cell.mean() * 3):.2f} (mean cell diff {cell.mean() * 255:.1f}/255)")
    print("rows " + " ".join(f"{r:.0f}" for r in cell.mean(1) * 255))
    return cell


def write_json(path, key, data):
    merged = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            merged = json.load(f)
    merged[key] = data
    with open(path, "w", encoding="utf-8") as f:
        json.dump(merged, f, indent=2)
    print(f"json {path} [{key}]")


def sheet(panels, cell, out, height=360):
    heat = np.clip(cell / max(cell.max(), 1e-6), 0, 1)
    heat_img = Image.fromarray((np.stack([heat, 0.2 * heat, 1 - heat], 2) * 255).astype(np.uint8))
    panels = panels + [("diff (red = far)", heat_img)]
    try:
        font = ImageFont.load_default(size=22)
    except TypeError:
        font = ImageFont.load_default()
    width = round(height * panels[0][1].width / panels[0][1].height)
    gap, cols = 4, 2
    rows = (len(panels) + cols - 1) // cols
    out_img = Image.new("RGB", (cols * width + gap, rows * height + (rows - 1) * gap), (0, 0, 0))
    for i, (label, img) in enumerate(panels):
        img = img.convert("RGB").resize((width, height), Image.NEAREST if label.startswith("diff") else Image.BILINEAR)
        ImageDraw.Draw(img).text((10, 8), label, fill=(255, 255, 0), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
        out_img.paste(img, ((i % cols) * (width + gap), (i // cols) * (height + gap)))
    out_img.save(out)
    print(f"sheet {out}")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("current")
    p.add_argument("ref")
    p.add_argument("--before")
    p.add_argument("--out", default="ref-compare.png")
    p.add_argument("--regions", default=DEFAULT_REGIONS)
    p.add_argument("--grid", default="40x18")
    p.add_argument("--json", help="merge the numbers into this file under --key")
    p.add_argument("--key", default="shot")
    args = p.parse_args()

    regions = []
    for part in args.regions.split(","):
        name, span = part.split(":")
        ys, _, xs = span.partition("@")
        y0, y1 = ys.split("-")
        x0, x1 = xs.split("-") if xs else ("0", "1")
        regions.append((name, (float(y0), float(y1), float(x0), float(x1))))
    grid = tuple(int(v) for v in args.grid.lower().split("x"))

    per_region, mean_score = region_scores(load(args.current, (320, 144), Image.BILINEAR),
                                           load(args.ref, (320, 144), Image.BILINEAR), regions)
    cell = grid_score(args.current, args.ref, grid)
    if args.json:
        write_json(args.json, args.key, {"regions": per_region, "regions_score": round(mean_score, 3),
                                         "grid": round(max(0.0, 1 - float(cell.mean()) * 3), 3)})
    panels = [("current", Image.open(args.current)), ("ref", Image.open(args.ref))]
    if args.before:
        panels.append(("before", Image.open(args.before)))
    sheet(panels, cell, args.out)


if __name__ == "__main__":
    main()
