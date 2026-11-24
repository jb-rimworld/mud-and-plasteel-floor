#!/usr/bin/env python3
"""Mud & Plasteel Floor: own seamless terrain textures + menu icons (house style of Hearth & Harbor art pipeline).

  uv run --with resvg-py --with pillow python3 tools/art/build.py

Vanilla terrain shaders map ONE texture over 16x16 cells (uv = world / 16), so every terrain texture here is 1024x1024
(64 px per cell) and must tile seamlessly. Mud: all elements are drawn 9 times (+-1024 offsets) = torus-seamless.
Plasteel floors: a 16x16 slab grid (one slab per cell), so the repeat is exact by construction.
Output: <mod>/Common/Textures/Terrain/MPF/*.png (terrain), <mod>/Common/Textures/UI/MPF/*.png (128 px menu icons),
        tools/art/out/contact_sheet.png (+ repeat_*.png 3x3 repetition checks).
"""
import sys, io, math, random, zlib
from pathlib import Path
sys.path.insert(0, '/Users/julius/Documents/Coding/22_RimWorld/HearthHarbor/tools/art')
import resvg_py
from PIL import Image, ImageDraw, ImageFont
from hhart.style import OUT, mix, light, dark, f2

ROOT = Path(__file__).resolve().parents[2]
TEX = ROOT / "Common" / "Textures"
OUTD = Path(__file__).resolve().parent / "out"
N = 1024  # px, 16 cells x 64


def render(svg, w, h):
    b = resvg_py.svg_to_bytes(svg_string=svg, width=w, height=h)
    return Image.open(io.BytesIO(bytes(b))).convert("RGBA")


def grain(im, key, amt=2):
    rnd = random.Random(zlib.crc32(key.encode()))
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a > 200:
                d = rnd.randint(-amt, amt)
                px[x, y] = (max(0, min(255, r + d)), max(0, min(255, g + d)), max(0, min(255, b + d)), a)
    return im


# ------------------------------------------------------------------ smooth blobs / paths
def blob_d(cx, cy, r, rnd, n=14, jit=0.28, sx=1.0, sy=1.0, rot=0.0):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        rr = r * (1 + rnd.uniform(-jit, jit))
        x, y = rr * math.cos(a) * sx, rr * math.sin(a) * sy
        ca, sa = math.cos(rot), math.sin(rot)
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    mids = [((pts[i][0] + pts[(i + 1) % n][0]) / 2, (pts[i][1] + pts[(i + 1) % n][1]) / 2) for i in range(n)]
    d = "M%s %s" % (f2(mids[-1][0]), f2(mids[-1][1]))
    for i in range(n):
        d += " Q%s %s %s %s" % (f2(pts[i][0]), f2(pts[i][1]), f2(mids[i][0]), f2(mids[i][1]))
    return d + " Z"


def shifted(d, dx, dy):
    return '<g transform="translate(%s %s)"><path d="%s"/></g>' % (f2(dx), f2(dy), d)


