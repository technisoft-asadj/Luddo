"""Draws the white UI icons the menu screens need into Assets/_Game/Art/UI/Icons/.

Each icon is a plain white silhouette with an alpha channel, 192x192, drawn at 4x and scaled down for smooth edges -
exactly like the shapes UiArtGenerator makes, just built from polygons instead of per-pixel maths because a crown or a
gem is far easier to describe as a shape. Colour always comes from the Image tint in Unity, so one file serves every
colour it is used in. Pure code, no outside artwork.
"""
import math
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "Icons")
SIZE, SS = 192, 4
W = SIZE * SS
WHITE = (255, 255, 255, 255)


def canvas():
    img = Image.new("RGBA", (W, W), (255, 255, 255, 0))
    return img, ImageDraw.Draw(img)


def p(*xy):
    """Fractions of the icon box -> pixels."""
    return [(x * W, y * W) for x, y in xy]


def ellipse(d, cx, cy, rx, ry=None, fill=WHITE):
    ry = rx if ry is None else ry
    d.ellipse([(cx - rx) * W, (cy - ry) * W, (cx + rx) * W, (cy + ry) * W], fill=fill)


def rect(d, x0, y0, x1, y1, radius=0.0, fill=WHITE):
    box = [x0 * W, y0 * W, x1 * W, y1 * W]
    if radius > 0:
        d.rounded_rectangle(box, radius=radius * W, fill=fill)
    else:
        d.rectangle(box, fill=fill)


def hole(d, shape_fn):
    """Draw with a fully transparent colour to punch a hole in what is already there."""
    shape_fn((0, 0, 0, 0))


# ---------------------------------------------------------------- the icons

def crown():
    img, d = canvas()
    d.polygon(p((0.12, 0.72), (0.18, 0.28), (0.32, 0.50), (0.50, 0.22),
               (0.68, 0.50), (0.82, 0.28), (0.88, 0.72)), fill=WHITE)
    rect(d, 0.14, 0.70, 0.86, 0.82, 0.05)
    for cx in (0.18, 0.50, 0.82):
        ellipse(d, cx, 0.27, 0.075)
    return img


def gem():
    img, d = canvas()
    d.polygon(p((0.50, 0.14), (0.86, 0.42), (0.50, 0.88), (0.14, 0.42)), fill=WHITE)
    d.polygon(p((0.50, 0.14), (0.68, 0.42), (0.50, 0.88), (0.32, 0.42)), fill=(0, 0, 0, 0))
    d.polygon(p((0.50, 0.16), (0.655, 0.42), (0.50, 0.84), (0.345, 0.42)), fill=WHITE)
    d.line(p((0.14, 0.42), (0.86, 0.42)), fill=(0, 0, 0, 0), width=int(W * 0.022))
    return img


def star():
    img, d = canvas()
    pts = []
    for i in range(10):
        angle = -math.pi / 2 + i * math.pi / 5
        r = 0.40 if i % 2 == 0 else 0.17
        pts.append((0.5 + math.cos(angle) * r, 0.5 + math.sin(angle) * r))
    d.polygon(p(*pts), fill=WHITE)
    return img


def key():
    img, d = canvas()
    ellipse(d, 0.32, 0.50, 0.24)
    ellipse(d, 0.32, 0.50, 0.105, fill=(0, 0, 0, 0))
    rect(d, 0.50, 0.44, 0.88, 0.56, 0.04)
    rect(d, 0.70, 0.56, 0.78, 0.70, 0.02)
    rect(d, 0.82, 0.56, 0.90, 0.68, 0.02)
    return img


def house():
    img, d = canvas()
    d.polygon(p((0.50, 0.12), (0.94, 0.50), (0.06, 0.50)), fill=WHITE)
    rect(d, 0.18, 0.48, 0.82, 0.88, 0.04)
    rect(d, 0.40, 0.62, 0.60, 0.88, 0.02, fill=(0, 0, 0, 0))
    return img


def gamepad():
    img, d = canvas()
    rect(d, 0.08, 0.32, 0.92, 0.70, 0.18)
    ellipse(d, 0.20, 0.72, 0.15)
    ellipse(d, 0.80, 0.72, 0.15)
    rect(d, 0.20, 0.46, 0.38, 0.52, 0.02, fill=(0, 0, 0, 0))
    rect(d, 0.26, 0.40, 0.32, 0.58, 0.02, fill=(0, 0, 0, 0))
    ellipse(d, 0.70, 0.44, 0.045, fill=(0, 0, 0, 0))
    ellipse(d, 0.80, 0.54, 0.045, fill=(0, 0, 0, 0))
    return img


def calendar():
    img, d = canvas()
    rect(d, 0.10, 0.20, 0.90, 0.88, 0.09)
    rect(d, 0.16, 0.40, 0.84, 0.82, 0.03, fill=(0, 0, 0, 0))
    rect(d, 0.26, 0.10, 0.34, 0.30, 0.03)
    rect(d, 0.66, 0.10, 0.74, 0.30, 0.03)
    for row in range(2):
        for col in range(3):
            rect(d, 0.24 + col * 0.19, 0.50 + row * 0.16, 0.335 + col * 0.19, 0.58 + row * 0.16, 0.015)
    return img


