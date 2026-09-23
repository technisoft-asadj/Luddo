"""
Builds the art for the first (welcome / login) page from the designer's prototype 'login_signup page.png'.
Run:  python Prototype/make_welcome_art.py     (needs: pillow numpy opencv-python-headless)
Writes into Assets/_Game/Art/Welcome/:
  welcome_bg.png        tall background (prototype corners kept, all text/buttons/logo painted out)
  welcome_emblem.png    crown + pawns + dice cut from the prototype (the name is NOT in it)
  welcome_wordmark.png  the name "Ludo Fight" drawn in the game font in the prototype's style
Only the wordmark:  python Prototype/make_welcome_art.py wordmark
  icon_google.png       Google "G"
  icon_facebook.png     Facebook "f"
"""
import os
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', '_Game', 'Art', 'Welcome')
FONT = os.path.join(ROOT, 'Assets', 'ThirdParty', 'Fonts', 'LilitaOne', 'LilitaOne-Regular.ttf')
os.makedirs(OUT, exist_ok=True)

src = Image.open(os.path.join(ROOT, 'Prototype', 'login_signup page.png')).convert('RGB')
W, H = src.size          # 941 x 1672


# ---------------------------------------------------------------- background
def clean_background():
    """The prototype with the logo, texts, buttons, arrow and gear painted out (only the soft blue and the corner decorations remain)."""
    arr = np.array(src)
    bgr = cv2.cvtColor(arr, cv2.COLOR_RGB2BGR).astype(np.float32)
    out = bgr.copy()
    band0, band1 = 300, 1380

    # 1. the whole middle band: a smooth blend of the left and right edge colours of every row, plus a soft glow in the middle
    left = bgr[:, 2:30].mean(axis=1)
    right = bgr[:, W - 30:W - 2].mean(axis=1)
    left = cv2.GaussianBlur(left.reshape(H, 1, 3), (1, 81), 0).reshape(H, 3)
    right = cv2.GaussianBlur(right.reshape(H, 1, 3), (1, 81), 0).reshape(H, 3)
    t = np.linspace(0, 1, W).reshape(1, W, 1)
    band = left[band0:band1, None, :] * (1 - t) + right[band0:band1, None, :] * t
    yy, xx = np.mgrid[band0:band1, 0:W]
    glow = np.exp(-(((xx - W / 2) / 360.0) ** 2 + ((yy - 700) / 330.0) ** 2))[..., None]
    band = band + glow * np.array([70, 42, 0], np.float32).reshape(1, 1, 3)        # BGR: a little more blue/green in the middle
    out[band0:band1] = band

    # 2. the area above the wordmark (crown, pawns, dice, rainbow arc): a vertical blend from the clean rows above it into the band below
    y0, y1 = 100, 330
    top = cv2.GaussianBlur(bgr[84:104].mean(axis=0).reshape(1, W, 3), (0, 0), 40).reshape(W, 3)
    bottom = cv2.GaussianBlur(out[y1 + 4:y1 + 12].mean(axis=0).reshape(1, W, 3), (0, 0), 40).reshape(W, 3)
    fill = out.copy()
    for y in range(y0, y1):
        k = (y - y0) / float(y1 - y0)
        k = k * k * (3 - 2 * k)
        fill[y] = top * (1 - k) + bottom * k
    m = np.zeros((H, W), np.float32)
    m[y0:y1, 262:745] = 1.0
    m = cv2.GaussianBlur(m, (0, 0), 30)[..., None]
    out = out * (1 - m) + fill * m

    # 3. small spots at the edges (arrow, gear) and the terms line: inpaint
    spots = np.zeros((H, W), np.uint8)
    spots[25:118, 25:118] = 255
    spots[25:118, 825:918] = 255
    spots[1415:1495, 270:670] = 255
    spots = cv2.dilate(spots, np.ones((7, 7), np.uint8))
    out = cv2.inpaint(np.clip(out, 0, 255).astype(np.uint8), spots, 9, cv2.INPAINT_TELEA).astype(np.float32)
    soft = cv2.GaussianBlur(out, (0, 0), 10)
    m = cv2.GaussianBlur(spots.astype(np.float32) / 255.0, (0, 0), 5)[..., None]
    out = out * (1 - m) + soft * m
    return cv2.cvtColor(np.clip(out, 0, 255).astype(np.uint8), cv2.COLOR_BGR2RGB)


