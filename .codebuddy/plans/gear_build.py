"""COLETTE WAVE COOKER —— 低模 v4（按 Concept Sheet SIDE VIEW 逐像素量取）
换算：侧视图 540 px = 1.0 m；高度 350 px = 0.648 m
坐标：X 长轴（左 -X / 右 +X）、Z 上、Y 厚度；无倒角
"""
import bpy
import math
import os
import mathutils

OUT_DIR = r"E:\Bluedivers\.codebuddy\out"
PARTS = []


def _mat(name, color, rough=0.5, emit=0.0, metal=0.0):
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


# ⚠ 材质会跨次运行留在 blend 里，同名 get 到旧的（改参数不生效）⇒ 先清
for _m in list(bpy.data.materials):
    if _m.name.startswith("LP_") or _m.name.startswith("REF_"):
        bpy.data.materials.remove(_m)

M_WHITE = _mat("LP_White", (0.88, 0.86, 0.81), 0.45)
M_NAVY = _mat("LP_Navy", (0.09, 0.10, 0.13), 0.4)
M_STEEL = _mat("LP_Steel", (0.52, 0.54, 0.57), 0.3, metal=0.8)
M_COPPER = _mat("LP_Copper", (0.34, 0.18, 0.08), 0.45)
M_RED = _mat("LP_Red", (0.60, 0.07, 0.05), 0.5)
M_ORANGE = _mat("LP_Orange", (0.82, 0.30, 0.05), 0.5)
M_GREEN = _mat("LP_Green", (0.07, 0.66, 0.34), 0.25, emit=1.0)