# ================================================================== MUD
def mud_svg(seed=11):
    rnd = random.Random(seed)
    el = []  # elements (strings) in design space 0..N, drawn on a torus

    def path(d, fill, extra=""):
        el.append('<path d="%s" fill="%s" %s/>' % (d, fill, extra))

    def stroke(d, col, w, extra=""):
        el.append('<path d="%s" fill="none" stroke="%s" stroke-width="%s" stroke-linecap="round" stroke-linejoin="round" %s/>' % (d, col, f2(w), extra))

    # --- 1 wet colour patches, flat, low contrast
    tones = ["#5a4430", "#463322", "#4f3e2e", "#5c4b38", "#3f2e1f", "#53402b"]
    for _ in range(80):
        path(blob_d(rnd.uniform(0, N), rnd.uniform(0, N), rnd.uniform(50, 140), rnd, n=12, jit=0.35, sx=rnd.uniform(.8, 1.5), sy=rnd.uniform(.7, 1.1), rot=rnd.uniform(0, 3.14)), rnd.choice(tones))

    # --- 2 trampled clods: shadow, body, light crest (light from top left)
    for _ in range(95):
        cx, cy, r = rnd.uniform(0, N), rnd.uniform(0, N), rnd.uniform(14, 40)
        s = rnd.randint(0, 10 ** 6)
        r1, r2, r3 = random.Random(s), random.Random(s), random.Random(s)
        path(blob_d(cx + 3, cy + 4, r, r1, n=9, jit=.22), "#2b1f14")
        path(blob_d(cx, cy, r, r2, n=9, jit=.22), rnd.choice(["#6b5239", "#62492f", "#725a3f"]))
        path(blob_d(cx - r * .22, cy - r * .26, r * .55, r3, n=9, jit=.22), "#8a6d4b")

    # --- 3 ruts (periodic so the tile wraps): list of (axis, base, amp, k, phase, offsets)
    ruts = []

    def rut_pts(axis, base, amp, k, ph, step=14):
        pts = []
        t = -120
        while t <= N + 120:
            w = base + amp * math.sin(2 * math.pi * k * t / N + ph) + 0.35 * amp * math.sin(2 * math.pi * (k * 2 + 1) * t / N + ph * 1.7)
            pts.append((t, w) if axis == "h" else (w, t))
            t += step
        return pts

    def pstr(pts):
        return "M" + " L".join("%s %s" % (f2(a), f2(b)) for a, b in pts)

    def rand_rut(sx, sy, ang, length, steps=26):
        pts = [(sx, sy)]
        x, y = sx, sy
        for _ in range(steps):
            ang += rnd.uniform(-.22, .22)
            x += length / steps * math.cos(ang)
            y += length / steps * math.sin(ang)
            pts.append((x, y))
        return pts

    def pstr(pts):
        return "M" + " L".join("%s %s" % (f2(a), f2(b)) for a, b in pts)

    for _ in range(9):
        pts = rand_rut(rnd.uniform(0, N), rnd.uniform(0, N), rnd.uniform(0, 6.28), rnd.uniform(260, 520))
        ruts.append(("r", pts))
        d = pstr(pts)
        stroke(d, "#6e553a", 44)
        stroke(d, "#241a11", 28)
        stroke(d, "#33251a", 20)
        stroke(d, "#2a1f15", 9)
        dl = pstr([(x - 16, y - 16) for x, y in pts])
        ds = pstr([(x + 16, y + 16) for x, y in pts])
        stroke(dl, "#987952", 3.2)
        stroke(ds, "#1c1411", 3.2)

    # --- 4 puddles: brown wet water, dark rim, tan sheen
    def puddle(cx, cy, r, sx, sy, rot):
        s = rnd.randint(0, 10 ** 6)
        path(blob_d(cx, cy, r * 1.12, random.Random(s), n=11, jit=.2, sx=sx, sy=sy, rot=rot), "#2a1e14")
        path(blob_d(cx, cy, r, random.Random(s + 1), n=11, jit=.2, sx=sx, sy=sy, rot=rot), "#3b3024")
        path(blob_d(cx - r * .12 * sx, cy - r * .22 * sy, r * .66, random.Random(s + 1), n=11, jit=.2, sx=sx, sy=sy * .75, rot=rot), "#5a5a4a")
        path(blob_d(cx - r * .38 * sx, cy - r * .42 * sy, r * .24, random.Random(s + 2), n=8, jit=.2, sx=sx * 1.5, sy=sy * .5, rot=rot), "#d9d3b8")

    for axis, pts in ruts:
        for _ in range(2):
            i = rnd.randint(4, len(pts) - 5)
            x, y = pts[i]
            nx, ny = pts[i + 1]
            ang = math.atan2(ny - y, nx - x)
            puddle(x, y, rnd.uniform(18, 28), rnd.uniform(1.4, 2.0), 0.8, ang)
    for _ in range(4):
        puddle(rnd.uniform(0, N), rnd.uniform(0, N), rnd.uniform(18, 30), rnd.uniform(1.1, 1.6), rnd.uniform(.7, 1.0), rnd.uniform(0, 3.14))

    # --- 5 pebbles
    for _ in range(24):
        cx, cy, r = rnd.uniform(0, N), rnd.uniform(0, N), rnd.uniform(4, 9)
        el.append('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="#1c1411"/>' % (f2(cx + 1.5), f2(cy + 2), f2(r + 1.5), f2(r * .8 + 1.5)))
        el.append('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="%s"/>' % (f2(cx), f2(cy), f2(r), f2(r * .8), rnd.choice(["#8a7b68", "#756858", "#9b8d79"])))
        el.append('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="#c7b9a1"/>' % (f2(cx - r * .3), f2(cy - r * .3), f2(r * .4), f2(r * .25)))

    # --- 6 straw (it costs hay): shadow stroke under every stalk
    straw = ["#dcb860", "#c59c47", "#ead58f", "#b88a3a"]
    for _ in range(300):
        x, y = rnd.uniform(0, N), rnd.uniform(0, N)
        a = rnd.uniform(0, math.pi)
        L = rnd.uniform(16, 46)
        bend = rnd.uniform(-7, 7)
        ex, ey = x + L * math.cos(a), y + L * math.sin(a)
        mx, my = (x + ex) / 2 - bend * math.sin(a), (y + ey) / 2 + bend * math.cos(a)
        w = rnd.uniform(2.6, 4.2)
        d = "M%s %s Q%s %s %s %s" % (f2(x), f2(y), f2(mx), f2(my), f2(ex), f2(ey))
        stroke("M%s %s Q%s %s %s %s" % (f2(x + 1.8), f2(y + 2.4), f2(mx + 1.8), f2(my + 2.4), f2(ex + 1.8), f2(ey + 2.4)), "#1c1411", w, 'stroke-opacity="0.55"')
        col = rnd.choice(straw)
        stroke(d, col, w)
        stroke("M%s %s Q%s %s %s %s" % (f2(x - .6), f2(y - .8), f2(mx - .6), f2(my - .8), f2(ex - .6), f2(ey - .8)), "#fff1cf", 0.9, 'stroke-opacity="0.7"')

    # --- 7 fine wet specks
    for _ in range(520):
        x, y, r = rnd.uniform(0, N), rnd.uniform(0, N), rnd.uniform(1.2, 2.8)
        c = rnd.choice(["#2c2015", "#2c2015", "#8a6d4b", "#a98d66", "#1d2629"])
        el.append('<circle cx="%s" cy="%s" r="%s" fill="%s"/>' % (f2(x), f2(y), f2(r), c))

    body = "".join(el)
    uses = "".join('<use xlink:href="#t" x="%d" y="%d"/>' % (dx, dy) for dx in (-N, 0, N) for dy in (-N, 0, N))
    return ('<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 %d %d" width="%d" height="%d">'
            '<defs><g id="t">%s</g></defs><rect width="%d" height="%d" fill="#4d3a28"/>%s</svg>') % (N, N, N, N, body, N, N, uses)


