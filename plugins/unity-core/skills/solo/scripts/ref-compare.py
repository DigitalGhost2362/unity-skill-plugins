"""Score a game capture against a reference image and write a side-by-side sheet.

    python ref-compare.py <current.png> <ref.png> [--before before.png] [--out sheet.png]
                          [--regions sky:0-0.12,far:0.12-0.32,...] [--grid 40x18]

Prints, per horizontal region (fractions of the image height): mean colour, contrast (std of
luminance), saturation (max-min channel) and a 0-1 score; then a grid score from per-cell colour
difference on a coarse grid (where light, shadow and colour sit), and the mean difference per grid
row. The sheet is a labelled 2-column grid: current | ref, then [before |] grid difference heatmap. Look at the sheet
first every round: the numbers plateau while a large difference (a shadow pattern, a missing layer)
stays obvious side by side.

Needs Pillow and numpy (pip install pillow numpy).
"""
import argparse

import numpy as np
from PIL import Image, ImageDraw, ImageFont

DEFAULT_REGIONS = "sky:0-0.12,far:0.12-0.32,mid:0.32-0.6,near:0.6-1"


def load(path, size, resample):
    return np.asarray(Image.open(path).convert("RGB").resize(size, resample), dtype=np.float32) / 255


def region_scores(cur, ref, regions):
    h = cur.shape[0]
    scores = []
    for name, (y0, y1) in regions:
        a, b = ref[int(y0 * h):int(y1 * h)], cur[int(y0 * h):int(y1 * h)]
        ma, mb = a.reshape(-1, 3).mean(0), b.reshape(-1, 3).mean(0)
        ca, cb = a.mean(2).std(), b.mean(2).std()
        sa, sb = (a.max(2) - a.min(2)).mean(), (b.max(2) - b.min(2)).mean()
        score = max(0.0, 1 - np.abs(ma - mb).mean() * 4 - abs(ca - cb) * 2 - abs(sa - sb) * 2)
        scores.append(score)
        print(f"{name:6s} colour ref={(ma * 255).astype(int)} cur={(mb * 255).astype(int)} "
              f"contrast ref={ca:.3f} cur={cb:.3f} sat ref={sa:.3f} cur={sb:.3f} score={score:.2f}")
    print(f"regions={np.mean(scores):.2f}")


def grid_score(cur_path, ref_path, grid):
    a, b = load(ref_path, grid, Image.BOX), load(cur_path, grid, Image.BOX)
    cell = np.abs(a - b).mean(2)
    print(f"grid={max(0.0, 1 - cell.mean() * 3):.2f} (mean cell diff {cell.mean() * 255:.1f}/255)")
    print("rows " + " ".join(f"{r:.0f}" for r in cell.mean(1) * 255))
    return cell


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
    args = p.parse_args()

    regions = []
    for part in args.regions.split(","):
        name, span = part.split(":")
        y0, y1 = span.split("-")
        regions.append((name, (float(y0), float(y1))))
    grid = tuple(int(v) for v in args.grid.lower().split("x"))

    region_scores(load(args.current, (320, 144), Image.BILINEAR), load(args.ref, (320, 144), Image.BILINEAR), regions)
    cell = grid_score(args.current, args.ref, grid)
    panels = [("current", Image.open(args.current)), ("ref", Image.open(args.ref))]
    if args.before:
        panels.append(("before", Image.open(args.before)))
    sheet(panels, cell, args.out)


if __name__ == "__main__":
    main()
