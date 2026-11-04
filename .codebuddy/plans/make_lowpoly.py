# -*- coding: utf-8 -*-
"""把 AI 高模减面成 lowpoly，UV 重映射到纯色块调色板贴图（参考 CWC_Diff.png 色卡风格）。

产物落在 Assets/Art/Modle/Weapon/：OBJ + MTL + 调色板 PNG + 正交预览 PNG。
"""
import json
import os

import fast_simplification as fs
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.spatial import cKDTree
import trimesh

GLB = r"E:\Bluedivers\Temp\gen3d_output\SciFiShotgun.glb"
OUT = r"E:\Bluedivers\Assets\Art\Modle\Weapon"
NAME = "SciFiShotgun_LowPoly"
TARGET_FACES = 6000

# 4x4 色卡：黑灰机匣 / 银灰枪管 / 黄橙装饰，12 号是黄黑警示斜纹
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
    # GLB 是 Z-up 且 X 与 Unity 相反；det=-1，故面绕序要交换后两顶点
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
    xs, ys = positions[..., 0], positions[..., 1]
    min_x, max_x, min_y, max_y = xs.min(), xs.max(), ys.min(), ys.max()
    width = 960
    scale = (width - 40) / (max_x - min_x)
    height = int((max_y - min_y) * scale) + 40

    def project(p):
        return (20 + (p[0] - min_x) * scale, height - 20 - (p[1] - min_y) * scale)

    img = Image.new("RGB", (width, height), (245, 245, 245))
    draw = ImageDraw.Draw(img)
    light = np.array([-0.30, 0.62, -0.72])
    light /= np.linalg.norm(light)
    shade = 0.62 + 0.38 * np.clip(normals @ light, 0, 1)
    # 相机在 -Z：先画 z 大（远）的面，再画 z 小（近）的面
    order = np.argsort(-positions[..., 2].mean(axis=1))
    for i in order:
        color = np.array(PALETTE[palette_ids[i]], dtype=np.float64) * shade[i]
        draw.polygon([project(p) for p in positions[i]], fill=tuple(color.astype(int)))
    img.save(path)


def main():
    scene = trimesh.load(GLB, force="scene")
    geom = list(scene.geometry.values())[0]
    vertices = np.asarray(geom.vertices, dtype=np.float64)
    faces = np.asarray(geom.faces, dtype=np.int32)
    uv = np.asarray(geom.visual.uv, dtype=np.float64)
    texture = geom.visual.material.baseColorTexture.convert("RGB")

    points, triangles = fs.simplify(
        vertices.astype(np.float32), faces, target_count=TARGET_FACES, agg=7
    )
    face_count = len(triangles)
    if not 4000 <= face_count <= 8000:
        raise RuntimeError("face count out of range: {}".format(face_count))

    # 简化后 UV 会丢，用最近原始顶点把 UV 近似带回来（原网格足够密）
    tree = cKDTree(vertices)
    _, nearest = tree.query(points.astype(np.float64), workers=-1)
    face_uv = uv[nearest][triangles].mean(axis=1)

    small = texture.resize((1024, 1024), Image.BILINEAR).filter(ImageFilter.MedianFilter(3))
    pixels = np.asarray(small).astype(np.float32)
    wrapped = np.mod(face_uv, 1.0)
    xi = np.clip((wrapped[:, 0] * 1023).astype(int), 0, 1023)
    yi = np.clip((wrapped[:, 1] * 1023).astype(int), 0, 1023)
    sampled = pixels[yi, xi]

    # 黄色装饰条在缩略图里只剩几像素，取 5x5 邻域里最黄的像素单独判一次
    yellow_score = pixels[..., 0] - pixels[..., 2]
    best_yellow = np.full(len(xi), -1.0, dtype=np.float32)
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-2, -1, 0, 1, 2):
            xx = np.clip(xi + dx, 0, 1023)
            yy = np.clip(yi + dy, 0, 1023)
            best_yellow = np.maximum(best_yellow, yellow_score[yy, xx])

    # 烘焙贴图暗部几乎全黑，最近色会把 70% 面压成纯黑；
    # 改成按亮度分档映射到灰阶色块，保住 lowpoly 的色块层次
    luminance = sampled @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    palette_ids = np.clip(
        (np.power(np.clip(luminance, 0, 255) / 255.0, 0.55) * 9).astype(int), 0, 8
    )
    yellow = (best_yellow > 70) & (sampled[:, 0] > sampled[:, 2] * 1.25)
    palette_ids[yellow] = STRIPE
    palette_ids[yellow & (luminance > 150)] = 9  # 高亮黄条给纯黄，其余给斜纹

    positions = to_unity(points[triangles])[:, [0, 2, 1], :]
    normals = np.cross(positions[:, 1] - positions[:, 0], positions[:, 2] - positions[:, 0])
    length = np.linalg.norm(normals, axis=1)
    length[length == 0] = 1.0
    normals /= length[:, None]

    # AI 生成网格的三角面绕序并不一致（约四成面法线朝内），逐面统一为朝外
    center = positions.reshape(-1, 3).mean(axis=0)
    flip = np.einsum("ij,ij->i", normals, positions.mean(axis=1) - center) < 0
    positions[flip] = positions[flip][:, [0, 2, 1], :]
    normals[flip] *= -1

    build_palette(os.path.join(OUT, NAME + "_Palette.png"))
    write_obj(os.path.join(OUT, NAME + ".obj"), positions, normals, palette_ids)
    with open(os.path.join(OUT, NAME + ".mtl"), "w", encoding="ascii") as fh:
        fh.write(
            "newmtl {0}\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nd 1\nNs 0\nillum 1\n"
            "map_Kd {0}_Palette.png\n".format(NAME)
        )
    write_preview(os.path.join(OUT, NAME + "_Preview.png"), positions, normals, palette_ids)

    counts = np.bincount(palette_ids, minlength=len(PALETTE)).tolist()
    print(json.dumps({
        "faces": face_count,
        "vertices": len(points),
        "split_vertices": face_count * 3,
        "palette_distribution": counts,
        "bounds_unity": np.stack([positions.reshape(-1, 3).min(0), positions.reshape(-1, 3).max(0)]).round(3).tolist(),
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
