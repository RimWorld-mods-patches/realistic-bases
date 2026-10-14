#!/usr/bin/env python3
"""Render planner ASCII layout exports as colour maps and compare two runs.

Subcommands:
  png    BEFORE_DIR AFTER_DIR OUT_DIR           one before|after PNG per case
  sheet  PNG_DIR OUT.png CASE [CASE ...]        stack chosen case PNGs vertically
  page   BEFORE_DIR AFTER_DIR OUT.html [--before-matrix F] [--after-matrix F]
         [--before-label L] [--after-label L] [--title T]
                                                one self-contained HTML page

BEFORE_DIR/AFTER_DIR hold the `--export` text files of the layout tests
(`v2m-<case>.txt` from the V2 matrix). Either side may be a single-run
directory; cases missing from BEFORE_DIR render after-only.
"""
import base64
import html
import io
import os
import re
import sys

from PIL import Image, ImageDraw

CELL = {
    '#': (70, 70, 80), '^': (120, 105, 90), 'G': (220, 40, 40), 'D': (240, 170, 40), '=': (150, 200, 255),
    ',': (205, 225, 170), '.': (245, 240, 225), 'i': (245, 240, 225), 'Y': (255, 220, 60), 'w': (255, 240, 160),
    'x': (180, 120, 200), 'g': (220, 90, 40), '|': (110, 60, 60), 'v': (240, 140, 140), 's': (240, 200, 200),
    '*': (250, 210, 210), '"': (120, 200, 90), 'F': (90, 170, 60), '@': (230, 30, 160), 'b': (140, 100, 60),
    'U': (200, 0, 0), 'M': (160, 60, 60), ' ': (232, 236, 226), ':': (200, 200, 200), '~': (80, 140, 220),
    'o': (150, 150, 150), '%': (185, 190, 180), 'X': (0, 0, 0),
}
FURNITURE = (205, 180, 140)
# Room floors by cluster: food warm, stores blue, work brown, housing purple.
ROOM = {
    'dining': (255, 150, 120), 'kitchen': (255, 150, 120), 'freezer': (150, 210, 255), 'pantry': (150, 210, 255),
    'storage': (200, 170, 120), 'workshop': (200, 170, 120), 'laboratory': (200, 170, 120),
    'barracks': (190, 160, 230), 'bedroom': (190, 160, 230), 'power': (255, 230, 120),
    'medical': (150, 230, 190), 'jail': (160, 160, 160), 'mortar-magazine': (160, 160, 160),
}
LEGEND = [('Corridor', CELL['=']), ('Dining, kitchen', ROOM['dining']), ('Freezer, pantry', ROOM['freezer']),
          ('Storage, workshop, lab', ROOM['storage']), ('Housing', ROOM['barracks']), ('Medical', ROOM['medical']),
          ('Power yard', CELL['Y']), ('Yard inside walls', CELL[',']), ('Door', CELL['D']), ('Wall', CELL['#']),
          ('Field', CELL['F'])]
ROOM_LINE = re.compile(r'^  (\S+)\s+bounds=(-?\d+),(-?\d+),(\d+)x(\d+)\s+door=(-?\d+),(-?\d+)')
MATRIX_LINE = re.compile(r'^\s+(\S+)\s+ok=(\d+)/(\d+) shared=\d+ overhead=\S+ walled=(\S+)(?: foodWalk=(\d+))?')


def parse(path):
    """Map rows and room records from one export file."""
    lines = open(path).read().split('\n')
    end = next(i for i, l in enumerate(lines) if l.strip() == 'roof view:')
    start = max(i for i, l in enumerate(lines[:end])
                if re.match(r'^  (note:|power:|\S+\s+bounds=)|^digest=', l)) + 1
    rows = lines[start:end]
    while rows and not rows[0].strip():
        rows.pop(0)
    while rows and not rows[-1].strip():
        rows.pop()
    rooms = []
    for l in lines[:end]:
        m = ROOM_LINE.match(l)
        if m:
            rooms.append((m[1],) + tuple(int(v) for v in m.groups()[1:]))
    return rows, rooms


