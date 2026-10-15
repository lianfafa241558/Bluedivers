# -*- coding: utf-8 -*-
"""把「拦截火车任务起终点/轨道断节修复」的结论追加到今天的记忆文件（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车任务：起终点 + 轨道断节 修复
- 位置 Assets/Scripts/06Gameplay/Mission/Main/MissionInterceptTrain.cs（用户已改：基类改 MissionBase、预制体改直接引用 railPrefab + Foldout/InspectorName，我保留这些写法）
- 病灶1：地图边缘不能用 TerrainData 边界。可玩区 = 空气墙圆，半径 = min(size.x,size.z)/2 - Constants.TaskBorder(默认128)，依据 MapRoot.GenerateTerrainRect（rect.size.x/2 = 半边长 - TaskBorder，墙就建在这个圆上）；照地形边界内缩 20m 取点 ⇒ 落在墙外死区/水里
- 病灶2：tarsonisrail.prefab 的长轴沿 local X（子 Cube localScale(24,6,6) + 旋转 -90°X），LookRotation 对齐的是 +Z ⇒ 每节被转了 90°，看着断断续续
- 修法：① 圆周取点，约束 = 夹角≥120° + 弦长≥半径×0.5 + 离 AreaCircle 圆心≥Radius/2，并在候选里挑最平的一处；② 每步 24m 在朝终点 ±75° 扇形采样，score=max(中点/落点坡度)+偏转轻罚，踩进 AreaCircle +1000；候选点 Clamp 回「地形内 + 圆内」；③ 铺轨 = 按实测轨道长度把每段切成整数节、节内缩放长轴，底面用 bounds.min.y 对齐地面再下沉 0.2 ⇒ 首尾相接
- 尺寸不写死：MeasureRail 在原点用单位旋转实例化探针量 bounds（长轴 = 水平更大那根、长轴在 X 时 YawOffset=-90、PivotToBottom=center.y-extents.y），量完立即 SetActive(false)+Destroy
- 坑：Terrain 上没有 GetSteepness，要用 TerrainData.GetSteepness(u,v)（归一化坐标、返回弧度）
- 离线编译 06_Gameplay 0 错误
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
