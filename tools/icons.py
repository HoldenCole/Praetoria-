# Writes the icon sprite and the icon gradients into src/Web/wwwroot/index.html from src/Web/Services/Icons.cs, so every glyph and
# gradient exists once in the document, outside Blazor's render tree (docs/VISUAL_SYSTEM.md 8.1). Run it after adding a glyph:
#
#   python3 tools/icons.py            rewrites the block between <!-- icons:begin --> and <!-- icons:end -->
#   python3 tools/icons.py --check    exits 1 if index.html is out of step with Icons.cs (tests/IconSpriteTests.cs checks the same)
import re, sys, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICONS = os.path.join(ROOT, 'src/Web/Services/Icons.cs')
INDEX = os.path.join(ROOT, 'src/Web/wwwroot/index.html')

# the six ramps of 2.1 (top, middle, foot), then the gold of the standard's finials and the dull brass of its pole
RAMPS = [('brass', '#fbeab4', '#d6ae55', '#8f6a26'), ('ivory', '#fffaf0', '#e2d7bd', '#a39780'), ('ivory-hi', '#ffffff', '#efe6cf', '#b8ad95'),
         ('steel', '#eef4fb', '#a9bad0', '#5d6e86'), ('ember', '#ffc3a6', '#ec6c4c', '#9b2d1a'), ('good', '#d8f2c8', '#8cc47e', '#43723a')]

def sprite():
    src = open(ICONS).read()
    paths = re.findall(r'\["([^"]+)"\] = "([^"]+)"', src)
    seen, out = set(), []
    for k, d in paths:
        if k in seen: continue
        seen.add(k); out.append((k, d))
    g = ''.join(f'<linearGradient id="g-{n}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{a}"/><stop offset=".55" stop-color="{b}"/><stop offset="1" stop-color="{c}"/></linearGradient>'
                for n, a, b, c in RAMPS)
    g += ('<linearGradient id="g-gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f7dc8e"/><stop offset=".5" stop-color="#d4a23e"/><stop offset="1" stop-color="#8b6420"/></linearGradient>'
          '<linearGradient id="g-rim" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#f3e1a8"/><stop offset=".5" stop-color="#a98436"/><stop offset="1" stop-color="#4f390f"/></linearGradient>')
    syms = '\n'.join(f'<symbol id="i-{k}" viewBox="0 0 512 512"><path d="{d}"/></symbol>' for k, d in out)
    return ('<!-- icons:begin (written by tools/icons.py from src/Web/Services/Icons.cs; do not edit by hand) -->\n'
            '<svg class="sprite" width="0" height="0" style="position:absolute;width:0;height:0;overflow:hidden" aria-hidden="true" focusable="false">'
            f'<defs>{g}</defs>\n{syms}\n</svg>\n<!-- icons:end -->')

def main():
    html = open(INDEX).read()
    block = sprite()
    m = re.search(r'<!-- icons:begin.*?<!-- icons:end -->', html, re.S)
    if '--check' in sys.argv:
        ok = m is not None and m.group(0) == block
        print('icon sprite ' + ('in step with Icons.cs' if ok else 'OUT OF STEP: run python3 tools/icons.py'))
        sys.exit(0 if ok else 1)
    if m: html = html[:m.start()] + block + html[m.end():]
    else: html = html.replace('<body>\n', '<body>\n    ' + block + '\n', 1)
    open(INDEX, 'w').write(html)
    print('wrote', block.count('<symbol'), 'glyphs and', block.count('Gradient id'), 'gradients into index.html')

if __name__ == '__main__':
    main()