# ================================================================== PLASTEEL STONE TILES
PLAS = "#a9c2cc"       # plasteel
PLAS_L = "#e3f2f7"
PLAS_D = "#27343b"
STONES = {
    "Sandstone": dict(base="#bc9f78", kind="bands", a="#a98a63", b="#d1b78f", spot="#8c7050"),
    "Granite":   dict(base="#8f8f8d", kind="speckle", a="#363636", b="#c4c4c0", spot="#9a948c"),
    "Limestone": dict(base="#c3c0ab", kind="fossil", a="#a9a58f", b="#dcd9c6", spot="#8d8975"),
    "Slate":     dict(base="#5f696f", kind="strata", a="#3a444b", b="#8795a0", spot="#262d33"),
    "Marble":    dict(base="#d9dcdc", kind="veins", a="#8e9aa3", b="#eef0f0", spot="#b8c0c6"),
    "Vacstone":  dict(base="#9aa5a8", kind="pits", a="#4a5558", b="#d4e0e2", spot="#6f7d82"),
}
S = 64


def slab_local(stone, i, j, rnd):
    """SVG for one slab in local coords (cell 64x64, seam centred on the cell border, slab area 3..61)."""
    st = STONES[stone]
    t = rnd.uniform(-.09, .08)
    base = mix(st["base"], "#ffffff", t) if t > 0 else mix(st["base"], "#000000", -t)
    x0, y0, x1, y1 = 3.2, 3.2, 60.8, 60.8
    o = []
    # groove edge then slab
    o.append('<rect x="%s" y="%s" width="%s" height="%s" rx="3.4" fill="%s"/>' % (f2(x0 - 1.3), f2(y0 - 1.3), f2(x1 - x0 + 2.6), f2(y1 - y0 + 2.6), PLAS_D))
    o.append('<rect x="%s" y="%s" width="%s" height="%s" rx="2.6" fill="%s"/>' % (f2(x0), f2(y0), f2(x1 - x0), f2(y1 - y0), base))
    o.append('<g clip-path="url(#sc)">')
    k = st["kind"]
    R = rnd
    if k == "speckle":
        for _ in range(40):
            x, y, r = R.uniform(4, 60), R.uniform(4, 60), R.uniform(1.0, 2.4)
            o.append('<circle cx="%s" cy="%s" r="%s" fill="%s"/>' % (f2(x), f2(y), f2(r), R.choice([st["a"], st["a"], st["b"], st["spot"]])))
    elif k == "bands":
        # sandstone: soft bedding blotches + fine grain, no lines
        for _ in range(6):
            o.append('<path d="%s" fill="%s" fill-opacity=".30"/>' % (blob_d(R.uniform(4, 60), R.uniform(4, 60), R.uniform(8, 16), R, n=9, jit=.3, sx=R.uniform(.9, 1.5)), R.choice([st["a"], st["b"]])))
        for _ in range(70):
            o.append('<circle cx="%s" cy="%s" r="%s" fill="%s" fill-opacity=".7"/>' % (f2(R.uniform(4, 60)), f2(R.uniform(4, 60)), f2(R.uniform(.5, 1.2)), R.choice([st["spot"], st["a"], st["b"], st["b"]])))
    elif k == "fossil":
        for _ in range(7):
            o.append('<path d="%s" fill="%s"/>' % (blob_d(R.uniform(6, 58), R.uniform(6, 58), R.uniform(5, 11), R, n=8, jit=.3), R.choice([st["a"], st["b"]])))
        for _ in range(R.randint(1, 2)):
            cx, cy, r = R.uniform(14, 50), R.uniform(14, 50), R.uniform(3.5, 5.5)
            o.append('<path d="M%s %s a%s %s 0 1 1 %s %s a%s %s 0 1 1 %s %s" fill="none" stroke="%s" stroke-width="2.4"/>' % (
                f2(cx), f2(cy), f2(r), f2(r), f2(r * .1), f2(r * 1.7), f2(r * .55), f2(r * .55), f2(-r * .9), f2(-r * .4), st["spot"]))
        o.append('<path d="M%s 3 l%s 20 l%s 14" fill="none" stroke="%s" stroke-width="1" stroke-opacity=".7"/>' % (f2(R.uniform(10, 50)), f2(R.uniform(-6, 6)), f2(R.uniform(-8, 8)), st["spot"]))
    elif k == "strata":
        y = 2.0
        flip = False
        while y < 64:
            h = R.uniform(7, 16)
            col = st["a"] if flip else st["b"]
            o.append('<polygon points="0,%s 64,%s 64,%s 0,%s" fill="%s" fill-opacity=".14"/>' % (f2(y), f2(y + R.uniform(-1.8, 1.8)), f2(y + h + R.uniform(-1.8, 1.8)), f2(y + h), col))
            y += h
            flip = not flip
        o.append('<path d="M4 %s L60 %s" stroke="%s" stroke-width="1" stroke-opacity=".25"/>' % (f2(R.uniform(10, 50)), f2(R.uniform(10, 50)), st["spot"]))
    elif k == "veins":
        for n_ in range(R.randint(1, 2)):
            x, y = (R.uniform(0, 64), 0) if n_ % 2 == 0 else (0, R.uniform(0, 64))
            pts = [(x, y)]
            ang = R.uniform(.5, 1.1) if n_ % 2 == 0 else R.uniform(-.4, .5)
            for _ in range(9):
                ang += R.uniform(-.5, .5)
                x, y = x + 8 * math.cos(ang), y + 8 * math.sin(ang)
                pts.append((x, y))
            d = "M" + " L".join("%s %s" % (f2(a), f2(b)) for a, b in pts)
            o.append('<path d="%s" fill="none" stroke="%s" stroke-width="8" stroke-opacity=".35" stroke-linejoin="round"/>' % (d, st["spot"]))
            o.append('<path d="%s" fill="none" stroke="%s" stroke-width="%s" stroke-opacity=".55" stroke-linejoin="round"/>' % (d, st["a"], f2(R.uniform(1.4, 2.2))))
        for _ in range(5):
            o.append('<path d="%s" fill="%s" fill-opacity=".6"/>' % (blob_d(R.uniform(6, 58), R.uniform(6, 58), R.uniform(6, 12), R, n=8, jit=.3), st["b"]))
    elif k == "pits":
        for _ in range(9):
            x, y, r = R.uniform(8, 56), R.uniform(8, 56), R.uniform(3.4, 7)
            ry = r * R.uniform(.6, .9)
            o.append('<ellipse cx="%s" cy="%s" rx="%s" ry="%s" fill="%s"/>' % (f2(x), f2(y), f2(r), f2(ry), st["a"]))
            o.append('<path d="M%s %s a%s %s 0 0 1 %s %s" fill="none" stroke="%s" stroke-width="1.8"/>' % (f2(x - r + .5), f2(y + .5), f2(r), f2(ry), f2(r * .9), f2(-ry), st["b"]))
        for _ in range(8):
            o.append('<circle cx="%s" cy="%s" r="%s" fill="%s"/>' % (f2(R.uniform(5, 59)), f2(R.uniform(5, 59)), f2(R.uniform(.7, 1.3)), st["spot"]))
    o.append('</g>')
    # bevel: light top/left, shadow bottom/right (inside edge)
    li, dk = light(base, .26), dark(base, .30)
    o.append('<path d="M5.4 %s V5.4 H%s" fill="none" stroke="%s" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>' % (f2(y1 - 2.2), f2(x1 - 2.2), li))
    o.append('<path d="M%s 5.4 V%s H5.4" fill="none" stroke="%s" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>' % (f2(x1 - 2.2), f2(y1 - 2.2), dk))
    # at most a few very subtle cracks
    if R.random() < .03:
        x, y = R.uniform(10, 54), 4
        pts = [(x, y)]
        for _ in range(6):
            x += R.uniform(-6, 6); y += R.uniform(7, 10)
            pts.append((x, y))
        d = "M" + " L".join("%s %s" % (f2(a), f2(b)) for a, b in pts)
        o.append('<path d="%s" fill="none" stroke="%s" stroke-width="1.4" stroke-opacity=".35" stroke-linejoin="round" clip-path="url(#sc)"/>' % (d, PLAS_D))
    return "".join(o)


