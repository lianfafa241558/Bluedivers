# -*- coding: utf-8 -*-
"""远端 quad LowPoly → 线性细分(面数×4) → Palette Texture / Color Atlas。

- 几何只做共面线性细分：新点都在原面内，形状不变，quad 拓扑保持且更规整。
- OBJ 的 v 保持共享顶点（干净拓扑），vt/vn 按面角独立，Unity 导入时再自动拆分。
- 产物覆盖 Assets/Art/Modle/Weapon/SciFiShotgun_LowPoly.*。
"""
import io
import json
import math
import os
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC = r"E:\Bluedivers\Temp\gen3d_quad_out\q10k\Gun.zip"
OUT = r"E:\Bluedivers\Assets\Art\Modle\Weapon"
NAME = "SciFiShotgun_LowPoly"

PALETTE = [
    (16, 17, 20), (30, 32, 35), (44, 48, 52), (61, 67, 74),
    (85, 92, 99), (118, 126, 133), (168, 174, 180), (216, 220, 224),
    (242, 243, 244), (245, 179, 1), (217, 142, 0), (138, 90, 0),
    (245, 179, 1), (35, 38, 42), (106, 114, 122), (192, 160, 48),
]
STRIPE = 12


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


def uv_point(palette_id, corner, count):
    """色块内取一个小多边形，避免 UV 退化；斜纹块因此能显出条纹。"""
    row, col = divmod(palette_id, 4)
    angle = 2.0 * math.pi * corner / count
    du = 0.5 + 0.28 * math.cos(angle)
    dv = 0.5 + 0.28 * math.sin(angle)
    return (col + du) / 4.0, 1.0 - (row + dv) / 4.0


def load_obj(bundle):
    obj_name = next(n for n in bundle.namelist() if n.endswith(".obj"))
    lines = bundle.read(obj_name).decode("utf-8", "ignore").splitlines()
    verts, uvs, faces = [], [], []
    for line in lines:
        if line.startswith("v "):
            verts.append(tuple(float(x) for x in line.split()[1:4]))
        elif line.startswith("vt "):
            uvs.append(tuple(float(x) for x in line.split()[1:3]))
        elif line.startswith("f "):
            face = []
            for token in line.split()[1:]:
                parts = token.split("/")
                vi = int(parts[0]) - 1
                ti = int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1
                face.append((vi, ti))
            faces.append(face)
    tex_name = next(n for n in bundle.namelist() if n.lower().endswith(".png"))
    texture = Image.open(io.BytesIO(bundle.read(tex_name))).convert("RGB")
    return verts, uvs, faces, texture


def subdivide(verts, faces):
    """quad→4 quad、tri→4 tri，n-gon 原样保留；共享边中点，形状不变。"""
    points = list(verts)
    mid_cache = {}
    out_faces, parent = [], []

    def midpoint(a, b):
        key = (min(a, b), max(a, b))
        if key in mid_cache:
            return mid_cache[key]
        p = (np.asarray(points[a]) + np.asarray(points[b])) / 2.0
        index = len(points)
        points.append(tuple(p))
        mid_cache[key] = index
        return index

    for fi, face in enumerate(faces):
        idx = [corner[0] for corner in face]
        n = len(idx)
        if n == 4:
            mids = [midpoint(idx[i], idx[(i + 1) % 4]) for i in range(4)]
            center = len(points)
            points.append(tuple(np.mean([points[i] for i in idx], axis=0)))
            for i in range(4):
                out_faces.append([idx[i], mids[i], center, mids[(i - 1) % 4]])
                parent.append(fi)
        elif n == 3:
            a, b, c = idx
            m0, m1, m2 = midpoint(a, b), midpoint(b, c), midpoint(c, a)
            for tri in ([a, m0, m2], [m0, b, m1], [m2, m1, c], [m0, m1, m2]):
                out_faces.append(tri)
                parent.append(fi)
        else:
            out_faces.append(idx)
            parent.append(fi)
    return np.asarray(points, dtype=np.float64), out_faces, parent


def face_normal(points):
    """Newell 法：对任意凸/凹多边形都比前三点叉积稳。"""
    normal = np.zeros(3)
    for i in range(len(points)):
        p, q = points[i], points[(i + 1) % len(points)]
        normal[0] += (p[1] - q[1]) * (p[2] + q[2])
        normal[1] += (p[2] - q[2]) * (p[0] + q[0])
        normal[2] += (p[0] - q[0]) * (p[1] + q[1])
    length = np.linalg.norm(normal)
    return normal / length if length > 0 else np.array([0.0, 0.0, 1.0])


def assign_palette(faces, uvs, texture):
    small = texture.resize((1024, 1024), Image.BILINEAR).filter(ImageFilter.MedianFilter(3))
    pixels = np.asarray(small).astype(np.float32)
    xi_list, yi_list = [], []
    for face in faces:
        corners = [uvs[ti] for vi, ti in face if ti >= 0]
        u, v = np.mean(corners, axis=0) if corners else (0.5, 0.5)
        u, v = np.mod([u, v], 1.0)
        xi_list.append(int(np.clip(u * 1023, 0, 1023)))
        yi_list.append(int(np.clip((1.0 - v) * 1023, 0, 1023)))
    xi, yi = np.asarray(xi_list), np.asarray(yi_list)
    sampled = pixels[yi, xi]

    yellow_score = pixels[..., 0] - pixels[..., 2]
    best_yellow = np.full(len(xi), -1.0, dtype=np.float32)
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-2, -1, 0, 1, 2):
            xx, yy = np.clip(xi + dx, 0, 1023), np.clip(yi + dy, 0, 1023)
            best_yellow = np.maximum(best_yellow, yellow_score[yy, xx])

    luminance = sampled @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    ids = np.clip((np.power(np.clip(luminance, 0, 255) / 255.0, 0.55) * 9).astype(int), 0, 8)
    yellow = (best_yellow > 70) & (sampled[:, 0] > sampled[:, 2] * 1.25)
    ids[yellow] = STRIPE
    ids[yellow & (luminance > 150)] = 9
    return ids


