import bpy
import numpy as np

PATHS = [
    ("crop", r"C:\Users\admin\AppData\Local\CodeBuddyExtension\Data\745eef27-68fa-4891-82d2-f37630042ec6\VisualStudio\745eef27-68fa-4891-82d2-f37630042ec6\history\b52f89de48dac559bdc39636e3dceded\bf74d07bd86944a2b4075792834afcee\assets\image.54490e796b.png"),
    ("desktop", r"C:\Users\admin\Desktop\GearGraphic_Colette.png"),
]

for tag, p in PATHS:
    img = bpy.data.images.load(p, check_existing=True)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    # 橙色线条：亮且 R 明显高于 B（背景是暗橄榄/白/黑，都不满足）
    mask = (r > 0.35) & ((r - b) > 0.12)
    ys, xs = np.nonzero(mask)
    print("=== %s %dx%d  orange_fill=%.3f" % (tag, w, h, float(mask.mean())))
    if len(xs) == 0:
        print("no orange")
        continue
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    print("bbox_px x[%d,%d] y[%d,%d]  w=%d h=%d  aspect=%.2f" % (x0, x1, y0, y1, x1 - x0, y1 - y0, (x1 - x0) / max(1, y1 - y0)))
    cols = 100
    rows = max(1, int(cols * (y1 - y0) / max(1, x1 - x0) * 0.5))
    for j in range(rows):
        ya = y0 + (y1 - y0) * j // rows
        yb = max(ya + 1, y0 + (y1 - y0) * (j + 1) // rows)
        line = []
        for i in range(cols):
            xa = x0 + (x1 - x0) * i // cols
            xb = max(xa + 1, x0 + (x1 - x0) * (i + 1) // cols)
            line.append("#" if mask[ya:yb, xa:xb].any() else ".")
        print("".join(line))
