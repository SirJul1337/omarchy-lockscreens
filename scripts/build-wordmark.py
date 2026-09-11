#!/usr/bin/env python3
"""Draw LOCKSCREENS in the Omarchy wordmark's own hand.

The Omarchy wordmark is not type. It is a bitmap on a 51x50 unit grid, shipped
as an SVG the page wears as a mask so a gradient shows through it -- see
omacom/omarchy-site, src/data/wordmark-bitmap.ts and
scripts/build-not-found-glyph.mjs, which cuts N T F U D the same way this cuts
what it needs. Everything here follows that file's rules:

  - strokes are three cells wide, on a 16-row body (rows 1..16)
  - corners are cut on the diagonal, one cell per row, over two rows
  - crossbars are two rows sheared by one cell (H, R, A)
  - a terminal either tapers to a point going up (H, Y) or beaks
    inward going right (C)
  - the letter's rightmost stem loses its bottom-right corner, but only
    when a left stem holds the other side (A, R, H, M)
  - there are no true diagonals; a diagonal is a stair, two rows to a tread

O, C and R are lifted unchanged from the Omarchy wordmark, so half of
LOCKSCREENS is literally the same letterforms. L, K, S, E and N are cut here
to the rules above.

Run: python scripts/build-wordmark.py
"""
import json
import os
from pathlib import Path

ROWS = 17  # rows 0..16; row 0 stays empty, the body starts at 1
CW, CH = 51, 50  # the wordmark's cell, kept exactly


def box(w):
    return [["0"] * w for _ in range(ROWS)]


def fill(g, r0, r1, c0, c1):
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1):
            g[r][c] = "1"


def clear(g, r0, r1, c0, c1):
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1):
            g[r][c] = "0"


def from_art(art):
    """A glyph copied cell for cell out of the Omarchy wordmark."""
    g = box(len(art[0]))
    for r, line in enumerate(art):
        for c, ch in enumerate(line):
            if ch == "#":
                g[r + 1][c] = "1"
    return g


# ---- lifted from the Omarchy wordmark, rows 1..16 -------------------------

O = from_art([
    "..#####..",
    ".#######.",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    "###...###",
    ".#######.",
    "..#####..",
])

C = from_art([
    "..#######",
    ".########",
    "###...###",
    "###...###",
    "###...##.",
    "###...#..",
    "###......",
    "###......",
    "###......",
    "###......",
    "###...#..",
    "###...##.",
    "###...###",
    "###...###",
    "########.",
    "#######..",
])

R = from_art([
    "...#######",
    "..########",
    ".###...###",
    ".###...###",
    ".###...###",
    ".###...###",
    ".###...###",
    "#########.",
    "########..",
    ".###......",
    "##########",
    "##########",
    ".###...###",
    ".###...###",
    ".###...###",
    ".###...###",
])

# ---- cut here, to the same rules -----------------------------------------
#
# Drawn out cell by cell rather than composed from fills: these are letterforms,
# and the only way to be sure of one is to look at it.

# A stem tapering to a point going up, and C's bottom bar.
L = from_art([
    "..#......",
    ".##......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "########.",
    "#######..",
])

# C's cut corners top and bottom, with A's sheared crossbar between them.
E = from_art([
    "..#######",
    ".########",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "#########",
    "########.",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    ".########",
    "..#######",
])

# Two stairs off one stem, two rows to a tread, meeting on the crossbar rows.
K = from_art([
    "..#...###",
    ".##...###",
    "###...###",
    "###..###.",
    "###..###.",
    "###.###..",
    "###.###..",
    "######...",
    "######...",
    "###.###..",
    "###.###..",
    "###..###.",
    "###..###.",
    "###...###",
    "###...###",
    "###...###",
])

# Top bar and bottom bar as C's, the two stems on opposite sides of the
# sheared crossbar.
S = from_art([
    "..#######",
    ".########",
    "###......",
    "###......",
    "###......",
    "###......",
    "###......",
    "#########",
    ".########",
    "......###",
    "......###",
    "......###",
    "......###",
    "......###",
    "########.",
    "#######..",
])

# The stair N from build-not-found-glyph.mjs, unchanged.
N = box(14)
fill(N, 3, 16, 0, 2)
fill(N, 3, 16, 11, 13)
fill(N, 1, 1, 2, 2)
fill(N, 2, 2, 1, 2)
fill(N, 1, 1, 11, 11)
fill(N, 2, 2, 11, 12)
for i in range(6):
    fill(N, 3 + i * 2, 4 + i * 2, 3 + i, 5 + i)
clear(N, 15, 15, 13, 13)
clear(N, 16, 16, 12, 13)
clear(N, 15, 15, 0, 0)
clear(N, 16, 16, 0, 1)

GLYPHS = {"L": L, "O": O, "C": C, "K": K, "S": S, "R": R, "E": E, "N": N}
WORD = "LOCKSCREENS"
GAP = 2



