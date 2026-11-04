# -*- coding: utf-8 -*-
"""把远端 LowPoly 模型做成 Palette Texture / Color Atlas 版本。

几何完全不动（不减面、不细分、不改形状），只重组顶点缓冲：
每个三角面独立 3 顶点（flat shading）+ UV 折叠到色卡里的一个色块。
产物覆盖 Assets/Art/Modle/Weapon/SciFiShotgun_LowPoly.*。
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import trimesh

GLB = r"E:\Bluedivers\Temp\gen3d_lowpoly_out\SciFiShotgun_LowPoly.glb"
OUT = r"E:\Bluedivers\Assets\Art\Modle\Weapon"
NAME = "SciFiShotgun_LowPoly"

# 4x4 色卡：黑灰机匣 / 银灰枪管 / 黄橙装饰；12 号是黄黑警示斜纹
PALETTE = [
    (16, 17, 20), (30, 32, 35), (44, 48, 52), (61, 67, 74),
    (85, 92, 99), (118, 126, 133), (168, 174, 180), (216, 220, 224),
    (242, 243, 244), (245, 179, 1), (217, 142, 0), (138, 90, 0),
    (245, 179, 1), (35, 38, 42), (106, 114, 122), (192, 160, 48),
]
STRIPE = 12
UV_TRI = [(0.18, 0.18), (0.82, 0.18), (0.50, 0.82)]


def build_palette(path):
    img = Image.new("RGB", (128, 128))
    for index, color in enumerate(PALETTE):
        row, col = divmod(index, 4)
        img.paste(color, (col * 32, row * 32, col * 32 + 32, row * 32 + 32))

    row, col = divmod(STRIPE, 4)
    tile = Image.new("RGB", (32, 32), (20, 20, 22))
    draw = ImageDraw.Draw(tile)
    for k in range(-4, 8):
        x0 = k * 8
        draw.polygon([(x0, 0), (x0 + 4, 0), (x0 + 36, 32), (x0 + 32, 32)], fill=(245, 179, 1))
    img.paste(tile, (col * 32, row * 32))
    img.save(path)


def block_uv(index):
    row, col = divmod(index, 4)
    # PIL 图像 y 向下，OBJ 的 vt 原点在左下 → v 要翻转
    return [((col + du) / 4.0, 1.0 - (row + dv) / 4.0) for du, dv in UV_TRI]


def to_unity(points):
    # 远端 GLB 是 Z-up 且 X 与 Unity 相反；det=-1，面绕序交换后两顶点
    return np.stack([-points[..., 0], -points[..., 2], points[..., 1]], axis=-1)


def write_obj(path, positions, normals, palette_ids):
    face_count = len(palette_ids)
    with open(path, "w", encoding="ascii") as fh:
        fh.write("mtllib {}.mtl\no {}\n".format(NAME, NAME))
        for p in positions.reshape(-1, 3):
            fh.write("v {:.6f} {:.6f} {:.6f}\n".format(*p))
        for n in normals:
            fh.write("vn {:.6f} {:.6f} {:.6f}\n".format(*n))
        for index in range(len(PALETTE)):
            for u, v in block_uv(index):
                fh.write("vt {:.6f} {:.6f}\n".format(u, v))
        fh.write("usemtl {}\ns off\n".format(NAME))
        for i in range(face_count):
            a, b, c = 3 * i + 1, 3 * i + 2, 3 * i + 3
            n = i + 1
            t = palette_ids[i] * 3 + 1
            fh.write("f {}/{}/{} {}/{}/{} {}/{}/{}\n".format(a, t, n, b, t + 1, n, c, t + 2, n))


def write_preview(path, positions, normals, palette_ids):
    """z-buffer 正交预览：避免画家算法在细长面上产生的假穿插线。"""
    xs, ys = positions[..., 0], positions[..., 1]
    min_x, max_x, min_y, max_y = xs.min(), xs.max(), ys.min(), ys.max()
    width = 960
    scale = (width - 40) / (max_x - min_x)
    height = int((max_y - min_y) * scale) + 40
    img = np.full((height, width, 3), 245, dtype=np.uint8)
    zbuf = np.full((height, width), np.inf, dtype=np.float32)

    light = np.array([-0.30, 0.62, -0.72])
    light /= np.linalg.norm(light)
    shade = 0.66 + 0.34 * np.clip(normals @ light, 0, 1)

    for i in range(len(palette_ids)):
        p = positions[i]
        xy = np.array([
            [20 + (p[0, 0] - min_x) * scale, height - 20 - (p[0, 1] - min_y) * scale],
            [20 + (p[1, 0] - min_x) * scale, height - 20 - (p[1, 1] - min_y) * scale],
            [20 + (p[2, 0] - min_x) * scale, height - 20 - (p[2, 1] - min_y) * scale],
        ])
        x0 = max(0, int(np.floor(xy[:, 0].min())))
        x1 = min(width, int(np.ceil(xy[:, 0].max())) + 1)
        y0 = max(0, int(np.floor(xy[:, 1].min())))
        y1 = min(height, int(np.ceil(xy[:, 1].max())) + 1)
        if x1 <= x0 or y1 <= y0:
            continue
        px, py = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        denom = (xy[1, 1] - xy[2, 1]) * (xy[0, 0] - xy[2, 0]) + (xy[2, 0] - xy[1, 0]) * (xy[0, 1] - xy[2, 1])
        if abs(denom) < 1e-9:
            continue
        w0 = ((xy[1, 1] - xy[2, 1]) * (px - xy[2, 0]) + (xy[2, 0] - xy[1, 0]) * (py - xy[2, 1])) / denom
        w1 = ((xy[2, 1] - xy[0, 1]) * (px - xy[2, 0]) + (xy[0, 0] - xy[2, 0]) * (py - xy[2, 1])) / denom
        w2 = 1.0 - w0 - w1
        z = w0 * p[0, 2] + w1 * p[1, 2] + w2 * p[2, 2]
        region = zbuf[y0:y1, x0:x1]
        inside = (w0 >= -0.002) & (w1 >= -0.002) & (w2 >= -0.002) & (z < region)
        if not inside.any():
            continue
        region[inside] = z[inside]
        color = np.clip(np.array(PALETTE[palette_ids[i]], dtype=np.float64) * shade[i], 0, 255).astype(np.uint8)
        img[y0:y1, x0:x1][inside] = color
    Image.fromarray(img).save(path)


def main():
    geom = list(trimesh.load(GLB, force="scene").geometry.values())[0]
    vertices = np.asarray(geom.vertices, dtype=np.float64)
    faces = np.asarray(geom.faces, dtype=np.int64)
    uv = np.asarray(geom.visual.uv, dtype=np.float64)
    texture = geom.visual.material.baseColorTexture.convert("RGB")

    # 面 UV 直接来自远端模型，不做任何近似或重投影
    face_uv = uv[faces].mean(axis=1)
    small = texture.resize((1024, 1024), Image.BILINEAR).filter(ImageFilter.MedianFilter(3))
    pixels = np.asarray(small).astype(np.float32)
    wrapped = np.mod(face_uv, 1.0)
    xi = np.clip((wrapped[:, 0] * 1023).astype(int), 0, 1023)
    yi = np.clip((wrapped[:, 1] * 1023).astype(int), 0, 1023)
    sampled = pixels[yi, xi]

    yellow_score = pixels[..., 0] - pixels[..., 2]
    best_yellow = np.full(len(xi), -1.0, dtype=np.float32)
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-2, -1, 0, 1, 2):
            xx = np.clip(xi + dx, 0, 1023)
            yy = np.clip(yi + dy, 0, 1023)
            best_yellow = np.maximum(best_yellow, yellow_score[yy, xx])

    luminance = sampled @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    palette_ids = np.clip(
        (np.power(np.clip(luminance, 0, 255) / 255.0, 0.55) * 9).astype(int), 0, 8
    )
    yellow = (best_yellow > 70) & (sampled[:, 0] > sampled[:, 2] * 1.25)
    palette_ids[yellow] = STRIPE
    palette_ids[yellow & (luminance > 150)] = 9

    positions = to_unity(vertices[faces])[:, [0, 2, 1], :]
    normals = np.cross(positions[:, 1] - positions[:, 0], positions[:, 2] - positions[:, 0])
    length = np.linalg.norm(normals, axis=1)
    length[length == 0] = 1.0
    normals /= length[:, None]

    build_palette(os.path.join(OUT, NAME + "_Palette.png"))
    write_obj(os.path.join(OUT, NAME + ".obj"), positions, normals, palette_ids)
    with open(os.path.join(OUT, NAME + ".mtl"), "w", encoding="ascii") as fh:
        fh.write(
            "newmtl {0}\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nd 1\nNs 0\nillum 1\n"
            "map_Kd {0}_Palette.png\n".format(NAME)
        )
    write_preview(os.path.join(OUT, NAME + "_Preview.png"), positions, normals, palette_ids)

    print(json.dumps({
        "source_faces": len(faces),
        "split_vertices": len(faces) * 3,
        "degenerate_faces": int((length < 1e-12).sum()),
        "palette_distribution": np.bincount(palette_ids, minlength=len(PALETTE)).tolist(),
        "bounds_unity": np.stack([
            positions.reshape(-1, 3).min(0), positions.reshape(-1, 3).max(0)
        ]).round(3).tolist(),
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
