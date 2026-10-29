"""从三视图掩膜反推主体盒集合 → out/body_boxes.json (v2)

每列：侧视掩膜拆**连续 z 段**（保留空腔，不填实）× 俯视掩膜最大连续 y 段；
锚点容差合并（zT ±0.025 / y ±0.04 / 同色），盒色 = 盒中心像素分类。
发射盘区（x > 0.86L）不反推，由 Blender 脚本手工建。
"""
import os
import json
from PIL import Image
import numpy as np

REF = "E:/Bluedivers/.codebuddy/ref"
OUT = "E:/Bluedivers/.codebuddy/out"

PAL = {"W": (228, 222, 210), "N": (38, 38, 44), "C": (150, 100, 55), "O": (205, 135, 50),
       "G": (110, 230, 170), "R": (185, 55, 45), "S": (115, 120, 125), "D": (80, 90, 68)}


def classify(rgb):
    r, g, b = [int(v) for v in rgb[:3]]
    best, bd = "S", 9e9
    for k, c in PAL.items():
        d = (r - c[0]) ** 2 + (g - c[1]) ** 2 + (b - c[2]) ** 2
        if d < bd:
            bd, best = d, k
    return best


def runs(bools, min_len):
    """连续 True 段列表 [(i0,i1)]，长度 < min_len 的丢弃"""
    out = []
    i, n = 0, len(bools)
    while i < n:
        if bools[i]:
            j = i
            while j + 1 < n and bools[j + 1]:
                j += 1
            if j - i + 1 >= min_len:
                out.append((i, j))
            i = j + 1
        else:
            i += 1
    return out


def largest_run(bools):
    rs = runs(bools, 1)
    return max(rs, key=lambda r: r[1] - r[0]) if rs else None


def main():
    m_side = np.load(os.path.join(OUT, "mask_side.npy"))
    m_top = np.load(os.path.join(OUT, "mask_top.npy"))
    img = np.array(Image.open(os.path.join(REF, "正左.png")).convert("RGB"))

    sx = np.where(m_side.any(axis=0))[0]
    tx = np.where(m_top.any(axis=0))[0]
    sy = np.where(m_side.any(axis=1))[0]
    ty = np.where(m_top.any(axis=1))[0]
    sx0, sx1, sy1 = int(sx.min()), int(sx.max()), int(sy.max())
    tx0, tx1 = int(tx.min()), int(tx.max())
    ty0, ty1 = int(ty.min()), int(ty.max())
    L = float(sx1 - sx0 + 1)
    YC = (ty0 + ty1) / 2.0

    DISC_X = 0.86
    step_px = max(1, int(L // 120))
    min_run_px = int(0.012 * L)  # z 段最矮 1.2cm

    cols = []  # (xm, zt, zb, yb, yt, color)
    for px in range(sx0, int(sx0 + DISC_X * L), step_px):
        col = np.where(m_side[:, px])[0]
        if not len(col):
            continue
        tpx = int(round(tx0 + (px - sx0) * ((tx1 - tx0) / L)))
        yr = largest_run(m_top[:, tpx].tolist())
        if yr is None:
            continue
        yb = (yr[0] + ty0 - YC) / L
        yt = (yr[1] + ty0 - YC) / L
        for a, b in runs(m_side[:, px].tolist(), min_run_px):
            zt = (sy1 - a) / L
            zb = (sy1 - b) / L
            cy = (a + b) // 2
            cols.append(((px - sx0) / L, zt, zb, yb, yt, classify(img[cy, px])))

    # 轮廓跟踪：逐列把 z 游程与活动段做 z-重叠匹配（≥60% 矮者），匹配不到就开新段
    # cols 条目 = (xm, zt, zb, yb, yt)；同列多个游程是相邻条目，不能直接两两比较
    col_runs = []  # 按列分组 [(xm, yb, yt, [(zt,zb),...])]
    for i, c in enumerate(cols):
        if col_runs and abs(col_runs[-1][0] - c[0]) < 1e-9:
            col_runs[-1][3].append((c[1], c[2]))
        else:
            col_runs.append([c[0], c[3], c[4], [(c[1], c[2])]])

    active = []  # dict(x0,x1,zt,zb,yb,yt,last=(zt,zb))
    res = []

    def close(seg):
        seg["zt"] = seg["zts"][len(seg["zts"]) // 2]
        seg["zb"] = seg["zbs"][len(seg["zbs"]) // 2]
        res.append(seg)

    for xm, yb, yt, rr in col_runs:
        rr = sorted(rr, key=lambda t: -t[0])  # 按 zt 降序
        used = [False] * len(rr)
        still = []
        for seg in active:
            hit = -1
            for i, (zt, zb) in enumerate(rr):
                if used[i]:
                    continue
                ov = min(seg["zt"], zt) - max(seg["zb"], zb)
                lz = seg["zts"][-1]
                lb = seg["zbs"][-1]
                if ov >= 0.5 * min(seg["zt"] - seg["zb"], zt - zb) \
                        and abs(lz - zt) <= 0.035 and abs(lb - zb) <= 0.035 \
                        and abs(seg["yb"] - yb) <= 0.04 and abs(seg["yt"] - yt) <= 0.04:
                    hit = i
                    break
            if hit >= 0:
                zt, zb = rr[hit]
                used[hit] = True
                seg["x1"] = xm
                seg["zts"].append(zt)
                seg["zbs"].append(zb)
                still.append(seg)
            else:
                close(seg)
        for i, (zt, zb) in enumerate(rr):
            if not used[i]:
                still.append({"x0": xm, "x1": xm, "zt": zt, "zb": zb, "yb": yb, "yt": yt,
                              "zts": [zt], "zbs": [zb]})
        active = still
    for seg in active:
        close(seg)

    step = step_px / L

    def box_color(seg):
        """盒像素矩形内多数色（描边黑占少数，不影响主体色）"""
        from collections import Counter
        px_a = int(seg["x0"] * L) + sx0
        px_b = min(int(seg["x1"] * L) + sx0 + 1, img.shape[1] - 1)
        pz_a = max(int(sy1 - seg["zt"] * L), 0)
        pz_b = min(int(sy1 - seg["zb"] * L), img.shape[0] - 1)
        if px_b <= px_a or pz_b <= pz_a:
            return "S"
        cnt = Counter()
        for yy in range(pz_a, pz_b, 3):
            for xx in range(px_a, px_b, 3):
                if m_side[yy, xx]:
                    cnt[classify(img[yy, xx])] += 1
        return cnt.most_common(1)[0][0] if cnt else "S"

    step = step_px / L
    data = [{"x0": round(s["x0"] - step / 2, 4), "x1": round(s["x1"] + step / 2, 4),
             "z0": round(s["zb"], 4), "z1": round(s["zt"], 4),
             "y0": round(s["yb"], 4), "y1": round(s["yt"], 4), "c": box_color(s)}
            for s in res if (s["x1"] - s["x0"]) + step >= 0.015]  # 窄于 1.5cm 的碎片丢弃
    with open(os.path.join(OUT, "body_boxes.json"), "w") as f:
        json.dump(data, f)
    print("boxes:", len(data))
    for b in data:
        print("x %.3f..%.3f  z %.3f..%.3f  y %.3f..%.3f  %s"
              % (b["x0"], b["x1"], b["z0"], b["z1"], b["y0"], b["y1"], b["c"]))


if __name__ == "__main__":
    main()
