"""三视图定量测量：alpha 掩膜 → bbox / 每列上下边界 / 粗 ASCII / 配色分区。

坐标约定（Blender 侧）：
  X = 长轴（侧视图水平，右为 +X，发射盘在 +X 端）
  Z = 高度（侧/正视垂直，上为 +Z）
  Y = 厚度（正视水平 / 俯视垂直）
用法： python ref_views.py
"""
import os
from PIL import Image
import numpy as np

REF = "E:/Bluedivers/.codebuddy/ref"
OUT = "E:/Bluedivers/.codebuddy/out"
os.makedirs(OUT, exist_ok=True)

VIEWS = ["side", "front", "top"]  # side=正左 front=正前 top=正上
SRC = {"side": "正左.png", "front": "正前.png", "top": "正上.png"}


def load(name):
    """在 ref 目录里按中文名找文件（可能带 hash 后缀），返回 RGBA ndarray"""
    for f in os.listdir(REF):
        if f.startswith(SRC[name].split(".")[0]) and f.lower().endswith(".png"):
            im = Image.open(os.path.join(REF, f)).convert("RGBA")
            return np.array(im)
    raise FileNotFoundError(SRC[name])


def mask_of(arr):
    return arr[:, :, 3] > 128


def bbox(m):
    ys, xs = np.where(m)
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def col_profile(m, x0, x1, n=56):
    """沿 x 采样 n 列，返回每列 [top,bottom]（掩膜内像素 y 范围）"""
    W = x1 - x0
    out = []
    for i in range(n):
        cx = int(x0 + (i + 0.5) * W / n)
        col = np.where(m[:, cx])[0]
        out.append((cx, int(col.min()), int(col.max())) if len(col) else (cx, -1, -1))
    return out


def ascii_sil(m, x0, y0, x1, y1, cols=76):
    rows = max(1, int(cols * ((y1 - y0) / max(1, x1 - x0)) * 0.5))
    sub = m[y0:y1, x0:x1]
    h, w = sub.shape
    lines = []
    for j in range(rows):
        line = []
        for i in range(cols):
            cy = int((j + 0.5) * h / rows)
            cx = int((i + 0.5) * w / cols)
            win = sub[max(0, cy - 1):cy + 2, max(0, cx - 1):cx + 2]
            line.append("#" if win.mean() > 0.4 else ".")
        lines.append("".join(line))
    return lines


def color_map(arr, m, x0, y0, x1, y1, cols=76):
    """把参考图配色量化成字母：W白 N深 C铜 O橙 G绿 R红 S钢灰 D暗绿橄榄"""
    sub = arr[y0:y1, x0:x1, :3].astype(int)
    msub = m[y0:y1, x0:x1]
    h, w = msub.shape
    rows = max(1, int(cols * (h / max(1, w)) * 0.5))
    PAL = {"W": (225, 220, 210), "N": (35, 35, 40), "C": (150, 100, 55), "O": (200, 130, 45),
           "G": (110, 230, 170), "R": (180, 50, 40), "S": (120, 125, 130), "D": (85, 95, 70)}
    lines = []
    for j in range(rows):
        line = []
        for i in range(cols):
            cy = int((j + 0.5) * h / rows)
            cx = int((i + 0.5) * w / cols)
            if not msub[cy, cx]:
                line.append(".")
                continue
            r, g, b = sub[cy, cx]
            best, bd = "?", 9e9
            for k, c in PAL.items():
                d = (r - c[0]) ** 2 + (g - c[1]) ** 2 + (b - c[2]) ** 2
                if d < bd:
                    bd, best = d, k
            line.append(best)
        lines.append("".join(line))
    return lines


def main():
    data = {}
    for v in VIEWS:
        arr = load(v)
        m = mask_of(arr)
        x0, y0, x1, y1 = bbox(m)
        w, h = x1 - x0, y1 - y0
        print("\n" % () if False else "=" * 20, v, "bbox=(%d,%d)-(%d,%d) w=%d h=%d w/h=%.3f"
              % (x0, y0, x1, y1, w, h, w / h))
        np.save(os.path.join(OUT, "mask_%s.npy" % v), m)
        data[v] = (arr, m, (x0, y0, x1, y1))

    # 侧视 / 正视：把长度归一到 1.0m，输出每列高度范围（米）
    sx0, sy0, sx1, sy1 = data["side"][2]
    fx0, fy0, fx1, fy1 = data["front"][2]
    tx0, ty0, tx1, ty1 = data["top"][2]
    slen = sx1 - sx0
    print("\n假设侧视全长 = 1.000 m，像素/米 = %d" % slen)
    print("\n== 侧视 每列 [z_bottom..z_top] (米, X 也为米) ==")
    for cx, t, b in col_profile(data["side"][1], sx0, sx1):
        if t < 0:
            continue
        print("x=%.3f  z %.3f..%.3f" % ((cx - sx0) / slen, (sy1 - b) / slen, (sy1 - t) / slen))

    print("\n== 侧视 ASCII ==")
    for l in ascii_sil(data["side"][1], sx0, sy0, sx1, sy1):
        print(l)
    print("\n== 侧视 配色 ==")
    for l in color_map(data["side"][0], data["side"][1], sx0, sy0, sx1, sy1):
        print(l)

    print("\n== 正视 ASCII ==")
    for l in ascii_sil(data["front"][1], fx0, fy0, fx1, fy1, cols=60):
        print(l)

    print("\n== 俯视 ASCII ==")
    for l in ascii_sil(data["top"][1], tx0, ty0, tx1, ty1, cols=76):
        print(l)


if __name__ == "__main__":
    main()
