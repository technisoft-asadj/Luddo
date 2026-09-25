"""Draws the treasure-chest picture used by the menu rails and the Events screen (coloured, like the reference art).
Writes Assets/_Game/Art/UI/Icons/chest_color.png"""
from PIL import Image, ImageDraw, ImageFilter

S = 4                      # supersampling
W = 256 * S
img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

def sc(v): return int(v * S)
def box(x0, y0, x1, y1): return [sc(x0), sc(y0), sc(x1), sc(y1)]

WOOD = (150, 84, 36, 255); WOOD_D = (98, 52, 22, 255); WOOD_L = (196, 118, 56, 255)
GOLD = (255, 205, 70, 255); GOLD_D = (200, 140, 20, 255); GOLD_L = (255, 240, 160, 255)

# soft ground shadow
sh = Image.new("RGBA", (W, W), (0, 0, 0, 0)); sd = ImageDraw.Draw(sh)
sd.ellipse(box(30, 205, 226, 240), fill=(0, 0, 0, 90)); sh = sh.filter(ImageFilter.GaussianBlur(sc(5)))
img.alpha_composite(sh)

# body
d.rounded_rectangle(box(34, 118, 222, 218), radius=sc(14), fill=WOOD, outline=WOOD_D, width=sc(5))
for x in (86, 128, 170):                       # planks
    d.line([sc(x), sc(124), sc(x), sc(212)], fill=WOOD_D, width=sc(3))
d.rounded_rectangle(box(40, 124, 216, 140), radius=sc(8), fill=WOOD_L)

# lid (arched)
d.pieslice(box(34, 40, 222, 196), 180, 360, fill=WOOD, outline=WOOD_D, width=sc(5))
d.rectangle(box(36, 118, 220, 132), fill=WOOD)
d.arc(box(50, 56, 206, 180), 200, 340, fill=WOOD_L, width=sc(6))
for x in (86, 170):
    d.line([sc(x), sc(60 if x == 86 else 62), sc(x), sc(118)], fill=WOOD_D, width=sc(3))

# gold bands + rim
d.rounded_rectangle(box(34, 112, 222, 128), radius=sc(6), fill=GOLD, outline=GOLD_D, width=sc(3))
for x in (52, 186):
    d.rectangle(box(x, 112, x + 18, 218), fill=GOLD, outline=GOLD_D, width=sc(3))
    d.rectangle(box(x + 3, 116, x + 7, 214), fill=GOLD_L)

# lock plate
d.rounded_rectangle(box(108, 108, 148, 156), radius=sc(8), fill=GOLD, outline=GOLD_D, width=sc(4))
d.ellipse(box(120, 122, 136, 138), fill=WOOD_D)
d.rectangle(box(125, 134, 131, 148), fill=WOOD_D)
d.arc(box(100, 96, 156, 136), 200, 340, fill=GOLD_D, width=sc(4))   # shackle top

# shine
d.ellipse(box(72, 58, 96, 72), fill=(255, 255, 255, 80))

img = img.resize((256, 256), Image.LANCZOS)
img.save("Assets/_Game/Art/UI/Icons/chest_color.png")
print("ok")

# ---------------- gift and calendar (same style) ----------------
def canvas():
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0)); return im, ImageDraw.Draw(im)

def finish(im, name):
    im = im.resize((256, 256), Image.LANCZOS); im.save("Assets/_Game/Art/UI/Icons/" + name)

RED = (236, 64, 82, 255); RED_D = (160, 30, 50, 255); RED_L = (255, 130, 140, 255)

# gift
im, d = canvas()
d.rounded_rectangle(box(40, 110, 216, 222), radius=sc(12), fill=RED, outline=RED_D, width=sc(5))
d.rounded_rectangle(box(30, 84, 226, 124), radius=sc(10), fill=RED_L, outline=RED_D, width=sc(5))
d.rectangle(box(112, 84, 144, 222), fill=GOLD, outline=GOLD_D, width=sc(3))
d.ellipse(box(72, 30, 128, 90), fill=None, outline=GOLD_D, width=sc(12))
d.ellipse(box(128, 30, 184, 90), fill=None, outline=GOLD_D, width=sc(12))
d.ellipse(box(72, 30, 128, 90), fill=None, outline=GOLD, width=sc(7))
d.ellipse(box(128, 30, 184, 90), fill=None, outline=GOLD, width=sc(7))
d.ellipse(box(114, 66, 142, 94), fill=GOLD, outline=GOLD_D, width=sc(3))
d.rounded_rectangle(box(48, 118, 62, 214), radius=sc(6), fill=(255, 255, 255, 60))
finish(im, "gift_color.png")

# calendar
im, d = canvas()
d.rounded_rectangle(box(30, 40, 226, 222), radius=sc(22), fill=(250, 250, 255, 255), outline=(90, 70, 150, 255), width=sc(6))
d.rounded_rectangle(box(30, 40, 226, 96), radius=sc(22), fill=(150, 70, 230, 255), outline=(90, 40, 150, 255), width=sc(6))
d.rectangle(box(34, 74, 222, 96), fill=(150, 70, 230, 255))
for x in (78, 178):
    d.rounded_rectangle(box(x - 9, 22, x + 9, 62), radius=sc(8), fill=(230, 230, 245, 255), outline=(90, 40, 150, 255), width=sc(4))
cols = [(236, 64, 82), (255, 190, 40), (60, 190, 90), (60, 140, 255), (236, 64, 82), (255, 190, 40), (60, 190, 90), (60, 140, 255), (236, 64, 82)]
for i, c in enumerate(cols):
    r, q = divmod(i, 3)
    x0 = 52 + q * 52; y0 = 114 + r * 34
    d.rounded_rectangle(box(x0, y0, x0 + 40, y0 + 26), radius=sc(6), fill=c + (255,))
finish(im, "calendar_color.png")
print("ok2")

# globe with a magnifier (Quick Match)
im, d = canvas()
d.ellipse(box(22, 22, 200, 200), fill=(40, 120, 230, 255), outline=(20, 70, 160, 255), width=sc(6))
land = (70, 190, 90, 255)
d.polygon([(sc(60), sc(60)), (sc(100), sc(48)), (sc(120), sc(80)), (sc(96), sc(112)), (sc(64), sc(100))], fill=land)
d.polygon([(sc(112), sc(120)), (sc(150), sc(112)), (sc(168), sc(146)), (sc(140), sc(180)), (sc(118), sc(158))], fill=land)
d.polygon([(sc(130), sc(50)), (sc(170), sc(60)), (sc(176), sc(90)), (sc(146), sc(84))], fill=land)
d.arc(box(40, 40, 182, 182), 200, 260, fill=(255, 255, 255, 150), width=sc(8))
# magnifier
d.line([sc(166), sc(166), sc(232), sc(232)], fill=(120, 70, 30, 255), width=sc(22))
d.line([sc(166), sc(166), sc(232), sc(232)], fill=(190, 120, 50, 255), width=sc(12))
d.ellipse(box(134, 134, 214, 214), outline=(255, 205, 70, 255), width=sc(12))
d.ellipse(box(134, 134, 214, 214), outline=(200, 140, 20, 255), width=sc(3))
finish(im, "globe_color.png")
print("ok3")