def gift():
    img, d = canvas()
    rect(d, 0.10, 0.36, 0.90, 0.50, 0.04)
    rect(d, 0.16, 0.50, 0.84, 0.90, 0.05)
    rect(d, 0.44, 0.36, 0.56, 0.90, 0.0, fill=(0, 0, 0, 0))
    rect(d, 0.455, 0.36, 0.545, 0.90)
    ellipse(d, 0.36, 0.22, 0.14, 0.12)
    ellipse(d, 0.64, 0.22, 0.14, 0.12)
    ellipse(d, 0.36, 0.22, 0.055, 0.045, fill=(0, 0, 0, 0))
    ellipse(d, 0.64, 0.22, 0.055, 0.045, fill=(0, 0, 0, 0))
    return img


def spin():
    img, d = canvas()
    ellipse(d, 0.5, 0.5, 0.44)
    for i in range(4):
        a = math.radians(i * 45 + 22.5)
        dx, dy = math.cos(a) * 0.44, math.sin(a) * 0.44
        d.line(p((0.5 - dx, 0.5 - dy), (0.5 + dx, 0.5 + dy)), fill=(0, 0, 0, 0), width=int(W * 0.035))
    ellipse(d, 0.5, 0.5, 0.115, fill=(0, 0, 0, 0))
    ellipse(d, 0.5, 0.5, 0.07)
    return img


def shield():
    img, d = canvas()
    # rounded shoulders on top, a clear point at the bottom - a hexagon does not read as a shield
    rect(d, 0.12, 0.10, 0.88, 0.52, 0.12)
    d.polygon(p((0.12, 0.44), (0.88, 0.44), (0.50, 0.94)), fill=WHITE)
    return img


def sliders():
    img, d = canvas()
    for i, y in enumerate((0.26, 0.50, 0.74)):
        rect(d, 0.10, y - 0.035, 0.90, y + 0.035, 0.035)
        cx = (0.68, 0.34, 0.56)[i]
        ellipse(d, cx, y, 0.115)
        ellipse(d, cx, y, 0.055, fill=(0, 0, 0, 0))
    return img


def medal():
    img, d = canvas()
    d.polygon(p((0.22, 0.06), (0.40, 0.06), (0.56, 0.42), (0.38, 0.46)), fill=WHITE)
    d.polygon(p((0.78, 0.06), (0.60, 0.06), (0.44, 0.42), (0.62, 0.46)), fill=WHITE)
    ellipse(d, 0.50, 0.66, 0.30)
    ellipse(d, 0.50, 0.66, 0.185, fill=(0, 0, 0, 0))
    return img


def grid():
    img, d = canvas()
    for row in range(2):
        for col in range(2):
            rect(d, 0.10 + col * 0.46, 0.10 + row * 0.46, 0.44 + col * 0.46, 0.44 + row * 0.46, 0.07)
    return img


def chart():
    img, d = canvas()
    rect(d, 0.12, 0.54, 0.32, 0.90, 0.04)
    rect(d, 0.40, 0.30, 0.60, 0.90, 0.04)
    rect(d, 0.68, 0.14, 0.88, 0.90, 0.04)
    return img


def lock():
    img, d = canvas()
    rect(d, 0.16, 0.44, 0.84, 0.90, 0.09)
    d.ellipse([0.28 * W, 0.12 * W, 0.72 * W, 0.58 * W], outline=WHITE, width=int(W * 0.085))
    rect(d, 0.16, 0.44, 0.84, 0.62, 0.0)
    ellipse(d, 0.50, 0.63, 0.075, fill=(0, 0, 0, 0))
    rect(d, 0.465, 0.63, 0.535, 0.78, 0.02, fill=(0, 0, 0, 0))
    return img


def chevron():
    img, d = canvas()
    d.line(p((0.36, 0.16), (0.68, 0.50), (0.36, 0.84)), fill=WHITE, width=int(W * 0.11), joint="curve")
    return img


def handshake():
    img, d = canvas()
    # two arms meeting in a grip: the grip has to be taller than the arms or the whole thing reads as one bar
    rect(d, 0.04, 0.42, 0.40, 0.58, 0.08)
    rect(d, 0.60, 0.42, 0.96, 0.58, 0.08)
    rect(d, 0.33, 0.30, 0.67, 0.70, 0.13)
    for x in (0.42, 0.50, 0.58):
        rect(d, x - 0.018, 0.33, x + 0.018, 0.67, 0.018, fill=(0, 0, 0, 0))
    return img


def coin():
    img, d = canvas()
    ellipse(d, 0.5, 0.5, 0.44)
    ellipse(d, 0.5, 0.5, 0.33, fill=(0, 0, 0, 0))
    ellipse(d, 0.5, 0.5, 0.26)
    return img


ICONS = {
    "crown": crown, "gem": gem, "star": star, "key": key, "house": house,
    "gamepad": gamepad, "calendar": calendar, "gift": gift, "spin": spin,
    "shield": shield, "sliders": sliders, "medal": medal, "grid": grid,
    "chart": chart, "lock": lock, "chevron": chevron, "handshake": handshake,
    "coin": coin,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in ICONS.items():
        fn().resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))
    print("saved", len(ICONS), "icons to", OUT)


if __name__ == "__main__":
    main()
