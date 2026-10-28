import glob, os, re, json

ROOT = r'd:\Pro\Bluedivers\Assets\Resources\GameData\WeaponModule'
out = []
for f in sorted(glob.glob(os.path.join(ROOT, '**', '*.asset'), recursive=True)):
    t = open(f, encoding='utf-8').read()
    m = re.search(r'^  name: "(.*)"$', t, re.M)
    nm = m.group(1).encode('latin-1', 'backslashreplace').decode('unicode_escape') if m else '?'
    ty = re.search(r'^  type: (\d+)$', t, re.M)
    has_describe = bool(re.search(r'^  describe:', t, re.M))
    desc = re.findall(r'^  - Key: (\d)\r?\n    Value: "(.*)"', t, re.M)
    desc = [k + ':' + v.encode('latin-1', 'backslashreplace').decode('unicode_escape') for k, v in desc]
    out.append({
        'rel': os.path.relpath(f, ROOT).replace('\\', '/'),
        'name': nm,
        'type': int(ty.group(1)) if ty else None,
        'has_describe': has_describe,
        'desc': desc,
    })
print(json.dumps(out, ensure_ascii=False, indent=1))
