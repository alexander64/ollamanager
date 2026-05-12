#!/usr/bin/env python3
"""
Generates tray.png, AppIcon.png and AppIcon.icns for OllamaManager.
Profile view of a llama (facing right): the distinctive long snout is
much more recognisable than a front-facing face at small icon sizes.
"""

from PIL import Image, ImageDraw
import os, subprocess, shutil

ASSETS = os.path.join(os.path.dirname(__file__), 'Assets')

BG    = (11, 11, 20,  255)
WHITE = (228, 228, 248, 255)
GREEN = (48, 209, 88,  255)
CLEAR = (0, 0, 0, 0)


# ── Core shape: right-facing llama profile ────────────────────────────────────

def draw_llama_profile(d, cx, cy, s, fill, eye_fill):
    """
    Right-facing llama profile. Key feature: long thin muzzle.
    s = reference unit. cx, cy = visual centre of figure.
    """
    # ── Cranium (smaller circle leaves more visual room for the long snout)
    cr  = s * 0.295
    ccx = cx - s * 0.06
    ccy = cy - s * 0.06

    # ── Muzzle: long, thin, slightly upturned (the defining llama trait)
    mw = s * 0.90             # very long
    mh = s * 0.155            # thin
    mx = ccx + cr * 0.12      # starts near front of cranium
    my = ccy + s * 0.02       # sits just below the cranium midline
    d.rounded_rectangle([mx, my, mx + mw, my + mh],
                        radius=int(mh * 0.36), fill=fill)

    # ── Cranium (drawn after muzzle to cover the overlap neatly)
    d.ellipse([ccx - cr,       ccy - cr,
               ccx + cr,       ccy + cr], fill=fill)

    # ── Ear (narrow triangle set back on the skull)
    ecx      = ccx - cr * 0.20
    e_base_y = ccy - cr * 0.78
    e_tip_y  = e_base_y - s * 0.46
    ew       = s * 0.082
    d.polygon([
        (ecx - ew * 0.75, e_base_y),
        (ecx,             e_tip_y),
        (ecx + ew,        e_base_y),
    ], fill=fill)

    # ── Neck (slightly forward-leaning, tapered)
    ncx  = ccx - cr * 0.26
    nw_t = s * 0.21
    nw_b = s * 0.13
    nt   = ccy + cr * 0.74
    nb   = cy  + s * 0.48
    d.polygon([
        (ncx - nw_t/2, nt), (ncx + nw_t/2, nt),
        (ncx + nw_b/2, nb), (ncx - nw_b/2, nb),
    ], fill=fill)

    # ── Eye
    er = max(cr * 0.13, 2)
    ex = ccx + cr * 0.04
    ey = ccy - cr * 0.05
    d.ellipse([ex - er, ey - er, ex + er, ey + er], fill=eye_fill)


def draw_green_dot(d, dx, dy, dr):
    d.ellipse([dx-dr-5, dy-dr-5, dx+dr+5, dy+dr+5], fill=(0, 18, 4, 140))
    d.ellipse([dx-dr,   dy-dr,   dx+dr,   dy+dr],   fill=GREEN)
    hr2 = int(dr * 0.38)
    d.ellipse([dx-hr2, dy-int(dr*0.54), dx+hr2, dy],
               fill=(140, 248, 160, 115))


# ── Tray icon ─────────────────────────────────────────────────────────────────

def make_tray(path, size=44):
    """White llama profile on transparent background."""
    SS = size * 6
    img = Image.new('RGBA', (SS, SS), CLEAR)
    d   = ImageDraw.Draw(img)

    # Centre the figure: snout goes right so shift cranium left of SS/2
    draw_llama_profile(d,
                       cx=int(SS * 0.38),
                       cy=int(SS * 0.50),
                       s=int(SS * 0.52),
                       fill=(255, 255, 255, 255),
                       eye_fill=(40, 40, 40, 255))

    img.resize((size, size), Image.LANCZOS).save(path)
    print(f'  tray  → {path}  ({size}×{size})')


# ── App icon ──────────────────────────────────────────────────────────────────

def make_app_png(path, size=1024):
    SS = size * 2
    img = Image.new('RGBA', (SS, SS), CLEAR)
    d   = ImageDraw.Draw(img)

    r = int(SS * 0.225)

    # ── Background
    d.rounded_rectangle([0, 0, SS-1, SS-1], radius=r, fill=BG)

    # ── Subtle inset ring (depth cue)
    ins = int(SS * 0.014)
    d.rounded_rectangle([ins, ins, SS-ins-1, SS-ins-1],
                        radius=max(r - ins, 2),
                        fill=(17, 17, 30, 255))

    # ── Llama (centred; snout extends right so push cranium left)
    draw_llama_profile(d,
                       cx=int(SS * 0.38),
                       cy=int(SS * 0.42),
                       s=int(SS * 0.44),
                       fill=WHITE,
                       eye_fill=BG)

    # ── Green status dot (lower-right, away from snout/neck)
    dr = int(SS * 0.060)
    draw_green_dot(d, int(SS * 0.740), int(SS * 0.748), dr)

    img.resize((size, size), Image.LANCZOS).save(path)
    print(f'  app   → {path}  ({size}×{size})')


# ── ICNS bundle ───────────────────────────────────────────────────────────────

def make_icns(src_png, dest_icns):
    iconset = dest_icns.replace('.icns', '.iconset')
    os.makedirs(iconset, exist_ok=True)
    base = Image.open(src_png).convert('RGBA')
    for name, px in [
        ('icon_16x16.png',        16), ('icon_16x16@2x.png',     32),
        ('icon_32x32.png',        32), ('icon_32x32@2x.png',     64),
        ('icon_128x128.png',     128), ('icon_128x128@2x.png',  256),
        ('icon_256x256.png',     256), ('icon_256x256@2x.png',  512),
        ('icon_512x512.png',     512), ('icon_512x512@2x.png', 1024),
    ]:
        base.resize((px, px), Image.LANCZOS).save(f'{iconset}/{name}')
    subprocess.run(['iconutil', '-c', 'icns', iconset, '-o', dest_icns], check=True)
    shutil.rmtree(iconset)
    print(f'  icns  → {dest_icns}')


# ── Run ───────────────────────────────────────────────────────────────────────

print('Generating icons…')
make_tray(f'{ASSETS}/tray.png', size=44)
base = f'{ASSETS}/AppIcon_1024.png'
make_app_png(base, size=1024)
Image.open(base).resize((512, 512), Image.LANCZOS).save(f'{ASSETS}/AppIcon.png')
make_icns(base, f'{ASSETS}/AppIcon.icns')
os.remove(base)
print('Done.')
