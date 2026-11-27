# Converts px to rem (15px root) in a stylesheet, so the client's text-size setting (13.5, 15, 16.5 or 18px on <html>) scales the
# interface (docs/VISUAL_SYSTEM.md, 2.4). At the normal setting nothing moves: a rem is 15px.
#
#   python3 tools/rem.py src/Web/wwwroot/css/app.css src/Web/wwwroot/css/win/laws.css
#
# Kept in px: anything under 3px (hairlines), borders, outlines, shadows, filters, clip-paths, backgrounds, masks, border-images,
# transforms, strokes; the :root block; media query conditions (a media query's rem is the browser's, not the page's); and the
# map's own geometry (labels placed by the map, the canvas), which does not scale with text. Comments and nesting are kept.
import re, sys

KEEP = re.compile(r'^(border|box-shadow|text-shadow|filter|backdrop-filter|clip-path|background|mask|-webkit-mask|outline|transform|'
                  r'border-image|stroke|paint-order|perspective|translate|scale|rotate|image-rendering|scrollbar)')
NOSCALE_SEL = re.compile(r'(^|[\s,>+~(])(\.map\b|\.map-wrap|\.gl-|\.sys-label|\.region-label|\.fleet-label|\.polity-label|\.star\b|'
                         r'body\b|html\b|canvas\b|\.sysmap|\.worldview)')
PX = re.compile(r'(?<![\w.#-])(-?\d*\.?\d+)px\b')

def conv_val(v):
    def f(m):
        n = float(m.group(1))
        if abs(n) < 3: return m.group(0)
        r = n / 15
        s = ('%.4f' % r).rstrip('0').rstrip('.')
        if s.startswith('0.'): s = s[1:]
        elif s.startswith('-0.'): s = '-' + s[2:]
        return s + 'rem'
    return PX.sub(f, v)

def conv_decls(body):
    # split on ';' outside parentheses and strings, keeping comments as they are
    out, cur, depth, i = [], '', 0, 0
    while i < len(body):
        c = body[i]
        if body.startswith('/*', i):
            j = body.index('*/', i) + 2; cur += body[i:j]; i = j; continue
        if c in '"\'':
            j = i + 1
            while body[j] != c: j += 2 if body[j] == '\\' else 1
            cur += body[i:j + 1]; i = j + 1; continue
        if c == '(': depth += 1
        elif c == ')': depth -= 1
        if c == ';' and depth == 0: out.append(cur); cur = ''; i += 1; continue
        cur += c; i += 1
    out.append(cur)
    res = []
    for d in out:
        bare = re.sub(r'/\*.*?\*/', '', d, flags=re.S)
        if ':' not in bare: res.append(d); continue
        k = d.index(':')
        prop = re.sub(r'/\*.*?\*/', '', d[:k]).strip().lower()
        res.append(d if KEEP.match(prop) else d[:k + 1] + conv_val(d[k + 1:]))
    return ';'.join(res)

def conv(css):
    # walk the sheet: a prelude, then a block; at-rules that hold rules (media, supports, layer) are walked into
    out, i, n = [], 0, len(css)
    while i < n:
        if css.startswith('/*', i):
            j = css.index('*/', i) + 2; out.append(css[i:j]); i = j; continue
        j = i
        while j < n and css[j] not in '{};':
            if css.startswith('/*', j): j = css.index('*/', j) + 2; continue
            j += 1
        if j >= n: out.append(css[i:]); break
        if css[j] in ';}': out.append(css[i:j + 1]); i = j + 1; continue
        prelude = css[i:j]
        k, depth = j, 0
        while k < n:
            if css.startswith('/*', k): k = css.index('*/', k) + 2; continue
            c = css[k]
            if c in '"\'':
                q = c; k += 1
                while css[k] != q: k += 2 if css[k] == '\\' else 1
            elif c == '{': depth += 1
            elif c == '}':
                depth -= 1
                if depth == 0: break
            k += 1
        body = css[j + 1:k]
        p = prelude.strip()
        if p.startswith('@media') or p.startswith('@supports') or p.startswith('@layer') or p.startswith('@container'):
            out.append(prelude + '{' + conv(body) + '}')
        elif p.startswith('@') or p.startswith(':root') or NOSCALE_SEL.search(re.sub(r'/\*.*?\*/', '', p, flags=re.S)):
            out.append(prelude + '{' + body + '}')
        else:
            out.append(prelude + '{' + conv_decls(body) + '}')
        i = k + 1
    return ''.join(out)

if __name__ == '__main__':
    for path in sys.argv[1:]:
        s = open(path).read()
        t = conv(s)
        open(path, 'w').write(t)
        print('converted', path, len(PX.findall(s)) - len(PX.findall(t)), 'px values to rem')
