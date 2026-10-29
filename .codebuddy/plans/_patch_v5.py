"""v4 → v5：加"接收机"横向带、给白甲加深色分隔条、修顶轨/转子/滑撬。一次性补丁。"""
import io

P = r"e:\Bluedivers\.codebuddy\plans\gear_build.py"
s = io.open(P, encoding="utf-8").read()

# 1) 左侧大框补左右两条深色包边（原来只有上下）
s = s.replace(
    'box("FrameTrimTop", -0.280, 0.192, 0.420, 0.014, 0.114, mat=M_NAVY)',
    'box("FrameTrimTop", -0.280, 0.192, 0.420, 0.014, 0.114, mat=M_NAVY)\n'
    'box("FrameTrimL", -0.494, -0.050, 0.014, 0.340, 0.114, mat=M_NAVY)\n'
    'box("FrameTrimR", -0.062, -0.050, 0.014, 0.340, 0.114, mat=M_NAVY)')

# 2) 接收机横带：把中间那条水平线做实（参考图里这条带子贯穿全长）
s = s.replace(
    '# =====================================================================\n# 5 主横杆两根',
    'box("ReceiverBand", -0.120, 0.070, 0.560, 0.070, 0.140, mat=M_NAVY)\n'
    'box("ReceiverBandTop", -0.120, 0.112, 0.540, 0.016, 0.146, mat=M_STEEL)\n'
    'box("ReceiverBandEnd", -0.400, 0.070, 0.030, 0.070, 0.140, mat=M_WHITE)\n\n'
    '# =====================================================================\n# 5 主横杆两根')

# 3) 转子锥度收小，别做成尖锥
s = s.replace('cyl("CoreDiscB", 0.209, 0.005, 0.098', 'cyl("CoreDiscB", 0.209, 0.005, 0.104')
s = s.replace('cyl("CoreDiscC", 0.245, 0.005, 0.083', 'cyl("CoreDiscC", 0.245, 0.005, 0.096')
s = s.replace('cyl("CoreDiscD", 0.277, 0.005, 0.066', 'cyl("CoreDiscD", 0.277, 0.005, 0.088')

# 4) 顶轨：卡箍改金属色、端板缩小、加一根立柱撑到机身上
s = s.replace('cyl("TopRailCollar", 0.120, 0.204, 0.058, 0.080, axis="X", verts=10, mat=M_WHITE, spin=18)',
              'cyl("TopRailCollar", 0.120, 0.204, 0.052, 0.080, axis="X", verts=10, mat=M_STEEL, spin=18)')
s = s.replace('box("TopRailEnd", 0.300, 0.204, 0.020, 0.080, 0.070, mat=M_WHITE)',
              'box("TopRailEnd", 0.300, 0.204, 0.018, 0.060, 0.060, mat=M_NAVY)\n'
              'box("TopRailPost", 0.170, 0.150, 0.030, 0.110, 0.070, mat=M_WHITE)')

# 5) 滑撬压薄，V 形支腿缩小
s = s.replace('box("Skid", 0.060, -0.258, 0.530, 0.048, 0.120', 'box("Skid", 0.060, -0.266, 0.530, 0.038, 0.120')
s = s.replace('box("VBraceA", 0.330, -0.230, 0.140, 0.028, 0.070, rot_y=-42',
              'box("VBraceA", 0.330, -0.222, 0.120, 0.024, 0.065, rot_y=-42')
s = s.replace('box("VBraceB", 0.400, -0.230, 0.130, 0.028, 0.070, rot_y=40',
              'box("VBraceB", 0.395, -0.222, 0.110, 0.024, 0.065, rot_y=40')

io.open(P, "w", encoding="utf-8", newline="\n").write(s)
print("patched")