def locate(rows, rooms):
    """Offset (and z direction) that puts every room door on a 'D' cell of the cropped map."""
    doors = [(x, z) for z, r in enumerate(rows) for x, ch in enumerate(r) if ch == 'D']
    if not rooms:
        return None
    _, _, _, _, _, dx, dz = rooms[0]
    for flip in (1, -1):
        for x, z in doors:
            ox, oz = x - dx, z - flip * dz
            if all(0 <= oz + flip * r[6] < len(rows) and 0 <= ox + r[5] < len(rows[oz + flip * r[6]]) and
                   rows[oz + flip * r[6]][ox + r[5]] == 'D' for r in rooms):
                return ox, oz, flip
    return None


def render(rows, rooms, cell=7, label=True):
    width = max(len(r) for r in rows)
    img = Image.new('RGB', (width * cell, len(rows) * cell), (255, 255, 255))
    draw = ImageDraw.Draw(img)

    def paint(x, z, colour):
        draw.rectangle([x * cell, z * cell, x * cell + cell - 1, z * cell + cell - 1], fill=colour)

    for z, r in enumerate(rows):
        for x, ch in enumerate(r.ljust(width)):
            paint(x, z, CELL.get(ch, FURNITURE))
    placed = locate(rows, rooms)
    if placed:
        ox, oz, flip = placed
        for name, bx, bz, bw, bh, _, _ in rooms:
            colour = ROOM.get(re.sub(r'-\d+$', '', name), (220, 220, 220))
            for zz in range(bz + 1, bz + bh - 1):
                for xx in range(bx + 1, bx + bw - 1):
                    X, Z = ox + xx, oz + flip * zz
                    ch = rows[Z][X] if X < len(rows[Z]) else ' '
                    # Floor in the cluster colour, furniture a shade darker.
                    paint(X, Z, colour if ch in '.i' else tuple(int(v * 0.75) for v in colour))
            if label:
                cx, cz = ox + bx + bw // 2, oz + flip * (bz + bh // 2)
                draw.text((cx * cell - 10, cz * cell - 4), name[:4], fill=(0, 0, 0))
    return img


def titled(img, title):
    out = Image.new('RGB', (img.width, img.height + 18), (255, 255, 255))
    out.paste(img, (0, 18))
    ImageDraw.Draw(out).text((3, 3), title, fill=(0, 0, 0))
    return out


def side_by_side(a, b, gap=20):
    out = Image.new('RGB', (a.width + gap + b.width, max(a.height, b.height)), (255, 255, 255))
    out.paste(a, (0, 0))
    out.paste(b, (a.width + gap, 0))
    return out


def cases(after_dir):
    return sorted(f for f in os.listdir(after_dir) if f.endswith('.txt'))


def case_name(f):
    return re.sub(r'^v2m-', '', f[:-4])


def cmd_png(before_dir, after_dir, out_dir, before_label='BEFORE', after_label='AFTER'):
    os.makedirs(out_dir, exist_ok=True)
    for f in cases(after_dir):
        name = case_name(f)
        after = titled(render(*parse(os.path.join(after_dir, f))), name + '  ' + after_label)
        before_path = os.path.join(before_dir, f)
        img = after if not os.path.exists(before_path) else side_by_side(
            titled(render(*parse(before_path)), name + '  ' + before_label), after)
        img.save(os.path.join(out_dir, name + '.png'))
        print(name)


def cmd_sheet(png_dir, out, names):
    images = [Image.open(os.path.join(png_dir, n + '.png')) for n in names]
    sheet = Image.new('RGB', (max(i.width for i in images), sum(i.height + 10 for i in images)), (255, 255, 255))
    y = 0
    for i in images:
        sheet.paste(i, (0, y))
        y += i.height + 10
    sheet.save(out)
    print(out, sheet.size)


def matrix(path):
    """case -> (walled average, foodWalk average or None, ok, total) from a V2 matrix log."""
    result = {}
    if path:
        for l in open(path):
            m = MATRIX_LINE.match(l)
            if m:
                result[m[1]] = (float(m[4]), int(m[5]) if m[5] else None, int(m[2]), int(m[3]))
    return result


def data_uri(img):
    buf = io.BytesIO()
    img.save(buf, 'PNG', optimize=True)
    return 'data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode()


