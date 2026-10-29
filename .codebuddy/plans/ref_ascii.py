"""把参考图转成灰度 ASCII，用于在无视觉通道时读出构图。

用法： python ref_ascii.py [cols] [x0] [x1] [y0] [y1]
字符越亮 = 画面越亮。
"""
import sys
from PIL import Image

REF = "E:/Bluedivers/.codebuddy/ref/ref_render.png"
RAMP = " .:-=+*#%@"

cols = int(sys.argv[1]) if len(sys.argv) > 1 else 100
bounds = [int(v) for v in sys.argv[2:6]] if len(sys.argv) > 5 else None

im = Image.open(REF).convert("RGB")
if bounds:
    im = im.crop(bounds)
W, H = im.size
rows = max(1, int(cols * (H / W) * 0.5))
small = im.resize((cols, rows), Image.LANCZOS)
px = small.load()
print("crop", (W, H), "-> grid", cols, "x", rows)
for y in range(rows):
    line = []
    for x in range(cols):
        r, g, b = px[x, y]
        lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
        line.append(RAMP[min(len(RAMP) - 1, int(lum * len(RAMP)))])
    print("%3d|%s" % (y, "".join(line)))
