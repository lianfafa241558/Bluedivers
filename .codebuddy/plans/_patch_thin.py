"""把各部件厚度统一压薄 ~12%，并提亮灯光。一次性补丁脚本，跑完可删。"""
import io

P = r"e:\Bluedivers\.codebuddy\plans\gear_build.py"
REPL = {
    'box("RibFrameTop", -0.225, 0.187, 0.235, 0.020, 0.165': 'box("RibFrameTop", -0.225, 0.187, 0.235, 0.020, 0.145',
    'box("RibFrameBot", -0.225, 0.090, 0.235, 0.020, 0.165': 'box("RibFrameBot", -0.225, 0.090, 0.235, 0.020, 0.145',
    'box("RibFrameL", -0.335, 0.138, 0.022, 0.100, 0.165': 'box("RibFrameL", -0.335, 0.138, 0.022, 0.100, 0.145',
    'box("RibFrameR", -0.115, 0.138, 0.022, 0.100, 0.165': 'box("RibFrameR", -0.115, 0.138, 0.022, 0.100, 0.145',
    'box("RibCore", -0.225, 0.138, 0.200, 0.075, 0.130': 'box("RibCore", -0.225, 0.138, 0.200, 0.075, 0.115',
    '0.009, 0.082, 0.140': '0.009, 0.082, 0.122',
    'box("HandleBot", -0.255, -0.196, 0.400, 0.048, 0.105': 'box("HandleBot", -0.255, -0.196, 0.400, 0.048, 0.092',
    'box("HandleL", -0.440, -0.075, 0.048, 0.290, 0.105': 'box("HandleL", -0.440, -0.075, 0.048, 0.290, 0.092',
    'box("HandleR", -0.075, -0.075, 0.048, 0.290, 0.105': 'box("HandleR", -0.075, -0.075, 0.048, 0.290, 0.092',
    'box("HandleSlope", -0.290, 0.088, 0.330, 0.048, 0.105': 'box("HandleSlope", -0.290, 0.088, 0.330, 0.048, 0.092',
    'box("HandleTrimBot", -0.255, -0.222, 0.380, 0.014, 0.108': 'box("HandleTrimBot", -0.255, -0.222, 0.380, 0.014, 0.094',
    'box("Skid", 0.130, -0.222, 0.480, 0.048, 0.130': 'box("Skid", 0.130, -0.222, 0.480, 0.048, 0.118',
    'box("SkidTrim", 0.130, -0.252, 0.470, 0.016, 0.136': 'box("SkidTrim", 0.130, -0.252, 0.470, 0.016, 0.124',
    'box("SkidFront", -0.130, -0.215, 0.120, 0.055, 0.130': 'box("SkidFront", -0.130, -0.215, 0.120, 0.055, 0.118',
}
ENERGY = {"= 60": "= 80", "= 26": "= 34"}

s = io.open(P, encoding="utf-8").read()
hit = 0
for a, b in REPL.items():
    if a in s:
        s = s.replace(a, b)
        hit += 1
for a, b in ENERGY.items():
    s = s.replace("bpy.context.active_object.data.energy " + a, "bpy.context.active_object.data.energy " + b)
io.open(P, "w", encoding="utf-8", newline="\n").write(s)
print("replaced", hit, "/", len(REPL))
