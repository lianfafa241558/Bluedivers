"""把设定页的 SIDE VIEW 区域裁出来放大渲染，看清结构细节"""
import bpy
import math
import os

SHEET = r"C:\Users\admin\AppData\Local\CodeBuddyExtension\Data\745eef27-68fa-4891-82d2-f37630042ec6\VisualStudio\745eef27-68fa-4891-82d2-f37630042ec6\history\b52f89de48dac559bdc39636e3dceded\bf74d07bd86944a2b4075792834afcee\assets\Gemini_Generated_Image_7achav7achav7ach (1).26699ff73c.jpg"
OUT = r"E:\Bluedivers\.codebuddy\out\ref_side_big.png"
# 侧视图在设定页里的像素范围（1090x580）
X0, X1, Y0, Y1 = 25, 645, 115, 525
W, H = 1090.0, 580.0

for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img = bpy.data.images.load(SHEET, check_existing=True)
if img.size[0] != 1090:
    W, H = float(img.size[0]), float(img.size[1])

pw = 1.40
ph = pw * (Y1 - Y0) / (X1 - X0)
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0.6, 0), rotation=(math.radians(90), 0, 0))
p = bpy.context.active_object
p.scale = (pw, ph, 1)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# 直接把 UV 改到子矩形（v 轴在图像里是自下而上）
uv = p.data.uv_layers[0]
for loop in p.data.loops:
    co = p.data.vertices[loop.vertex_index].co
    u = X0 / W + (co.x + pw / 2) / pw * ((X1 - X0) / W)
    v = (H - Y1) / H + (co.y + ph / 2) / ph * ((Y1 - Y0) / H)
    uv.data[loop.index].uv = (u, v)

m = bpy.data.materials.new("SHEET")
m.use_nodes = True
nt = m.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
emi = nt.nodes.new("ShaderNodeEmission")
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = img
tex.interpolation = "Cubic"
emi.inputs["Strength"].default_value = 1.0
nt.links.new(tex.outputs["Color"], emi.inputs["Color"])
nt.links.new(emi.outputs["Emission"], out.inputs["Surface"])
p.data.materials.append(m)

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
bpy.context.scene.collection.objects.link(cam)
bpy.context.scene.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = pw * 1.02
cam.location = (0, -2, 0)
cam.rotation_euler = (math.radians(90), 0, 0)

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = 1400
sc.render.resolution_y = int(1400 * ph / pw)
sc.eevee.taa_render_samples = 8
sc.view_settings.view_transform = "Standard"
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("ok", OUT, os.path.exists(OUT))
