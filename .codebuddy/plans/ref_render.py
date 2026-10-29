"""把线稿参考图放大渲染出来，方便看清结构线"""
import bpy
import math
import os

REF = r"C:\Users\admin\AppData\Local\CodeBuddyExtension\Data\745eef27-68fa-4891-82d2-f37630042ec6\VisualStudio\745eef27-68fa-4891-82d2-f37630042ec6\history\b52f89de48dac559bdc39636e3dceded\bf74d07bd86944a2b4075792834afcee\assets\image.54490e796b.png"
OUT = r"E:\Bluedivers\.codebuddy\out\ref_big.png"
os.makedirs(os.path.dirname(OUT), exist_ok=True)

for ob in bpy.data.objects:
    if ob.type == "MESH":
        ob.hide_render = True

img = bpy.data.images.load(REF, check_existing=True)
w, h = 1.42, 1.42 * 193 / 356.0
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0.6, 0), rotation=(math.radians(90), 0, 0))
p = bpy.context.active_object
p.scale = (w, h, 1)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

m = bpy.data.materials.new("REF")
m.use_nodes = True
nt = m.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
emi = nt.nodes.new("ShaderNodeEmission")
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = img
tex.interpolation = "Cubic"
emi.inputs["Strength"].default_value = 1.6
nt.links.new(tex.outputs["Color"], emi.inputs["Color"])
nt.links.new(emi.outputs["Emission"], out.inputs["Surface"])
p.data.materials.append(m)

cam = bpy.data.objects.get("Camera")
if cam is None:
    bpy.ops.object.camera_add(location=(0, -3, 0), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.active_object
cam.location = (0, -3, 0)
cam.rotation_euler = (math.radians(90), 0, 0)
cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.44
bpy.context.scene.camera = cam

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = 1420
sc.render.resolution_y = 763
sc.eevee.taa_render_samples = 8
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("ok", OUT, os.path.exists(OUT))
