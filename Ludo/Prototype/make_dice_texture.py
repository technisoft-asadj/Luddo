"""Draws the face atlas of the 3D dice: Assets/_Game/Art/Dice/dice_faces.png (4 x 2 cells of 256 px).
Cells 0..5 = the faces 1..6 (ivory with sunken pips, a red 1 like a classic dice), cell 6 = plain ivory for the bevels,
cell 7 unused. Drawn at 4x and scaled down for smooth edges. Pure code, no outside art.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Game", "Art", "Dice", "dice_faces.png")
CELL, SS = 256, 4
IVORY = (250, 248, 240)
EDGE = (222, 218, 205)
PIP = (28, 32, 52)
RED = (214, 34, 44)

PIPS = {
    1: [(0.5, 0.5)],
    2: [(0.27, 0.27), (0.73, 0.73)],
    3: [(0.25, 0.25), (0.5, 0.5), (0.75, 0.75)],
    4: [(0.27, 0.27), (0.73, 0.27), (0.27, 0.73), (0.73, 0.73)],
    5: [(0.25, 0.25), (0.75, 0.25), (0.5, 0.5), (0.25, 0.75), (0.75, 0.75)],
    6: [(0.27, 0.23), (0.73, 0.23), (0.27, 0.5), (0.73, 0.5), (0.27, 0.77), (0.73, 0.77)],
}

def face(value):
    s = CELL * SS
    img = Image.new("RGB", (s, s), IVORY)
    d = ImageDraw.Draw(img)
    # soft darkening towards the border: the face seems to curve into the bevel
    for i in range(24):
        t = i / 24
        c = tuple(round(EDGE[k] + (IVORY[k] - EDGE[k]) * t) for k in range(3))
        m = round(i * s * 0.0035)
        d.rectangle([m, m, s - 1 - m, s - 1 - m], outline=c, width=round(s * 0.0035) + 1)
    if value == 0:
        return img.resize((CELL, CELL), Image.LANCZOS)
    r = s * (0.13 if value == 1 else 0.085)
    colour = RED if value == 1 else PIP
    for (x, y) in PIPS[value]:
        cx, cy = x * s, y * s
        # a light rim below and a dark one above: the pip looks drilled into the face
        d.ellipse([cx - r * 1.12, cy - r * 1.12 + r * 0.10, cx + r * 1.12, cy + r * 1.12 + r * 0.10], fill=(255, 255, 255))
        d.ellipse([cx - r * 1.06, cy - r * 1.06, cx + r * 1.06, cy + r * 1.06], fill=tuple(max(0, v - 40) for v in EDGE))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=colour)
        hl = tuple(min(255, v + 55) for v in colour)
        d.ellipse([cx - r * 0.55, cy + r * 0.05, cx + r * 0.35, cy + r * 0.75], fill=hl)
        d.ellipse([cx - r * 0.72, cy - r * 0.72, cx + r * 0.72, cy + r * 0.62], fill=colour)
    return img.filter(ImageFilter.GaussianBlur(SS * 0.4)).resize((CELL, CELL), Image.LANCZOS)

def pawn_face():
    """Cell 7: a pawn silhouette, drawn the same way a pip is (dark ink, soft drilled-in shading) so it reads as a die
    face rather than a sticker. DiceView tints the whole die per seat colour while this face is showing (see ShowSeatIcon)."""
    s = CELL * SS
    img = Image.new("RGB", (s, s), IVORY)
    d = ImageDraw.Draw(img)
    for i in range(24):
        t = i / 24
        c = tuple(round(EDGE[k] + (IVORY[k] - EDGE[k]) * t) for k in range(3))
        m = round(i * s * 0.0035)
        d.rectangle([m, m, s - 1 - m, s - 1 - m], outline=c, width=round(s * 0.0035) + 1)
    cx, cy = s * 0.5, s * 0.5
    head_r = s * 0.155
    head_cy = cy - s * 0.20
    body_top, body_bottom = cy - s * 0.02, cy + s * 0.30
    body_half_top, body_half_bottom = s * 0.10, s * 0.205
    # a soft drilled-in shadow first (like the pip's rim), then the dark pawn shape on top
    for dy, col in ((s * 0.02, tuple(max(0, v - 40) for v in EDGE)), (0, PIP)):
        d.ellipse([cx - head_r, head_cy - head_r + dy, cx + head_r, head_cy + head_r + dy], fill=col)
        d.polygon([
            (cx - body_half_top, body_top + dy), (cx + body_half_top, body_top + dy),
            (cx + body_half_bottom, body_bottom + dy), (cx - body_half_bottom, body_bottom + dy),
        ], fill=col)
        d.ellipse([cx - body_half_bottom, body_bottom - s * 0.05 + dy, cx + body_half_bottom, body_bottom + s * 0.05 + dy], fill=col)
    hl = tuple(min(255, v + 55) for v in PIP)
    d.ellipse([cx - head_r * 0.4, head_cy - head_r * 0.55, cx + head_r * 0.15, head_cy - head_r * 0.05], fill=hl)
    return img.filter(ImageFilter.GaussianBlur(SS * 0.4)).resize((CELL, CELL), Image.LANCZOS)

def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    atlas = Image.new("RGB", (CELL * 4, CELL * 2), IVORY)
    for value in range(1, 7):
        i = value - 1
        atlas.paste(face(value), ((i % 4) * CELL, (i // 4) * CELL))
    plain = Image.new("RGB", (CELL, CELL), tuple(round(v * 0.97) for v in IVORY))
    atlas.paste(plain, (2 * CELL, CELL))
    atlas.paste(pawn_face(), (3 * CELL, CELL))
    atlas.save(OUT)
    print("saved", OUT)

if __name__ == "__main__":
    main()