def delta(a, b):
    if a is None or b is None or a == 0:
        return ''
    d = (b - a) / a * 100
    cls = 'flat' if abs(d) < 0.5 else 'good' if d < 0 else 'bad'
    return f' <span class="d {cls}">{d:+.0f}%</span>'


PAGE_CSS = """
/* Layout: summary strip, legend, case links, then one before/after pair per case */
:root { --bg:#eef0ea; --panel:#f8f9f5; --fg:#1f2421; --muted:#5d665f; --line:#cfd5cc; --good:#2f7d4f; --bad:#b4472f;
  --display:"Archivo", "Helvetica Neue", Arial, sans-serif; --body:"Source Sans 3", "Segoe UI", sans-serif;
  --mono:"JetBrains Mono", ui-monospace, Menlo, monospace; }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { --bg:#171a18; --panel:#202421; --fg:#e6ebe5;
  --muted:#9aa49c; --line:#343a35; --good:#6cc28f; --bad:#e58a72; color-scheme:dark } }
:root[data-theme="dark"] { --bg:#171a18; --panel:#202421; --fg:#e6ebe5; --muted:#9aa49c; --line:#343a35;
  --good:#6cc28f; --bad:#e58a72; color-scheme:dark }
body { background:var(--bg); color:var(--fg); font:16px/1.5 var(--body); padding-block:28px 48px;
  padding-left:max(16px, env(safe-area-inset-left, 0px)); padding-right:max(16px, env(safe-area-inset-right, 0px)); }
main { max-width:1180px; margin:0 auto; display:grid; gap:28px; }
h1 { font:700 2rem/1.15 var(--display); margin:0; text-wrap:balance; }
.lede { color:var(--muted); max-width:65ch; margin:6px 0 0; }
.summary { display:flex; flex-wrap:wrap; gap:12px; }
.stat { background:var(--panel); border:1px solid var(--line); border-radius:6px; padding:12px 16px; min-width:0; flex:1 1 220px; }
.stat b { display:block; font:600 .75rem var(--body); letter-spacing:.06em; text-transform:uppercase; color:var(--muted); }
.stat .v { font:600 1.35rem var(--mono); font-variant-numeric:tabular-nums; }
.legend { display:flex; flex-wrap:wrap; gap:8px 16px; font-size:.9rem; color:var(--muted); }
.legend i { display:inline-block; width:12px; height:12px; border-radius:2px; vertical-align:-1px; margin-right:6px; border:1px solid rgba(0,0,0,.15); }
nav { display:flex; flex-wrap:wrap; gap:6px; font:400 .8rem var(--mono); }
nav a { color:var(--fg); text-decoration:none; border:1px solid var(--line); border-radius:4px; padding:2px 7px; }
nav a:hover, nav a:focus-visible { border-color:var(--fg); outline:none; }
.case { border-top:1px solid var(--line); padding-top:18px; display:grid; gap:12px; }
.case header { display:flex; flex-wrap:wrap; justify-content:space-between; align-items:baseline; gap:8px 24px; }
h2 { font:700 1.15rem var(--display); margin:0; }
dl { display:flex; flex-wrap:wrap; gap:4px 20px; margin:0; font:400 .85rem var(--mono); font-variant-numeric:tabular-nums; }
dt { display:inline; color:var(--muted); margin-right:6px; } dd { display:inline; margin:0; }
.d { font-weight:600; margin-left:4px; } .d.good { color:var(--good); } .d.bad { color:var(--bad); } .d.flat { color:var(--muted); }
.pair { display:grid; grid-template-columns:repeat(auto-fit, minmax(280px, 1fr)); gap:16px; align-items:start; }
figure { margin:0; background:var(--panel); border:1px solid var(--line); border-radius:6px; padding:10px; min-width:0; }
figcaption { font:600 .75rem var(--body); letter-spacing:.06em; text-transform:uppercase; color:var(--muted); margin-bottom:8px; }
figure img { display:block; width:100%; height:auto; image-rendering:pixelated; border-radius:3px; }
"""


