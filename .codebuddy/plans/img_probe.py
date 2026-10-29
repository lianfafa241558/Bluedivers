import bpy
import numpy as np

P = r"C:\Users\admin\Desktop\GearGraphic_Colette.png"
img = bpy.data.images.load(P, check_existing=True)
w, h = img.size
a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]  # Blender 像素自下而上，翻过来
lum = a[..., :3].max(axis=2)
mask = lum > 0.30

ys, xs = np.nonzero(mask)
print("size", w, h)
print("bbox_px", xs.min(), xs.max(), ys.min(), ys.max())
print("bbox_norm", round(xs.min() / w, 3), round(xs.max() / w, 3), round(ys.min() / h, 3), round(ys.max() / h, 3))
print("fill", round(float(mask.mean()), 3))

cols = 110
rows = max(1, int(cols * h / w * 0.5))
for r in range(rows):
    y0, y1 = int(r * h / rows), max(int(r * h / rows) + 1, int((r + 1) * h / rows))
    line = []
    for c in range(cols):
        x0, x1 = int(c * w / cols), max(int(c * w / cols) + 1, int((c + 1) * w / cols))
        line.append("#" if mask[y0:y1, x0:x1].any() else ".")
    print("".join(line))
