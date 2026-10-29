import bpy
import numpy as np

P = r"C:\Users\admin\Desktop\GearGraphic_Colette.png"
img = bpy.data.images.load(P, check_existing=True)
w, h = img.size
a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
rgb = a[..., :3]
lum = rgb.max(axis=2)
print("size", w, h, "alpha_min", float(a[..., 3].min()), "alpha_max", float(a[..., 3].max()))
print("lum min/mean/max", round(float(lum.min()), 3), round(float(lum.mean()), 3), round(float(lum.max()), 3))
for t in (0.2, 0.3, 0.4, 0.5, 0.6, 0.7):
    print("frac lum>%.1f" % t, round(float((lum > t).mean()), 3))

# 16x9 网格平均色（十六进制）
gx, gy = 16, 9
for j in range(gy):
    row = []
    for i in range(gx):
        blk = rgb[j * h // gy:(j + 1) * h // gy, i * w // gx:(i + 1) * w // gx]
        m = blk.reshape(-1, 3).mean(axis=0)
        row.append("%02x%02x%02x" % tuple(int(max(0, min(1, v)) * 255) for v in m))
    print(" ".join(row))