def cmd_page(before_dir, after_dir, out, before_matrix=None, after_matrix=None,
             before_label='Before', after_label='After', title='Layout Comparison'):
    mb, ma = matrix(before_matrix), matrix(after_matrix)
    esc = html.escape
    sections, links = [], []
    for f in cases(after_dir):
        name = case_name(f)
        figures = []
        before_path = os.path.join(before_dir, f)
        if os.path.exists(before_path):
            figures.append((before_label, render(*parse(before_path), cell=6)))
        figures.append((after_label, render(*parse(os.path.join(after_dir, f)), cell=6)))
        stats = []
        b, a = mb.get(name), ma.get(name)
        if a and a[1] is not None:
            stats.append(('Food walk', (f'{b[1]} → ' if b and b[1] is not None else '') + str(a[1]) +
                          (delta(b[1], a[1]) if b and b[1] is not None else '')))
        if a:
            stats.append(('Walled / room cell', (f'{b[0]:.2f} → ' if b else '') + f'{a[0]:.2f}' + (delta(b[0], a[0]) if b else '')))
        dl = ''.join(f'<div><dt>{k}</dt><dd>{v}</dd></div>' for k, v in stats)
        figs = ''.join(f'<figure><figcaption>{esc(l)}</figcaption><img src="{data_uri(i)}" alt="{esc(name)} {esc(l)}"></figure>'
                       for l, i in figures)
        sections.append(f'<section class="case" id="{esc(name)}"><header><h2>{esc(name)}</h2><dl>{dl}</dl></header>'
                        f'<div class="pair">{figs}</div></section>')
        links.append(f'<a href="#{esc(name)}">{esc(name)}</a>')
    summary = []
    if ma:
        food_a = sum(v[1] or 0 for v in ma.values())
        walled_a = sum(v[0] for v in ma.values()) / len(ma)
        ok_a, total_a = sum(v[2] for v in ma.values()), sum(v[3] for v in ma.values())
        if mb:
            food_b = sum(v[1] or 0 for v in mb.values())
            walled_b = sum(v[0] for v in mb.values()) / len(mb)
            summary.append(('Food walk, all cases', f'{food_b:,} → {food_a:,}', delta(food_b, food_a)))
            summary.append(('Walled area per room cell', f'{walled_b:.2f} → {walled_a:.2f}', delta(walled_b, walled_a)))
        else:
            summary.append(('Food walk, all cases', f'{food_a:,}', ''))
            summary.append(('Walled area per room cell', f'{walled_a:.2f}', ''))
        summary.append(('Valid layouts', f'{ok_a} / {total_a}', ''))
    stat_html = ''.join(f'<div class="stat"><b>{k}</b><span class="v">{v}</span>{d}</div>' for k, v, d in summary)
    legend = ''.join(f'<span><i style="background:rgb{c}"></i>{esc(n)}</span>' for n, c in LEGEND)
    page = f'''<title>{esc(title)}</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Archivo:wght@500;700&family=Source+Sans+3:wght@400;600&family=JetBrains+Mono:wght@400;600&display=swap">
<style>{PAGE_CSS}</style>
<main>
<div><h1>{esc(title)}</h1>
<p class="lede">First seed of each layout case, {esc(before_label)} next to {esc(after_label)}. Averages cover every seed in the matrix logs. Lower is better for both metrics.</p></div>
<div class="summary">{stat_html}</div>
<div class="legend">{legend}</div>
<nav aria-label="Cases">{' '.join(links)}</nav>
{''.join(sections)}
</main>'''
    open(out, 'w').write(page)
    print(out, os.path.getsize(out) // 1024, 'KB')


def main(argv):
    if len(argv) < 2:
        sys.exit(__doc__)
    cmd, args = argv[1], argv[2:]
    options = {}
    positional = []
    i = 0
    while i < len(args):
        if args[i].startswith('--'):
            options[args[i][2:].replace('-', '_')] = args[i + 1]
            i += 2
        else:
            positional.append(args[i])
            i += 1
    if cmd == 'png':
        cmd_png(*positional[:3], **options)
    elif cmd == 'sheet':
        cmd_sheet(positional[0], positional[1], positional[2:])
    elif cmd == 'page':
        cmd_page(*positional[:3], **options)
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    main(sys.argv)
