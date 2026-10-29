import hashlib
import os
import struct

PATHS = [
    r"C:\Users\admin\Desktop\GearGraphic_Colette.png",
    r"C:\Users\admin\AppData\Local\CodeBuddyExtension\Data\745eef27-68fa-4891-82d2-f37630042ec6\VisualStudio\745eef27-68fa-4891-82d2-f37630042ec6\history\b52f89de48dac559bdc39636e3dceded\bf74d07bd86944a2b4075792834afcee\assets\image.54490e796b.png",
]
for p in PATHS:
    if not os.path.exists(p):
        print("MISSING", p)
        continue
    with open(p, "rb") as f:
        data = f.read()
    w, h = struct.unpack(">II", data[16:24])
    print("%dx%d  %d KB  %s  %s" % (w, h, len(data) // 1024, hashlib.md5(data).hexdigest()[:8], p))
