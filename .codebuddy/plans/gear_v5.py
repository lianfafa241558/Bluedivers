"""Gear_Colette v5：主体 = body_boxes.json（三视图掩膜反推）+ 手工细节件。
渲染 侧视/正视 透明底 PNG + 导出 GLB，供 gear_v5_cmp.py 做 IoU 比对。
"""
import bpy
import json
import math
import os

OUT = "E:/Bluedivers/.codebuddy/out"
PARTS = []

for _m in list(bpy.data.materials):
    if _m.name.startswith("LP_"):
        bpy.data.materials.remove(_m)
for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)


def mat(name, color, rough=0.5, emit=0.0, metal=0.0):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission"].default_value = (*color, 1)
        b.inputs["Emission Strength"].default_value = emit
    return m


M = {
    "W": mat("LP_White", (0.87, 0.85, 0.79), 0.45),
    "N": mat("LP_Navy", (0.06, 0.06, 0.08), 0.5),
    "C": mat("LP_Copper", (0.55, 0.34, 0.16), 0.4, metal=0.5),
    "O": mat("LP_Orange", (0.80, 0.44, 0.10), 0.5),
    "G": mat("LP_Green", (0.30, 0.95, 0.62), 0.3, emit=2.5),
    "R": mat("LP_Red", (0.68, 0.12, 0.08), 0.5),
    "S": mat("LP_Steel", (0.40, 0.42, 0.45), 0.35, metal=0.7),
    "D": mat("LP_Olive", (0.28, 0.31, 0.23), 0.6),
}


def finish(ob, m):
    ob.data.materials.append(m)
    PARTS.append(ob)
    return ob