def stone_svg(stone):
    rnd = random.Random(zlib.crc32(stone.encode()))
    parts = []
    # seam: plasteel strip along every cell border, centre bright line
    parts.append('<rect width="%d" height="%d" fill="%s"/>' % (N, N, PLAS))
    for i in range(16):
        for j in range(16):
            parts.append('<g transform="translate(%d %d)">%s</g>' % (i * S, j * S, slab_local(stone, i, j, rnd)))
    # seam highlight lines and studs at crossings (centres on grid lines, wrap via the periodic grid)
    for g in range(16):
        p = g * S
        parts.append('<path d="M%d 0 V%d" stroke="%s" stroke-width="1.8" stroke-opacity=".9"/>' % (p - 1, N, PLAS_L))
        parts.append('<path d="M0 %d H%d" stroke="%s" stroke-width="1.8" stroke-opacity=".9"/>' % (p - 1, N, PLAS_L))
    for gx in range(16):
        for gy in range(16):
            if (gx + gy) % 2:
                continue
            cx, cy = gx * S, gy * S
            for ox in ((0, N) if gx == 0 else (0,)):
                for oy in ((0, N) if gy == 0 else (0,)):
                    x, y = cx + ox, cy + oy
                    def dia(r):
                        return "M%d %s L%s %d L%d %s L%s %d Z" % (x, f2(y - r), f2(x + r), y, x, f2(y + r), f2(x - r), y)
                    parts.append('<path d="%s" fill="%s" stroke="%s" stroke-width="2.4" stroke-linejoin="round"/>' % (dia(11.5), PLAS, PLAS_D))
                    parts.append('<path d="M%s %s L%d %s" fill="none" stroke="%s" stroke-width="1.8" stroke-linecap="round"/>' % (f2(x - 7), f2(y - 0.5), x - 1, f2(y - 6.5), PLAS_L))
    clip = '<clipPath id="sc"><rect x="3.2" y="3.2" width="57.6" height="57.6" rx="2.6"/></clipPath>'
    return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d"><defs>%s</defs>%s</svg>' % (N, N, N, N, clip, "".join(parts)))


