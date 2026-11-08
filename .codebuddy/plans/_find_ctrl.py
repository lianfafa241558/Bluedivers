import io, os, re, sys

guid = sys.argv[1] if len(sys.argv) > 1 else 'f4586e59c6395e54ca4549abc0ab5742'
hit = None
for root, dirs, files in os.walk('Assets'):
    for f in files:
        if f.endswith('.meta'):
            p = os.path.join(root, f)
            try:
                if guid in io.open(p, encoding='utf-8', errors='ignore').read():
                    hit = p
                    break
            except Exception:
                pass
    if hit:
        break
print('meta:', hit)
if hit:
    p = hit[:-5]
    s = io.open(p, encoding='utf-8').read()
    print(p, len(s))
    names = sorted(set(re.findall(r'm_Name: (.*)', s)))
    print('NAMES:', names)
    # animation clips referenced
    print('CLIPS:', sorted(set(re.findall(r'm_Motion: \{fileID: (-?\d+), guid: (\w+)', s))))
