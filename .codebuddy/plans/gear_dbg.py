import bpy

for ob in bpy.data.objects:
    if ob.type != "MESH":
        continue
    bb = [ob.matrix_world @ __import__("mathutils").Vector(c) for c in ob.bound_box]
    zmax = max(v.z for v in bb)
    zmin = min(v.z for v in bb)
    if zmax > 0.615:
        print("%-14s z %.3f..%.3f  x %.3f..%.3f" % (
            ob.name, zmin, zmax, min(v.x for v in bb), max(v.x for v in bb)))
# 顺带查 x 0.37..0.55 / z 0.35..0.40 区域有谁的包围盒覆盖
print("--- region x0.37-0.55 z0.35-0.40 ---")
for ob in bpy.data.objects:
    if ob.type != "MESH":
        continue
    bb = [ob.matrix_world @ __import__("mathutils").Vector(c) for c in ob.bound_box]
    if max(v.x for v in bb) > 0.36 and min(v.x for v in bb) < 0.56 \
            and max(v.z for v in bb) > 0.35 and min(v.z for v in bb) < 0.40:
        print("%-14s x %.3f..%.3f z %.3f..%.3f y %.3f..%.3f" % (
            ob.name, min(v.x for v in bb), max(v.x for v in bb),
            min(v.z for v in bb), max(v.z for v in bb),
            min(v.y for v in bb), max(v.y for v in bb)))