def box(name, cx, cz, w, h, d, rot_y=0.0, mat=None, cy=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(cx, cy, cz), rotation=(0, math.radians(rot_y), 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = (w, d, h)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        ob.data.materials.append(mat)
    PARTS.append(ob)
    return ob


def cyl(name, cx, cz, r, depth, axis="Z", verts=8, mat=None, spin=0.0, cy=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=(0, 0, 0),
                                        rotation=(0, 0, math.radians(spin)))
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    ob.rotation_euler = {"Z": (0, 0, 0), "Y": (math.radians(90), 0, 0), "X": (0, math.radians(90), 0)}[axis]
    ob.location = (cx, cy, cz)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if mat:
        ob.data.materials.append(mat)
    PARTS.append(ob)
    return ob


def radial_bolt(name, cx, cz, ring_r, r, length, angle_deg, mat, cy=0.0):
    a = math.radians(angle_deg)
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=r, depth=length, location=(0, 0, 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_euler = (a - math.pi / 2, 0, 0)
    ob.location = (cx, cy + ring_r * math.cos(a), cz + ring_r * math.sin(a))
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if mat:
        ob.data.materials.append(mat)
    PARTS.append(ob)
    return ob


for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)

# =====================================================================
# 1 左侧大框（侧视 X -0.50..-0.06，Z -0.29..+0.19，中间大开口）
# =====================================================================
box("FrameTop", -0.280, 0.155, 0.440, 0.070, 0.110, mat=M_WHITE)
box("FrameBot", -0.280, -0.252, 0.440, 0.072, 0.110, mat=M_WHITE)
box("FrameL", -0.472, -0.050, 0.056, 0.350, 0.110, mat=M_WHITE)
box("FrameR", -0.083, -0.050, 0.055, 0.350, 0.110, mat=M_WHITE)
box("FrameTrimTop", -0.280, 0.192, 0.420, 0.014, 0.114, mat=M_NAVY)
box("FrameTrimL", -0.494, -0.050, 0.014, 0.340, 0.114, mat=M_NAVY)
box("FrameTrimR", -0.062, -0.050, 0.014, 0.340, 0.114, mat=M_NAVY)
box("FrameTrimBot", -0.280, -0.290, 0.420, 0.014, 0.114, mat=M_NAVY)
box("FrameFootL", -0.462, -0.312, 0.100, 0.048, 0.120, mat=M_NAVY)
box("FrameFootR", -0.095, -0.312, 0.070, 0.048, 0.120, mat=M_NAVY)
box("FrameRedMark", -0.460, 0.096, 0.030, 0.034, 0.116, mat=M_RED)
# 红色竖握把（参考 3/4 渲染机身下方的红色棱纹握把）
box("GripRed", -0.300, -0.360, 0.075, 0.190, 0.090, rot_y=-12, mat=M_RED)
for i in range(4):
    box("GripRidge%d" % i, -0.318 + i * 0.014, -0.368, 0.009, 0.160, 0.096, mat=M_NAVY)
box("FrameGrip", -0.435, -0.120, 0.026, 0.130, 0.125, mat=M_NAVY)
# 框内斜拉杆（棕色）：从 (-0.463,-0.185) 到 (-0.130,+0.028)；⚠ 绕 Y 正角让 +X 端下沉，要右上必须负角
box("FrameBar", -0.296, -0.078, 0.360, 0.026, 0.055, rot_y=-28, mat=M_COPPER)
box("FrameBarClampL", -0.452, -0.176, 0.045, 0.048, 0.062, mat=M_NAVY)
box("FrameBarClampR", -0.140, 0.020, 0.045, 0.048, 0.062, mat=M_NAVY)

# =====================================================================
# 2 散热肋盒（坐在框顶，X -0.41..-0.17，Z 0.15..0.26）
# =====================================================================
box("RibCore", -0.287, 0.205, 0.240, 0.100, 0.140, mat=M_COPPER)
for i in range(10):
    box("Rib%d" % i, -0.392 + i * 0.023, 0.205, 0.010, 0.092, 0.148, mat=M_COPPER)
box("RibFrameTop", -0.287, 0.262, 0.246, 0.016, 0.152, mat=M_WHITE)
box("RibFrameL", -0.408, 0.205, 0.020, 0.115, 0.152, mat=M_WHITE)
box("RibFrameR", -0.166, 0.205, 0.020, 0.115, 0.152, mat=M_WHITE)

# =====================================================================
# 3 MULTISTAGE FOCUSING ARRAY（白箱在散热盒上方/右侧）
# =====================================================================
box("FocusBox", -0.200, 0.268, 0.290, 0.104, 0.130, mat=M_WHITE)
box("FocusDarkL", -0.330, 0.268, 0.052, 0.096, 0.132, rot_y=16, mat=M_NAVY)
box("FocusRed", -0.166, 0.232, 0.036, 0.034, 0.136, mat=M_RED)
box("FocusLabel", -0.046, 0.264, 0.056, 0.046, 0.136, mat=M_NAVY)
box("FocusCap", -0.106, 0.330, 0.048, 0.026, 0.090, mat=M_ORANGE)
box("FocusRidge", -0.200, 0.222, 0.270, 0.014, 0.132, mat=M_NAVY)
# 天线
box("AntUp", -0.416, 0.296, 0.018, 0.056, 0.020, mat=M_NAVY)
box("AntTop", -0.386, 0.322, 0.078, 0.016, 0.020, mat=M_NAVY)
box("AntTip", -0.352, 0.308, 0.016, 0.030, 0.020, mat=M_NAVY)

# =====================================================================
# 4 POWER CELL 中央立柱（X -0.056..+0.037，Z -0.157..+0.222）
# =====================================================================
box("Column", -0.010, 0.020, 0.092, 0.340, 0.120, mat=M_WHITE)
box("ColumnTrim", -0.010, -0.070, 0.098, 0.016, 0.126, mat=M_NAVY)
box("ColumnClamp", -0.010, 0.196, 0.106, 0.036, 0.128, mat=M_NAVY)
cyl("ColumnThread", -0.010, 0.145, 0.044, 0.085, axis="Z", verts=12, mat=M_STEEL, spin=15)
for i in range(3):
    cyl("ThreadRing%d" % i, -0.010, 0.118 + i * 0.028, 0.048, 0.010, axis="Z", verts=12, mat=M_NAVY)
cyl("ColumnBolt", -0.010, -0.120, 0.046, 0.070, axis="Z", verts=8, mat=M_NAVY, spin=22.5)
box("ColumnBoltWing", -0.010, -0.120, 0.140, 0.028, 0.044, mat=M_STEEL)

box("ReceiverBand", -0.120, 0.070, 0.560, 0.070, 0.140, mat=M_NAVY)
box("ReceiverBandTop", -0.120, 0.112, 0.540, 0.016, 0.146, mat=M_STEEL)
box("ReceiverBandEnd", -0.400, 0.070, 0.030, 0.070, 0.140, mat=M_WHITE)

# =====================================================================
# 5 主横杆两根（Z 0.027）+ 两道粗卡箍
# =====================================================================
for s, yy in (("A", 0.088), ("B", -0.088)):
    cyl("MainRod" + s, -0.090, 0.027, 0.021, 0.820, axis="X", verts=10, mat=M_STEEL, cy=yy, spin=18)
cyl("CollarA", 0.037, 0.027, 0.062, 0.090, axis="X", verts=10, mat=M_WHITE, spin=18)
cyl("CollarARing", 0.037, 0.027, 0.066, 0.016, axis="X", verts=10, mat=M_NAVY, spin=18)
cyl("CollarB", 0.163, 0.027, 0.058, 0.085, axis="X", verts=10, mat=M_WHITE, spin=18)
cyl("CollarBRing", 0.163, 0.027, 0.062, 0.016, axis="X", verts=10, mat=M_NAVY, spin=18)

# =====================================================================
# 6 MULTI-DISC CORE：深色壳体 + 铜色递减碟片 + 分段枪管
# =====================================================================
box("CoreHousing", 0.092, 0.010, 0.112, 0.168, 0.165, mat=M_NAVY)
cyl("CoreDiscA", 0.169, 0.005, 0.112, 0.042, axis="X", verts=8, mat=M_COPPER, spin=22.5)
cyl("CoreDiscB", 0.209, 0.005, 0.104, 0.040, axis="X", verts=8, mat=M_COPPER, spin=22.5)
cyl("CoreDiscC", 0.245, 0.005, 0.096, 0.038, axis="X", verts=8, mat=M_COPPER, spin=22.5)
cyl("CoreDiscD", 0.277, 0.005, 0.088, 0.036, axis="X", verts=8, mat=M_COPPER, spin=22.5)
cyl("CoreShaft", 0.240, 0.005, 0.040, 0.290, axis="X", verts=8, mat=M_NAVY, spin=22.5)
cyl("NoseBarrel", 0.330, 0.005, 0.042, 0.110, axis="X", verts=8, mat=M_NAVY, spin=22.5)
cyl("NoseBarrelRing1", 0.352, 0.005, 0.047, 0.014, axis="X", verts=8, mat=M_STEEL, spin=22.5)
cyl("NoseBarrelRing2", 0.384, 0.005, 0.045, 0.014, axis="X", verts=8, mat=M_STEEL, spin=22.5)

# =====================================================================
# 7 GLOWING EMITTER（八角盘，偏下 Z -0.044，r 0.100 + 绿六边 + 8 螺栓）
# =====================================================================
cyl("EmitPlate", 0.408, -0.044, 0.100, 0.048, axis="X", verts=8, mat=M_WHITE, spin=22.5)
cyl("EmitRim", 0.432, -0.044, 0.108, 0.020, axis="X", verts=8, mat=M_NAVY, spin=22.5)
cyl("EmitBezel", 0.444, -0.044, 0.078, 0.016, axis="X", verts=8, mat=M_WHITE, spin=22.5)
cyl("EmitCore", 0.454, -0.044, 0.056, 0.022, axis="X", verts=6, mat=M_GREEN, spin=0)
for i in range(8):
    radial_bolt("EmitBolt%d" % i, 0.416, -0.044, 0.112, 0.014, 0.026, 22.5 + i * 45, M_STEEL)

# =====================================================================
# 8 顶部导轨（Z 0.204）+ 大卡箍
# =====================================================================
cyl("TopRail", -0.010, 0.204, 0.016, 0.620, axis="X", verts=8, mat=M_STEEL, spin=22.5)
cyl("TopRailCollar", 0.120, 0.204, 0.052, 0.080, axis="X", verts=10, mat=M_STEEL, spin=18)
cyl("TopRailRing", 0.120, 0.204, 0.062, 0.014, axis="X", verts=10, mat=M_NAVY, spin=18)
box("TopRailClampL", -0.150, 0.204, 0.030, 0.070, 0.070, mat=M_NAVY)
box("TopRailClampR", 0.250, 0.204, 0.028, 0.066, 0.066, mat=M_NAVY)
box("TopRailEnd", 0.300, 0.204, 0.018, 0.060, 0.060, mat=M_NAVY)
box("TopRailPost", 0.170, 0.150, 0.030, 0.110, 0.070, mat=M_WHITE)

# =====================================================================
# 9 底部：滑撬 / 三角端板 / V 支腿 / OVERHEAT SYNC / 橙色软管
# =====================================================================
box("Skid", 0.060, -0.266, 0.530, 0.038, 0.120, mat=M_WHITE)
box("SkidTrim", 0.060, -0.288, 0.520, 0.014, 0.126, mat=M_NAVY)
box("SkidTri", 0.268, -0.190, 0.110, 0.140, 0.105, rot_y=32, mat=M_WHITE)
box("SkidPost", 0.400, -0.190, 0.030, 0.140, 0.080, mat=M_WHITE)
box("VBraceA", 0.330, -0.222, 0.120, 0.024, 0.065, rot_y=-42, mat=M_WHITE)
box("VBraceB", 0.395, -0.222, 0.110, 0.024, 0.065, rot_y=40, mat=M_WHITE)
cyl("SyncPlug", 0.040, -0.190, 0.036, 0.090, axis="Z", verts=10, mat=M_STEEL, spin=18)
cyl("SyncCollar", 0.040, -0.160, 0.044, 0.024, axis="Z", verts=10, mat=M_NAVY, spin=18)
cyl("HoseLoop", -0.085, -0.300, 0.070, 0.030, axis="Y", verts=12, mat=M_ORANGE)
box("HoseNeck", -0.020, -0.250, 0.032, 0.070, 0.036, mat=M_ORANGE)
box("HoseFoot", -0.130, -0.330, 0.090, 0.030, 0.040, mat=M_ORANGE)

# =====================================================================
# 合并
# =====================================================================
bpy.ops.object.select_all(action="DESELECT")
for ob in PARTS:
    ob.select_set(True)
bpy.context.view_layer.objects.active = PARTS[0]
bpy.ops.object.join()
model = bpy.context.active_object
model.name = "Gear_Colette_LP"
bpy.ops.object.shade_flat()
print("dims", [round(v, 3) for v in model.dimensions])
print("verts", len(model.data.vertices), "faces", len(model.data.polygons))

# =====================================================================
# 渲染
# =====================================================================
os.makedirs(OUT_DIR, exist_ok=True)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
bpy.context.scene.collection.objects.link(cam)
bpy.context.scene.camera = cam

LIGHTS = (((0.9, -1.3, 1.5), (42, 0, 35), 80),
          ((-1.5, -1.0, 0.35), (78, 0, -58), 34),
          ((0.0, -2.2, 0.1), (90, 0, 0), 45))
for loc, rot, en in LIGHTS:
    bpy.ops.object.light_add(type="AREA", location=loc, rotation=tuple(math.radians(a) for a in rot))
    bpy.context.active_object.data.energy = en
    bpy.context.active_object.data.size = 2.2

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.eevee.taa_render_samples = 24
sc.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.015, 0.015, 0.02, 1)
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "High Contrast"

cam.data.type = "PERSP"
cam.data.lens = 55
cam.location = (1.55, -1.55, 0.62)
cam.rotation_euler = (cam.location - mathutils.Vector((0.0, 0.0, -0.02))).to_track_quat("Z", "Y").to_euler()
sc.render.resolution_x = 1100
sc.render.resolution_y = 640
sc.render.filepath = os.path.join(OUT_DIR, "gear_34.png")
bpy.ops.render.render(write_still=True)

cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.10
cam.location = (0, -3, 0)
cam.rotation_euler = (math.radians(90), 0, 0)
sc.render.resolution_x = 1000
sc.render.resolution_y = 648
sc.render.filepath = os.path.join(OUT_DIR, "gear_side.png")
bpy.ops.render.render(write_still=True)

cam.location = (3, 0, -0.02)
cam.rotation_euler = (math.radians(90), 0, math.radians(90))
cam.data.ortho_scale = 0.40
sc.render.resolution_x = 620
sc.render.resolution_y = 620
sc.render.filepath = os.path.join(OUT_DIR, "gear_face.png")
bpy.ops.render.render(write_still=True)

glb = os.path.join(OUT_DIR, "Gear_Colette_LP.glb")
bpy.ops.object.select_all(action="DESELECT")
model.select_set(True)
bpy.context.view_layer.objects.active = model
bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True)
print("done")
