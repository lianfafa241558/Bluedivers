"""把参考图转成 ASCII 网格 + 配色分区，用来定量建模（不靠肉眼猜坐标）。

用法： python ref_probe.py [cols] [rows]
输出：
  1) 轮廓 ASCII（每个格子按留白覆盖率给字符）
  2) 配色分区图（W=白 N=深藏青 C=铜/橙 G=绿 R=红 -=背景）
  3) 每列上/下边界与每行左/右边界（像素坐标，便于直接换算成部件尺寸）
"""
import sys
import os
from PIL import Image

REF = os.path.join("E:/Bluedivers/.codebuddy/ref", "ref_render.png")

# 参考图色调
PAL = {
    "W": (0.80, 0.78, 0.74),   # 白甲
    "N": (0.12, 0.14, 0.18),   # 深藏青包边
    "C": (0.45, 0.24, 0.10),   # 青铜
    "O": (0.80, 0.32, 0.05),   # 橙
    "G": (0.10, 0.70, 0.35),   # 绿发光
    "R": (0.62, 0.10, 0.07),   # 红
    "S": (0.50, 0.52, 0.55),   # 钢灰
}


def classify(rgb):
    r, g, b = [v / 255.0 for v in rgb]
    mx, mn = max(r, g, b), min(r, g, b)
    # 背景：接近纯青
    if b >= r and g >= r and mx - mn > 0.05 and b > 0.12:
        return "-"
    best, bd = " ", 9e9
    for k, c in PAL.items():
        d = (r - c[0]) ** 2 + (g - c[1]) ** 2 + (b - c[2]) ** 2
        if d < bd:
            bd, best = d, k
    return best


def main():
    cols = int(sys.argv[1]) if len(sys.argv) > 1 else 96
    rows = int(sys.argv[2]) if len(sys.argv) > 2 else 44
    im = Image.open(REF).convert("RGB")
    W, H = im.size
    px = im.load()

    # ---- 先用背景色做粗掩膜，求 bbox
    xs, ys = [], []
    for y in range(0, H, 3):
        for x in range(0, W, 3):
            r, g, b = px[x, y]
            if not (b >= r and g >= r and (b - min(r, g, b)) > 12):
                xs.append(x)
                ys.append(y)
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    print("bbox x %d..%d (w=%d)  y %d..%d (h=%d)  ratio w/h=%.3f"
          % (x0, x1, x1 - x0, y0, y1, y1 - y0, (x1 - x0) / (y1 - y0)))

    cw = (x1 - x0) / cols
    ch = (y1 - y0) / rows
    print("\n== 轮廓 ASCII (%dx%d，每格中心是否属于物体) ==" % (cols, rows))
    for j in range(rows):
        line = []
        for i in range(cols):
            cx = int(x0 + (i + 0.5) * cw)
            cy = int(y0 + (j + 0.5) * ch)
            r, g, b = px[cx, cy]
            bg = b >= r and g >= r and (b - min(r, g, b)) > 12
            line.append("." if bg else "#")
        print("".join(line))

    print("\n== 配色分区 (%dx%d) ==" % (cols, rows))
    for j in range(rows):
        line = []
        for i in range(cols):
            cx = int(x0 + (i + 0.5) * cw)
            cy = int(y0 + (j + 0.5) * ch)
            line.append(classify(px[cx, cy]))
        print("".join(line))

    print("\n== 每列占用像素区间（step=%d，仅列 min/max 有意义）==" % max(1, (x1 - x0) // 48))
    for i in range(cols):
        cx = int(x0 + (i + 0.5) * cw)
        ts, bs = None, None
        cnt = 0
        for y in range(y0, y1):
            r, g, b = px[cx, y]
            bg = b >= r and g >= r and (b - min(r, g, b)) > 12
            if not bg:
                if ts is None:
                    ts = y
                bs = y
                cnt += 1
        if ts is None:
            print("x%4d  empty" % cx)
        else:
            print("x%4d  y %4d..%4d  fill %d" % (cx, ts, bs, cnt))


if __name__ == "__main__":
    main()
