"""采样参考图：粗网格平均色 + 亮度，用于确定背景模型与阈值。"""
import os
from PIL import Image

REF = "E:/Bluedivers/.codebuddy/ref/ref_render.png"
im = Image.open(REF).convert("RGB")
px = im.load()
W, H = im.size

print("size", W, H)
print("corners:")
for name, (x, y) in [("tl", (5, 5)), ("tr", (W - 5, 5)), ("bl", (5, H - 5)),
                     ("br", (W - 5, H - 5)), ("mid", (W // 2, H // 2))]:
    print("  ", name, px[x, y])

GRIDX, GRIDY = 32, 18
print("\n== 网格平均 RGB (R,G,B) ==")
for j in range(GRIDY):
    row = []
    for i in range(GRIDX):
        rs = gs = bs = n = 0
        for y in range(j * H // GRIDY, (j + 1) * H // GRIDY, 6):
            for x in range(i * W // GRIDX, (i + 1) * W // GRIDX, 6):
                r, g, b = px[x, y]
                rs += r
                gs += g
                bs += b
                n += 1
        row.append("%3d,%3d,%3d" % (rs // n, gs // n, bs // n))
    print(" ".join(row))