# ================================================================== icons / sheets
def make_icon(src, box, key):
    im = src.crop(box).resize((128, 128), Image.LANCZOS) if box[2] - box[0] != 128 else src.crop(box)
    mask = Image.new("L", (128, 128), 0)
    ImageDraw.Draw(mask).rounded_rectangle([3, 3, 124, 124], radius=12, fill=255)
    out = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    out.paste(im, (0, 0), mask)
    d = ImageDraw.Draw(out)
    d.rounded_rectangle([3, 3, 124, 124], radius=12, outline=(28, 20, 17, 255), width=4)
    return out


def tile3(im):
    s = Image.new("RGBA", (im.width * 3, im.height * 3))
    for a in range(3):
        for b in range(3):
            s.paste(im, (a * im.width, b * im.height))
    return s


def main():
    names = []
    OUTD.mkdir(parents=True, exist_ok=True)
    (TEX / "Terrain/MPF").mkdir(parents=True, exist_ok=True)
    (TEX / "UI/MPF").mkdir(parents=True, exist_ok=True)
    mud = grain(render(mud_svg(), N, N), "mud", 3)
    mud.convert("RGB").save(TEX / "Terrain/MPF/Mud.png", optimize=True)
    names.append(("Mud", mud))
    ib = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (90, 230)
    make_icon(mud, (ib[0], ib[1], ib[0] + 128, ib[1] + 128), "mud").save(TEX / "UI/MPF/Mud_Icon.png", optimize=True)
    for s in STONES:
        im = grain(render(stone_svg(s), N, N), s, 2)
        im.convert("RGB").save(TEX / ("Terrain/MPF/Plasteel%s.png" % s), optimize=True)
        make_icon(im, (0, 0, 128, 128), s).save(TEX / ("UI/MPF/Plasteel%s_Icon.png" % s), optimize=True)
        names.append((s, im))
    # contact sheet: textures at 0.5x (512 px, 32 ppc), 3x2, on neutral floor; plus 3x3 repetition crops
    cs = Image.new("RGB", (512 * 4 + 50, 512 * 2 + 30 + 20 + 128 + 30), (150, 128, 96))
    for k, (n, im) in enumerate(names):
        x, y = 10 + (k % 4) * 522, 10 + (k // 4) * 522
        cs.paste(im.resize((512, 512), Image.LANCZOS).convert("RGB"), (x, y))
    for k, (n, _) in enumerate(names):
        p = TEX / ("UI/MPF/%s_Icon.png" % ("Mud" if n == "Mud" else "Plasteel" + n))
        ic = Image.open(p)
        cs.paste(ic, (10 + k * 140, 512 * 2 + 40), ic)
    cs.save(OUTD / "contact_sheet.png")
    for n, im in names:
        t = tile3(im.convert("RGB"))
        t.crop((N // 2, N // 2, N // 2 + 1500, N // 2 + 1500)).resize((1000, 1000), Image.LANCZOS).save(OUTD / ("repeat_%s.png" % n))
    # native 64 px/cell detail crops (what the player sees at zoom 1.0)
    for n, im in names:
        im.crop((0, 0, 512, 512)).convert("RGB").save(OUTD / ("detail_%s.png" % n))
    print("ok", [n for n, _ in names])


if __name__ == "__main__":
    main()
