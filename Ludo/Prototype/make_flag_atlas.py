"""Builds the country-flag atlas used by the game (Assets/_Game/Art/Flags/flags_atlas.png + flags_atlas.txt).

Source: the public-domain flag images in googlefonts/noto-emoji, third_party/region-flags/png (see
Assets/ThirdParty/RegionFlags/LICENSE.txt). Only the countries listed in Assets/_Game/Scripts/Core/Countries.cs are packed.
Each flag is scaled to fit a 72x48 box (aspect kept), given a thin dark edge so white flags show on white cards, and placed
in a grid. The .txt lists "CODE x y w h" (pixels, origin bottom-left as Unity expects). Downloads are cached.

Usage: python make_flag_atlas.py [cache_dir]
"""
import io, os, re, sys, urllib.request
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COUNTRIES = os.path.join(ROOT, "Assets", "_Game", "Scripts", "Core", "Countries.cs")
OUT_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Flags")
URL = "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/third_party/region-flags/png/{}.png"
BOX_W, BOX_H, PAD, COLS = 72, 48, 3, 16

def codes():
    text = open(COUNTRIES, encoding="utf-8").read()
    block = text[text.index("Data ="):text.index("};", text.index("Data ="))]
    return re.findall(r'"([A-Z]{2})", "', block)

def fetch(code, cache):
    path = os.path.join(cache, code + ".png")
    if not os.path.exists(path):
        with urllib.request.urlopen(URL.format(code), timeout=30) as r:
            open(path, "wb").write(r.read())
    data = open(path, "rb").read()
    if not data.startswith(b"\x89PNG"):            # a git symlink (e.g. BV -> NO.png): the file holds the target's name
        return fetch(os.path.basename(data.decode("ascii").strip())[:-4], cache)
    return Image.open(io.BytesIO(data)).convert("RGBA")

def main():
    cache = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Temp", "flag_cache")
    os.makedirs(cache, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    all_codes = codes()
    cell_w, cell_h = BOX_W + PAD * 2, BOX_H + PAD * 2
    rows = (len(all_codes) + COLS - 1) // COLS
    atlas = Image.new("RGBA", (COLS * cell_w, rows * cell_h), (0, 0, 0, 0))
    lines = []
    for i, code in enumerate(all_codes):
        img = fetch(code, cache)
        scale = min(BOX_W / img.width, BOX_H / img.height)
        w, h = max(1, round(img.width * scale)), max(1, round(img.height * scale))
        img = img.resize((w, h), Image.LANCZOS)
        edge = ImageDraw.Draw(img)
        edge.rectangle([0, 0, w - 1, h - 1], outline=(10, 20, 50, 110))
        cx, cy = (i % COLS) * cell_w + PAD + (BOX_W - w) // 2, (i // COLS) * cell_h + PAD + (BOX_H - h) // 2
        atlas.paste(img, (cx, cy))
        lines.append(f"{code} {cx} {atlas.height - cy - h} {w} {h}")
    atlas.save(os.path.join(OUT_DIR, "flags_atlas.png"))
    open(os.path.join(OUT_DIR, "flags_atlas.txt"), "w", newline="\n").write("\n".join(lines) + "\n")
    print(len(all_codes), "flags ->", atlas.size)

if __name__ == "__main__":
    main()