def tall_background(clean, tall_h):
    top_h, bottom_h = 310, 300
    top = clean[:top_h]
    bottom = clean[H - bottom_h:]
    middle = clean[top_h:H - bottom_h]
    mid_h = tall_h - top_h - bottom_h
    middle = cv2.resize(middle, (W, mid_h), interpolation=cv2.INTER_CUBIC)
    tall = np.concatenate([top, middle, bottom], axis=0)
    # blend the two joins
    for join in (top_h, top_h + mid_h):
        a = max(0, join - 20); b = min(tall.shape[0], join + 20)
        band = cv2.GaussianBlur(tall[a:b], (0, 0), 8)
        tall[a:b] = band
    return Image.fromarray(tall)


# ---------------------------------------------------------------- emblem (crown, pawns, dice)
def emblem():
    x0, y0, x1, y1 = 214, 112, 728, 376
    crop = src.crop((x0, y0, x1, y1)).convert('RGBA')
    w, h = crop.size
    a = np.ones((h, w), np.float32)
    feather = 46
    xs = np.minimum(np.arange(w), np.arange(w)[::-1]).astype(np.float32)
    ys = np.arange(h).astype(np.float32)
    fx = np.clip(xs / feather, 0, 1)[None, :]
    fy_top = np.clip(ys / feather, 0, 1)[:, None]
    fy_bottom = np.clip((h - 1 - ys) / 30.0, 0, 1)[:, None]
    a = a * fx * fy_top * fy_bottom
    a = a * a * (3 - 2 * a)
    arr = np.array(crop)
    arr[..., 3] = (a * 255).astype(np.uint8)
    return Image.fromarray(arr, 'RGBA')


# ---------------------------------------------------------------- wordmark
def dilate(mask, r):
    m = mask.filter(ImageFilter.GaussianBlur(r * 0.55))
    return m.point(lambda v: 255 if v > 28 else 0).filter(ImageFilter.GaussianBlur(1.1))


def vgradient(size, top, bottom):
    w, h = size
    t = np.linspace(0, 1, h).reshape(h, 1, 1)
    top = np.array(top, np.float32).reshape(1, 1, 3)
    bottom = np.array(bottom, np.float32).reshape(1, 1, 3)
    arr = (top * (1 - t) + bottom * t) * np.ones((1, w, 1), np.float32)
    return Image.fromarray(arr.astype(np.uint8), 'RGB').convert('RGBA')