def box(name, x0, x1, y0, y1, z0, z1, m):
    bpy.ops.mesh.primitive_cube_add(size=1, location=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = (max(1e-4, x1 - x0), max(1e-4, y1 - y0), max(1e-4, z1 - z0))
    bpy.ops.object.transform_apply(scale=True)
    return finish(ob, m)


def cylx(name, x0, x1, cy, cz, r, m, verts=10, spin=0.0):
    """沿 X 轴的圆柱"""
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=max(1e-4, x1 - x0),
                                        location=(0, 0, 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_euler = (0, math.radians(90), math.radians(spin))
    ob.location = ((x0 + x1) / 2, cy, cz)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return finish(ob, m)


def cylz(name, z0, z1, cx, cy, r, m, verts=10, spin=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=max(1e-4, z1 - z0),
                                        location=(0, 0, 0), rotation=(0, 0, math.radians(spin)))
    ob = bpy.context.active_object
    ob.name = name
    ob.location = (cx, cy, (z0 + z1) / 2)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return finish(ob, m)


def tube(name, p0, p1, r, m, verts=8):
    """任意两点间圆柱（软管/连杆用）"""
    import mathutils
    p0, p1 = mathutils.Vector(p0), mathutils.Vector(p1)
    d = p1 - p0
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=(0, 0, 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    ob.location = (p0 + p1) / 2
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return finish(ob, m)


def radial(name, cx, cz, r0, r1, ang, r, m, verts=8):
    """YZ 平面放射圆柱，从半径 r0 到 r1，ang 单位度（0=+Y）"""
    a = math.radians(ang)
    mid = (r0 + r1) / 2
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=r1 - r0, location=(0, 0, 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_euler = (a - math.pi / 2, 0, 0)
    ob.location = (cx, mid * math.cos(a), cz + mid * math.sin(a))
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return finish(ob, m)


# ============ 1) 主体盒（自动反推） ============
with open(os.path.join(OUT, "body_boxes.json")) as f:
    boxes = json.load(f)
for i, b in enumerate(boxes):
    # 后枪管区用圆柱重建，跳过扁盒；握柱+顶板自动盒被 Y 过宽，拆成手工件
    if b["x1"] < 0.09 and b["z0"] > 0.3:
        continue
    if b["x0"] > 0.24 and b["x1"] < 0.33 and b["z1"] > 0.55:
        continue
    box("Auto%02d" % i, b["x0"], b["x1"], b["y0"], b["y1"], b["z0"], b["z1"], M[b["c"]])

# 握柱（铜，细）+ 顶板（白）+ 红钮 + 准星（正视里这组要窄）
box("GripCol", 0.250, 0.321, -0.055, 0.055, 0.082, 0.500, M["C"])
box("TopPlate", 0.225, 0.345, -0.100, 0.100, 0.500, 0.585, M["W"])
cylz("RedButton", 0.575, 0.605, 0.300, 0, 0.020, M["R"], verts=10)
box("Sight", 0.255, 0.300, -0.022, 0.022, 0.585, 0.612, M["N"])
# 机身左段与框架条之间的竖缝填充（参考侧视该列实心）
box("BodyFill", 0.133, 0.158, -0.09, 0.09, 0.119, 0.560, M["W"])

# ============ 2) 手工件 ============
# 后枪管（圆柱 + 消焰器 + 座圈）
cylx("Barrel", -0.008, 0.068, 0, 0.436, 0.055, M["S"], verts=10)
cylx("BarrelTip", -0.002, 0.012, 0, 0.436, 0.064, M["N"], verts=10)
cylx("BarrelCollar", 0.058, 0.085, 0, 0.436, 0.070, M["D"], verts=10)

# 顶部双导轨 + 卡箍
for s, yy in (("L", 0.13), ("R", -0.13)):
    cylx("Rail" + s, 0.33, 0.86, yy, 0.5335, 0.014, M["S"], verts=8)
for xx in (0.40, 0.56, 0.72, 0.845):
    for yy in (0.13, -0.13):
        box("RailClamp", xx - 0.018, xx + 0.018, yy - 0.026, yy + 0.026, 0.510, 0.550, M["N"])

# 底部纵杆（伸到盘后）
cylx("BottomRod", 0.55, 0.995, 0, 0.149, 0.016, M["S"], verts=8)
cylx("BottomRodCollar", 0.845, 0.875, 0, 0.149, 0.030, M["N"], verts=8)

# 软管（橙色，机体底部）
HOSE = [(0.345, 0, 0.170), (0.400, 0, 0.085), (0.475, 0, 0.045), (0.545, 0, 0.085), (0.595, 0, 0.150)]
for i in range(len(HOSE) - 1):
    tube("Hose%d" % i, HOSE[i], HOSE[i + 1], 0.038, M["O"])
for i in range(1, len(HOSE) - 1):
    tube("HoseRing%d" % i, (HOSE[i][0] - 0.014, 0, HOSE[i][2]),
         (HOSE[i][0] + 0.014, 0, HOSE[i][2]), 0.044, M["N"])

# 发射盘（参考侧视盘体 x 0.919..0.955，环全部收在此范围内）+ 绿六边芯
cylx("DiscWhite", 0.915, 0.940, 0, 0.268, 0.145, M["W"], verts=8, spin=22.5)
cylx("DiscCopper", 0.938, 0.948, 0, 0.268, 0.118, M["C"], verts=8, spin=22.5)
cylx("DiscNavy", 0.946, 0.953, 0, 0.268, 0.092, M["N"], verts=8, spin=22.5)
cylx("DiscCore", 0.946, 0.968, 0, 0.235, 0.062, M["G"], verts=6, spin=0)
# 芯前凸台（参考侧视 x 0.955..0.985 有 z 0.135..0.335 的柱）
cylx("CoreBoss", 0.953, 0.985, 0, 0.235, 0.095, M["N"], verts=10)

# 锥尖（盘后板之间可见的铜色段）
box("ConeTip", 0.838, 0.862, -0.05, 0.05, 0.135, 0.330, M["C"])

# 周向 6 根放射连接座：水平两根粗长（决定正视宽度），上斜两根短，下斜两根中等
for a in (12, 168):
    radial("Conn%d" % a, 0.942, 0.268, 0.135, 0.235, a, 0.055, M["D"])
    radial("ConnTip%d" % a, 0.942, 0.268, 0.235, 0.255, a, 0.060, M["S"])
for a in (45, 135):
    radial("Conn%d" % a, 0.945, 0.268, 0.135, 0.155, a, 0.035, M["D"])
    radial("ConnTip%d" % a, 0.945, 0.268, 0.155, 0.185, a, 0.042, M["S"])
for a in (200, 340):
    radial("Conn%d" % a, 0.945, 0.268, 0.135, 0.165, a, 0.045, M["D"])
    radial("ConnTip%d" % a, 0.945, 0.268, 0.165, 0.190, a, 0.052, M["S"])
# 机框顶横梁正视加宽（参考正视 z 0.42..0.52 处 y ±0.12）
box("FrameBarWide", 0.155, 0.250, -0.12, 0.12, 0.420, 0.520, M["N"])

# 中位纵杆两根（参考侧视：z 0.376..0.402 与 0.475..0.501，都伸到右板末端）
cylx("MidRodA", 0.42, 0.862, 0, 0.389, 0.015, M["S"], verts=8)
cylx("MidRodB", 0.42, 0.862, 0, 0.488, 0.014, M["S"], verts=8)

# 前握把（盘下橙色竖握把）
cylz("ForeGrip", 0.022, 0.130, 0.925, 0, 0.030, M["O"], verts=10)
cylz("ForeGripCap", 0.022, 0.052, 0.925, 0, 0.038, M["N"], verts=6)

# ============ 合并 / 平直着色 ============
bpy.ops.object.select_all(action="DESELECT")
for ob in PARTS:
    ob.select_set(True)
bpy.context.view_layer.objects.active = PARTS[0]
bpy.ops.object.join()
model = bpy.context.active_object
model.name = "Gear_Colette_v5"
bpy.ops.object.shade_flat()
print("dims", [round(v, 3) for v in model.dimensions],
      "verts", len(model.data.vertices), "faces", len(model.data.polygons))

# ============ 渲染：侧视 / 正视，透明底 ============
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.eevee.taa_render_samples = 16
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"

# 侧视：-Y 看 +X 方向，画面右 = +X
cam.data.ortho_scale = 1.06
cam.location = (0.5, -3, 0.31)
cam.rotation_euler = (math.radians(90), 0, 0)
sc.render.resolution_x, sc.render.resolution_y = 800, 500
sc.render.filepath = os.path.join(OUT, "v5_side.png")
bpy.ops.render.render(write_still=True)

# 正视：+X 看 -X 方向（发射盘正对相机）
cam.data.ortho_scale = 0.62
cam.location = (3, 0, 0.30)
cam.rotation_euler = (math.radians(90), 0, math.radians(90))
sc.render.resolution_x, sc.render.resolution_y = 620, 760
sc.render.filepath = os.path.join(OUT, "v5_front.png")
bpy.ops.render.render(write_still=True)

# 3/4 美观渲染（打光，供目视）
sc.render.film_transparent = False
sc.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.015, 0.015, 0.02, 1)
for loc, rot, en in ((((0.9, -1.3, 1.5), (42, 0, 35), 80)),
                     ((-1.5, -1.0, 0.35), (78, 0, -58), 34),
                     ((0.0, -2.2, 0.1), (90, 0, 0), 45)):
    bpy.ops.object.light_add(type="AREA", location=loc,
                             rotation=tuple(math.radians(a) for a in rot))
    bpy.context.active_object.data.energy = en
    bpy.context.active_object.data.size = 2.2
cam.data.type = "PERSP"
cam.data.lens = 55
cam.location = (1.55, -1.55, 0.55)
cam.rotation_euler = (cam.location - __import__("mathutils").Vector((0.4, 0, 0.0))).to_track_quat("Z", "Y").to_euler()
sc.render.resolution_x, sc.render.resolution_y = 1100, 640
sc.render.filepath = os.path.join(OUT, "v5_34.png")
bpy.ops.render.render(write_still=True)

# ============ GLB ============
bpy.ops.object.select_all(action="DESELECT")
model.select_set(True)
bpy.context.view_layer.objects.active = model
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "Gear_Colette_v5.glb"),
                          export_format="GLB", use_selection=True)
print("done")
