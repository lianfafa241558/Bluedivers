import io, os, sys

guid = sys.argv[1]
exts = ('.prefab', '.unity', '.asset', '.controller')
for root, dirs, files in os.walk('Assets'):
    for f in files:
        if f.endswith(exts):
            p = os.path.join(root, f)
            try:
                s = io.open(p, encoding='utf-8', errors='ignore').read()
            except Exception:
                continue
            if guid in s:
                print(p)