def wordmark(text_a='Ludo', text_b='Fight', gap=0.22):
    S = 2                                        # draw at 2x and keep it (the UI scales it down)
    pad = 70
    font = ImageFont.truetype(FONT, 250 * S // 2 + 60)
    wa = font.getlength(text_a) + font.size * gap; wb = font.getlength(text_b)
    tw = int(wa + wb)
    bbox = font.getbbox(text_a + text_b)
    th = bbox[3] - bbox[1]
    W2, H2 = tw + pad * 2, th + pad * 2 + 40
    def text_mask(txt, x):
        m = Image.new('L', (W2, H2), 0)
        ImageDraw.Draw(m).text((x, pad - bbox[1]), txt, font=font, fill=255)
        return m
    mask_a = text_mask(text_a, pad)
    mask_b = text_mask(text_b, pad + wa)
    mask = ImageChops.lighter(mask_a, mask_b)

    canvas = Image.new('RGBA', (W2, H2), (0, 0, 0, 0))
    # soft shadow
    shadow = dilate(mask, 40).filter(ImageFilter.GaussianBlur(16))
    sh = Image.new('RGBA', (W2, H2), (0, 10, 60, 150)); sh.putalpha(shadow.point(lambda v: int(v * 0.55)))
    canvas.alpha_composite(sh, (0, 26))
    # outer light-blue rim, dark navy outline
    rim = dilate(mask, 46)
    canvas.alpha_composite(Image.composite(Image.new('RGBA', (W2, H2), (70, 170, 255, 255)), Image.new('RGBA', (W2, H2), (0, 0, 0, 0)), rim))
    outline = dilate(mask, 34)
    canvas.alpha_composite(Image.composite(Image.new('RGBA', (W2, H2), (8, 32, 110, 255)), Image.new('RGBA', (W2, H2), (0, 0, 0, 0)), outline))
    # 3D lip under the letters
    for m, lip in ((mask_a, (214, 112, 0)), (mask_b, (150, 12, 24))):
        shifted = ImageChops.offset(m, 0, 16)
        shifted = dilate(shifted, 4)
        canvas.alpha_composite(Image.composite(Image.new('RGBA', (W2, H2), lip + (255,)), Image.new('RGBA', (W2, H2), (0, 0, 0, 0)), shifted))
    # letter faces with a vertical gradient
    top_y = pad; bottom_y = pad + th
    for m, top, bottom in ((mask_a, (255, 232, 110), (255, 150, 10)), (mask_b, (255, 140, 120), (225, 28, 44))):
        grad = vgradient((W2, H2), top, bottom)
        # gradient only spans the text height
        ga = np.array(grad)
        t = np.clip((np.arange(H2) - top_y) / max(1, (bottom_y - top_y)), 0, 1).reshape(H2, 1, 1)
        top_a = np.array(top, np.float32).reshape(1, 1, 3); bot_a = np.array(bottom, np.float32).reshape(1, 1, 3)
        ga[..., :3] = ((top_a * (1 - t) + bot_a * t) * np.ones((1, W2, 1))).astype(np.uint8)
        face = Image.fromarray(ga, 'RGBA')
        canvas.alpha_composite(Image.composite(face, Image.new('RGBA', (W2, H2), (0, 0, 0, 0)), m))
    # glossy highlight on the upper part of every letter
    gloss = Image.new('L', (W2, H2), 0)
    gd = ImageDraw.Draw(gloss)
    gd.rectangle((0, top_y + int(th * 0.06), W2, top_y + int(th * 0.40)), fill=110)
    gloss = gloss.filter(ImageFilter.GaussianBlur(10))
    gloss = ImageChops.multiply(gloss, mask)
    white = Image.new('RGBA', (W2, H2), (255, 255, 255, 255)); white.putalpha(gloss)
    canvas.alpha_composite(white)
    bbox2 = canvas.getbbox()
    return canvas.crop(bbox2)


# ---------------------------------------------------------------- icons
def google_g(size=512):
    S = 4
    n = size * S
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = n / 2
    R = n * 0.47
    t = n * 0.20                      # ring thickness
    box = (cx - R, cy - R, cx + R, cy + R)
    # PIL angles: 0 = right (3 o'clock), clockwise. Google G: red top, yellow left, green bottom, blue right-lower.
    d.arc(box, 205, 320, fill=(234, 67, 53, 255), width=int(t))        # red: upper left over the top to the right
    d.arc(box, 140, 205, fill=(251, 188, 5, 255), width=int(t))        # yellow: left
    d.arc(box, 50, 140, fill=(52, 168, 83, 255), width=int(t))         # green: bottom
    d.arc(box, 0, 50, fill=(66, 133, 244, 255), width=int(t))          # blue: lower right up to the bar
    # blue bar
    d.rectangle((cx - n * 0.02, cy - t * 0.5, cx + R, cy + t * 0.5), fill=(66, 133, 244, 255))
    # cut the opening on the upper right (between red and the bar)
    mask = Image.new('L', (n, n), 255)
    md = ImageDraw.Draw(mask)
    md.polygon([(cx, cy - t * 0.5), (cx + R + 10, cy - t * 0.5), (cx + R + 10, cy - R - 10), (cx + R * 0.35, cy - R - 10)], fill=0)
    a = ImageChops.multiply(img.getchannel('A'), mask)
    # keep the red arc end: redraw the red end piece above the bar level
    img.putalpha(a)
    d2 = ImageDraw.Draw(img)
    d2.arc(box, 285, 320, fill=(234, 67, 53, 255), width=int(t))
    return img.resize((size, size), Image.LANCZOS)


def facebook_f(size=512):
    S = 4
    n = size * S
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((0, 0, n - 1, n - 1), fill=(255, 255, 255, 255))
    font = ImageFont.truetype(r'C:\Windows\Fonts\arialbd.ttf', int(n * 1.3))
    layer = Image.new('L', (n, n), 0)
    ld = ImageDraw.Draw(layer)
    bb = font.getbbox('f')
    fx = (n - (bb[2] - bb[0])) / 2 - bb[0] + n * 0.05
    fy = n * 0.14 - bb[1]
    ld.text((fx, fy), 'f', font=font, fill=255)
    circle = Image.new('L', (n, n), 0)
    ImageDraw.Draw(circle).ellipse((0, 0, n - 1, n - 1), fill=255)
    layer = ImageChops.multiply(layer, circle)
    blue = Image.new('RGBA', (n, n), (24, 119, 242, 255))
    img.alpha_composite(Image.composite(blue, Image.new('RGBA', (n, n), (0, 0, 0, 0)), layer))
    return img.resize((size, size), Image.LANCZOS)


if __name__ == '__main__':
    import sys
    if sys.argv[1:] == ['wordmark']:
        wordmark().save(os.path.join(OUT, 'welcome_wordmark.png'))
        sys.exit(0)
    clean = clean_background()
    tall_h = int(round(W * 2436 / 1080))
    tall_background(clean, tall_h).save(os.path.join(OUT, 'welcome_bg.png'))
    emblem().save(os.path.join(OUT, 'welcome_emblem.png'))
    wordmark().save(os.path.join(OUT, 'welcome_wordmark.png'))
    google_g().save(os.path.join(OUT, 'icon_google.png'))
    facebook_f().save(os.path.join(OUT, 'icon_facebook.png'))
    print('written to', OUT)
