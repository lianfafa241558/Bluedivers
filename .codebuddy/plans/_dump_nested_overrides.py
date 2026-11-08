# -*- coding: utf-8 -*-
import io, re, sys

path = sys.argv[1]
guid = sys.argv[2] if len(sys.argv) > 2 else 'e8ab4ad369eaefc47850b29cd4ca2429'
s = io.open(path, encoding='utf-8').read()

for m in re.finditer(r'--- !u!1001 &(-?\d+)\n.*?(?=\n--- |\Z)', s, re.S):
    block = m.group(0)
    if guid not in block:
        continue
    print('=== nested VehicleCanvas instance', m.group(1))
    for mm in re.finditer(r'- target: \{fileID: (-?\d+), guid: ' + guid + r',\s*\n\s*type: 3\}\n\s*propertyPath: (.*?)\n\s*value: (.*?)\n(\s*objectReference: (.*?)\n)?', block, re.S):
        print('   ', mm.group(1), '|', mm.group(2), '=', mm.group(3).strip(), '| ref', (mm.group(5) or '').strip())
