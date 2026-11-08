import io, re, sys, os

path = sys.argv[1]
fid = sys.argv[2]
s = io.open(path, encoding='utf-8').read()
# find the GameObject whose components include transform fid? ScopeGo fileID may be GameObject or Transform
idx = s.find('&' + fid)
print('--- block at', fid, '---')
if idx >= 0:
    print(s[idx:idx+900])
# also print all m_Name lines with their nearest anchor
print('--- all objects ---')
for m in re.finditer(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- |\Z)', s, re.S):
    cls, anchor, body = m.group(1), m.group(2), m.group(3)
    name = re.search(r'm_Name: (.*)', body)
    print(cls, anchor, name.group(1)[:40] if name else '')
