import glob
import io
import re

guids = {}
for meta in glob.glob('Assets/**/*.cs.meta', recursive=True):
    text = io.open(meta, encoding='utf-8', errors='ignore').read()
    m = re.search(r'guid: ([0-9a-f]{32})', text)
    if m:
        guids[m.group(1)] = meta[:-5].split('/')[-1]

for path in glob.glob('Assets/Resources/Prefabs/Projectiles/Grenade/*.prefab'):
    text = io.open(path, encoding='utf-8', errors='ignore').read()
    scripts = re.findall(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})', text)
    print(path.split('/')[-1], [guids.get(g, g) for g in scripts])
