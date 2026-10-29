import bpy
from mathutils import Vector

ob = bpy.data.objects["Gear_Colette_v5"]
verts = [v.co for v in ob.data.vertices]
zmin = min(verts, key=lambda v: v.z)
print("zmin", round(zmin.z, 4), "at x", round(zmin.x, 3), "y", round(zmin.y, 3))
near = [v for v in verts if v.z < zmin.z + 0.005]
print("near count", len(near))
for v in near[:8]:
    print("  ", round(v.x, 3), round(v.y, 3), round(v.z, 4))
