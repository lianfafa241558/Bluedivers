import glob, os, re

ROOT = r'd:\Pro\Bluedivers\Assets\Resources\GameData\WeaponModule'
for f in sorted(glob.glob(os.path.join(ROOT, '**', '*.asset'), recursive=True)):
    b = open(f, 'rb').read()
    bom = b.startswith(b'\xef\xbb\xbf')
    t = (b[3:] if bom else b).decode('utf-8')
    m = re.findall(r'^  describe: "(.*)"$', t, re.M)
    d = m[0].encode('latin-1', 'backslashreplace').decode('unicode_escape') if m else '<缺失>'
    print(('BOM' if bom else '---'), os.path.relpath(f, ROOT).replace('\\', '/'), '|', d)