def write_obj(path, points, faces, parent, parent_palette):
    with open(path, "w", encoding="ascii") as fh:
        fh.write("mtllib {}.mtl\no {}\n".format(NAME, NAME))
        for p in points:
            fh.write("v {:.6f} {:.6f} {:.6f}\n".format(*p))
        vt_lines, vn_lines, f_lines = [], [], []
        vt_count = vn_count = 0
        for i, face in enumerate(faces):
            pid = int(parent_palette[parent[i]])
            normal = face_normal(np.asarray([points[j] for j in face]))
            vn_count += 1
            vn_lines.append("vn {:.6f} {:.6f} {:.6f}\n".format(*normal))
            corners = []
            for corner, vi in enumerate(face):
                u, v = uv_point(pid, corner, len(face))
                vt_count += 1
                vt_lines.append("vt {:.6f} {:.6f}\n".format(u, v))
                corners.append("{}/{}/{}".format(vi + 1, vt_count, vn_count))
            f_lines.append("f {}\n".format(" ".join(corners)))
        fh.writelines(vt_lines)
        fh.writelines(vn_lines)
        fh.write("usemtl {}\ns off\n".format(NAME))
        fh.writelines(f_lines)


def write_preview(path, points, faces, parent, parent_palette):
    flat = points.reshape(-1, 3)
    xs, ys = flat[:, 0], flat[:, 1]
    min_x, max_x, min_y, max_y = xs.min(), xs.max(), ys.min(), ys.max()
    width = 960
    scale = (width - 40) / (max_x - min_x)
    height = int((max_y - min_y) * scale) + 40
    img = np.full((height, width, 3), 245, dtype=np.uint8)
    zbuf = np.full((height, width), np.inf, dtype=np.float32)
    light = np.array([-0.30, 0.62, -0.72])
    light /= np.linalg.norm(light)

    for i, face in enumerate(faces):
        shade = 0.66 + 0.34 * max(0.0, float(np.dot(face_normal([points[j] for j in face]), light)))
        color = np.clip(np.asarray(PALETTE[int(parent_palette[parent[i]])]) * shade, 0, 255).astype(np.uint8)
        poly = np.asarray([points[j] for j in face])
        for k in range(1, len(poly) - 1):
            tri = np.asarray([poly[0], poly[k], poly[k + 1]])
            xy = np.array([[20 + (p[0] - min_x) * scale, height - 20 - (p[1] - min_y) * scale] for p in tri])
            x0, x1 = max(0, int(np.floor(xy[:, 0].min()))), min(width, int(np.ceil(xy[:, 0].max())) + 1)
            y0, y1 = max(0, int(np.floor(xy[:, 1].min()))), min(height, int(np.ceil(xy[:, 1].max())) + 1)
            if x1 <= x0 or y1 <= y0:
                continue
            px, py = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            denom = (xy[1, 1] - xy[2, 1]) * (xy[0, 0] - xy[2, 0]) + (xy[2, 0] - xy[1, 0]) * (xy[0, 1] - xy[2, 1])
            if abs(denom) < 1e-9:
                continue
            w0 = ((xy[1, 1] - xy[2, 1]) * (px - xy[2, 0]) + (xy[2, 0] - xy[1, 0]) * (py - xy[2, 1])) / denom
            w1 = ((xy[2, 1] - xy[0, 1]) * (px - xy[2, 0]) + (xy[0, 0] - xy[2, 0]) * (py - xy[2, 1])) / denom
            w2 = 1.0 - w0 - w1
            z = w0 * tri[0, 2] + w1 * tri[1, 2] + w2 * tri[2, 2]
            region = zbuf[y0:y1, x0:x1]
            inside = (w0 >= -0.002) & (w1 >= -0.002) & (w2 >= -0.002) & (z < region)
            if inside.any():
                region[inside] = z[inside]
                img[y0:y1, x0:x1][inside] = color
    Image.fromarray(img).save(path)


def main():
    with zipfile.ZipFile(SRC) as bundle:
        verts, uvs, faces, texture = load_obj(bundle)

    parent_palette = assign_palette(faces, uvs, texture)
    points, out_faces, parent = subdivide(verts, faces)

    build_palette(os.path.join(OUT, NAME + "_Palette.png"))
    write_obj(os.path.join(OUT, NAME + ".obj"), points, out_faces, parent, parent_palette)
    with open(os.path.join(OUT, NAME + ".mtl"), "w", encoding="ascii") as fh:
        fh.write(
            "newmtl {0}\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nd 1\nNs 0\nillum 1\n"
            "map_Kd {0}_Palette.png\n".format(NAME)
        )
    write_preview(os.path.join(OUT, NAME + "_Preview.png"), points, out_faces, parent, parent_palette)

    counts = {}
    for face in out_faces:
        counts[len(face)] = counts.get(len(face), 0) + 1
    print(json.dumps({
        "source_faces": len(faces),
        "output_faces": len(out_faces),
        "output_faces_by_vertices": counts,
        "unity_triangles": sum(len(f) - 2 for f in out_faces),
        "shared_vertices": len(points),
        "palette_distribution": np.bincount(parent_palette, minlength=len(PALETTE)).tolist(),
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
