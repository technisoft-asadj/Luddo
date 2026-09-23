"""
Play Store feature graphic (1024x500): deep blue background with a soft glow, the crown/pawns/dice emblem and the
"Ludo Fight" wordmark.   Run:  python Prototype/make_feature_graphic.py <output.png>   (needs: pillow numpy)
"""
import os
import sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, 'Assets', '_Game', 'Art', 'Welcome')
W, H = 1024, 500

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
r = np.sqrt(((xx - W / 2) / (W * 0.55)) ** 2 + ((yy - H * 0.45) / (H * 0.8)) ** 2)
t = np.clip(r, 0, 1)[..., None]
centre = np.array([0, 78, 172], np.float32)
edge = np.array([0, 40, 112], np.float32)
bg = Image.fromarray((centre * (1 - t) + edge * t).astype(np.uint8), 'RGB').convert('RGBA')

emblem = Image.open(os.path.join(ART, 'welcome_emblem.png')).convert('RGBA')
ew = 470
emblem = emblem.resize((ew, int(emblem.height * ew / emblem.width)), Image.LANCZOS)
word = Image.open(os.path.join(ART, 'welcome_wordmark.png')).convert('RGBA')
ww = 640
word = word.resize((ww, int(word.height * ww / word.width)), Image.LANCZOS)

overlap = 34                                           # the wordmark sits just over the foot of the emblem
top = (H - (emblem.height + word.height - overlap)) // 2
bg.alpha_composite(emblem, ((W - ew) // 2, top))
bg.alpha_composite(word, ((W - ww) // 2, top + emblem.height - overlap))

out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'feature_graphic.png')
bg.convert('RGB').save(out)
print('written', out)
