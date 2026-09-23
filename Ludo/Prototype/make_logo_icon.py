"""
Makes Assets/_Game/Art/Branding/logo_icon.png: the app icon with smooth (anti-aliased) rounded corners and a thin light rim,
used as the logo tile on the splash and main menu.   Run:  python Prototype/make_logo_icon.py   (needs: pillow)
"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Assets', '_Game', 'Art', 'Branding', 'app_icon_source.png')
OUT = os.path.join(ROOT, 'Assets', '_Game', 'Art', 'Branding', 'logo_icon.png')

SIZE = 1024
S = 4                                               # draw the mask 4x larger, then shrink: smooth edges
icon = Image.open(SRC).convert('RGBA').resize((SIZE, SIZE), Image.LANCZOS)
radius = int(SIZE * 0.22)
rim = 10

mask = Image.new('L', (SIZE * S, SIZE * S), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, SIZE * S - 1, SIZE * S - 1), radius * S, fill=255)
mask = mask.resize((SIZE, SIZE), Image.LANCZOS)

inner = Image.new('L', (SIZE * S, SIZE * S), 0)
ImageDraw.Draw(inner).rounded_rectangle((rim * S, rim * S, (SIZE - rim) * S - 1, (SIZE - rim) * S - 1), (radius - rim) * S, fill=255)
inner = inner.resize((SIZE, SIZE), Image.LANCZOS)

out = Image.new('RGBA', (SIZE, SIZE), (150, 205, 255, 255))   # the light rim colour
out.paste(icon, (0, 0), inner)
out.putalpha(mask)
out.save(OUT)
print('written', OUT)
