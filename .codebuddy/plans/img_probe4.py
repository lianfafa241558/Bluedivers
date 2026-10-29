import bpy
import numpy as np

P = r"C:\Users\admin\AppData\Local\CodeBuddyExtension\Data\745eef27-68fa-4891-82d2-f37630042ec6\VisualStudio\745eef27-68fa-4891-82d2-f37630042ec6\history\b52f89de48dac559bdc39636e3dceded\bf74d07bd86944a2b4075792834afcee\assets\image.54490e796b.png"
img = bpy.data.images.load(P, check_existing=True)
w, h = img.size
a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
lum = a[..., :3].max(axis=2)
print("lum p5/p50/p90/p99", [round(float(np.percentile(lum, q)), 3) for q in (5, 50, 90, 99)])

def ascii_map(mask, cols=100):
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    rows = max(1, int(cols * (y1 - y0) / (x1 - x0) * 0.5))
    out = []
    for j in range(rows):
        ya = y0 + (y1 - y0) * j // rows
        yb = max(ya + 1, y0 + (y1 - y0) * (j + 1) // rows)
        line = []
        for i in range(cols):
            xa = x0 + (x1 - x0) * i // cols
            xb = max(xa + 1, x0 + (x1 - x0) * (i + 1) // cols)
            line.append("#" if mask[ya:yb, xa:xb].any() else ".")
        out.append("".join(line))
    return "bbox x[%d,%d] y[%d,%d] %dx%d aspect=%.2f" % (x0, x1, y0, y1, x1 - x0, y1 - y0, (x1 - x0) / (y1 - y0)), out

for t in (0.22, 0.30, 0.42):
    head, rows = ascii_map(lum > t)
    print("=== lum > %.2f  %s" % (t, head))
    for r in rows:
        print(r)