# ---- the mark ------------------------------------------------------------
#
# A padlock on the same pixel grid as the wordmark, in the same hand: two-cell
# strokes rather than three because it is drawn far smaller, corners cut on the
# diagonal, and no true diagonals anywhere -- the shackle is an arch of stairs,
# not a curve. Fourteen cells square, which still reads at 16px in a tab.

MARK = [
    "....######....",
    "...########...",
    "...##....##...",
    "...##....##...",
    "...##....##...",
    "...##....##...",
    ".############.",
    "##############",
    "#####....#####",
    "#####....#####",
    "######..######",
    "######..######",
    "##############",
    ".############.",
]


def build_mark(out_dir):
    """The mark as two files: a shape to wear as a mask, and a favicon."""
    cell = 50
    w = len(MARK[0]) * cell
    h = len(MARK) * cell

    rects = []
    for r, line in enumerate(MARK):
        c = 0
        while c < len(line):
            if line[c] != "#":
                c += 1
                continue
            start = c
            while c < len(line) and line[c] == "#":
                c += 1
            rects.append(
                f'<rect x="{start * cell}" y="{r * cell}" '
                f'width="{(c - start) * cell}" height="{cell}"/>'
            )
    body = "".join(rects)

    # Worn as a CSS mask, so the header takes the accent colour of the theme.
    mask = out_dir / "brand/lockscreens-mark.svg"
    mask.parent.mkdir(parents=True, exist_ok=True)
    mask.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" '
        f'width="{w}" height="{h}">'
        f'<g fill="#000" shape-rendering="crispEdges">{body}</g></svg>\n',
        encoding="utf-8",
    )

    # The favicon carries its own colour, and a little air so the shape is not
    # jammed against the edge of a tab.
    pad = cell
    fav = out_dir / "favicon.svg"
    fav.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" '
        f'viewBox="{-pad} {-pad} {w + pad * 2} {h + pad * 2}">'
        f'<g fill="#9ece6a" shape-rendering="crispEdges">{body}</g></svg>\n',
        encoding="utf-8",
    )
    return mask, fav, len(rects), body


def main() -> None:
    columns = []
    for i, ch in enumerate(WORD):
        g = GLYPHS[ch]
        for c in range(len(g[0])):
            columns.append([g[r][c] for r in range(ROWS)])
        if i + 1 < len(WORD):
            for _ in range(GAP):
                columns.append(["0"] * ROWS)

    grid = ["".join(col[r] for col in columns) for r in range(ROWS)]
    first = next(i for i, r in enumerate(grid) if "1" in r)
    last = len(grid) - 1 - next(i for i, r in enumerate(reversed(grid)) if "1" in r)
    rows = grid[first:last + 1]
    w, h = len(rows[0]), len(rows)

    rects = []
    for r in range(h):
        c = 0
        while c < w:
            if rows[r][c] != "1":
                c += 1
                continue
            start = c
            while c < w and rows[r][c] == "1":
                c += 1
            rects.append(
                f'<rect x="{start * CW}" y="{r * CH}" '
                f'width="{(c - start) * CW}" height="{CH}"/>'
            )

    out = Path(__file__).resolve().parent.parent / "wwwroot/brand/lockscreens-wordmark.svg"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w * CW} {h * CH}" '
        f'width="{w * CW}" height="{h * CH}">'
        f'<g fill="#9ece6a" shape-rendering="crispEdges">{"".join(rects)}</g></svg>\n',
        encoding="utf-8",
    )

    # The same bitmap the mask is cut from, for the field to draw and animate.
    # omarchy.org keeps this pair for the same reason (src/data/wordmark-bitmap.ts):
    # the word and the field are then on one grid and cannot fall out of step.
    js = Path(__file__).resolve().parent.parent / "wwwroot/js/wordmark-bitmap.js"
    rows_js = ",\n    ".join("'" + r + "'" for r in rows)
    js.write_text(
        "/* Generated by scripts/build-wordmark.py -- do not edit.\n"
        f"   LOCKSCREENS as a {w}x{h} bitmap: the same cells the mask in\n"
        "   /brand/lockscreens-wordmark.svg is cut from, so the field can\n"
        "   draw the word on its own grid and animate it. */\n"
        "window.WORDMARK = {\n"
        f"  width: {w},\n"
        f"  height: {h},\n"
        "  rows: [\n    " + rows_js + "\n  ]\n};\n",
        encoding="utf-8",
    )

    print(f"{w} x {h} cells, {len(rects)} rects, aspect {w * CW / (h * CH):.3f}")
    print(f"-> {out}")
    mask, fav, n, mark_body = build_mark(Path(__file__).resolve().parent.parent / "wwwroot")
    # The same rects again, for the script that repaints the tab icon in the
    # theme's accent -- a favicon cannot carry a CSS variable.
    with js.open("a", encoding="utf-8") as f:
        f.write(os.linesep.join(["", "window.MARK_PATHS = " + json.dumps(mark_body) + ";", ""]))
    print(f"-> {js}")
    print(f"-> {mask} ({n} rects)")
    print(f"-> {fav}")
    for r in rows:
        print("  " + "".join("#" if c == "1" else "." for c in r))


if __name__ == "__main__":
    main()
