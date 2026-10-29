"""模型渲染 vs 参考掩膜 IoU 比对：归一化 bbox → 模型掩膜缩放到参考尺寸 → IoU + 差异 ASCII。

用法： python gear_v5_cmp.py [view]   view ∈ side|front|all（默认 all）
"""
import os
import sys
from PIL import Image
import numpy as np

OUT = "E:/Bluedivers/.codebuddy/out"


def load_alpha(path):
    im = Image.open(path).convert("RGBA")
    return np.array(im)[:, :, 3] > 128


def bbox(m):
    ys, xs = np.where(m)
    return m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def iou(a, b):
    return (a & b).sum() / max(1, (a | b).sum())


def compare(name, ref_mask_path, model_png):
    ref = bbox(np.load(ref_mask_path))
    mdl = bbox(load_alpha(model_png))
    # 等比缩放（按高度对齐）+ 中心对齐，画布取两者并集——比例失真会如实反映在 IoU 里
    H, W = ref.shape
    scale = H / mdl.shape[0]
    nw = max(1, int(round(mdl.shape[1] * scale)))
    mi = Image.fromarray((mdl * 255).astype(np.uint8)).resize((nw, H), Image.LANCZOS)
    m = np.array(mi) > 128
    cw = max(W, nw)
    canvas_r = np.zeros((H, cw), bool)
    canvas_m = np.zeros((H, cw), bool)
    canvas_r[:, (cw - W) // 2:(cw - W) // 2 + W] = ref
    canvas_m[:, (cw - nw) // 2:(cw - nw) // 2 + nw] = m
    print("\n==== %s ====" % name)
    print("ref w/h=%.3f  model w/h=%.3f  (等比对齐后宽差 %+.1f%%)"
          % (W / H, mdl.shape[1] / mdl.shape[0], 100.0 * (nw - W) / W))
    print("IoU(原朝向) = %.3f" % iou(canvas_r, canvas_m))
    print("IoU(水平翻转) = %.3f" % iou(canvas_r[:, ::-1], canvas_m))

    # 差异 ASCII（不翻转）
    cols, rows = 72, max(1, int(72 * (H / cw) * 0.5))
    lines = []
    for j in range(rows):
        line = []
        for i in range(cols):
            cy = int((j + 0.5) * H / rows)
            cx = int((i + 0.5) * cw / cols)
            r = canvas_r[cy, cx]
            mm = canvas_m[cy, cx]
            line.append("#" if (r and mm) else ("R" if r else ("M" if mm else ".")))
        lines.append("".join(line))
    for l in lines:
        print(l)


def main():
    view = sys.argv[1] if len(sys.argv) > 1 else "all"
    if view in ("side", "all"):
        compare("侧视 side", os.path.join(OUT, "mask_side.npy"), os.path.join(OUT, "v5_side.png"))
    if view in ("front", "all"):
        compare("正视 front", os.path.join(OUT, "mask_front.npy"), os.path.join(OUT, "v5_front.png"))


if __name__ == "__main__":
    main()
