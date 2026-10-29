"""从参考图里抠出物体：PIL 边界泛洪找背景（C 实现）→ 反掩码 → 粗网连通分量 → 逐分量输出剪影 ASCII。

为什么这么做：参考图背景含纯白/纯黑/条纹面板，按 RGB 阈值分不开；从四边往内泛洪容差填充可以可靠摘掉背景。
"""
import os
from PIL import Image, ImageDraw
import numpy as np

REF = "E:/Bluedivers/.codebuddy/ref/ref_render.png"
OUT = "E:/Bluedivers/.codebuddy/out"
os.makedirs(OUT, exist_ok=True)


def bg_mask(img, tol=40):
    gray = img.convert("L")
    W, H = gray.size
    seed = Image.new("L", (W, H), 0)
    draw = ImageDraw.floodfill
    for x in range(0, W, 6):
        draw(seed, (x, 0), 1, thresh=tol)
        draw(seed, (x, H - 1), 1, thresh=tol)
    for y in range(0, H, 6):
        draw(seed, (0, y), 1, thresh=tol)
        draw(seed, (W - 1, y), 1, thresh=tol)
    return np.array(seed, dtype=bool)


def components(obj, step=4, min_px=600):
    """在粗网格上做连通分量，返回 [(x0,y0,x1,y1,cellcount)]（像素坐标）"""
    coarse = obj[::step, ::step]
    H, W = coarse.shape
    seen = np.zeros((H, W), bool)
    out = []
    for sy in range(H):
        for sx in range(W):
            if not coarse[sy, sx] or seen[sy, sx]:
                continue
            stack = [(sx, sy)]
            seen[sy, sx] = True
            pts = []
            while stack:
                x, y = stack.pop()
                pts.append((x, y))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and coarse[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((nx, ny))
            if len(pts) < min_px:
                continue
            xs = [p[0] * step for p in pts]
            ys = [p[1] * step for p in pts]
            out.append((min(xs), min(ys), max(xs), max(ys), len(pts)))
    return out


def ascii_sil(obj, x0, y0, x1, y1, cols=92):
    rows = max(1, int(cols * ((y1 - y0) / max(1, x1 - x0)) * 0.5))
    lines = []
    H, W = obj.shape
    for j in range(rows):
        cy = int(y0 + (j + 0.5) * (y1 - y0) / rows)
        line = []
        for i in range(cols):
            cx = int(x0 + (i + 0.5) * (x1 - x0) / cols)
            hit = tot = 0
            for yy in range(max(0, cy - 2), min(H, cy + 3)):
                for xx in range(max(0, cx - 2), min(W, cx + 3)):
                    tot += 1
                    hit += 1 if obj[yy, xx] else 0
            line.append("#" if hit > tot * 0.35 else ".")
        lines.append("".join(line))
    return lines


def main():
    img = Image.open(REF).convert("RGB")
    W, H = img.size
    bg = bg_mask(img)
    obj = ~bg
    print("size", W, H, "obj px %d (%.1f%%)" % (obj.sum(), 100.0 * obj.sum() / (W * H)))

    comps = components(obj)
    comps.sort(key=lambda c: -c[4])
    print("components:", len(comps))
    for k, (x0, y0, x1, y1, n) in enumerate(comps[:5]):
        print("\n--- comp %d bbox=(%d,%d)-(%d,%d) w=%d h=%d ratio=%.2f cells=%d ---"
              % (k, x0, y0, x1, y1, x1 - x0, y1 - y0, (x1 - x0) / max(1, y1 - y0), n))
        for line in ascii_sil(obj, x0, y0, x1, y1, cols=80):
            print(line)
    np.save(os.path.join(OUT, "ref_obj.npy"), obj)


if __name__ == "__main__":
    main()
