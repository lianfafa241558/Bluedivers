# -*- coding: utf-8 -*-
"""给 WeaponPlayerController 接上"子弹配置序号"属性：
1) 加 using System.Collections.Generic + _damageSetters 字段
2) InitAttribute 末尾注入 DamageIndex 并订阅
3) 整段重写 UnInitAttribute（改为按 _damageSetters 批量退订）
4) 追加 OnDamageIndexChanged / RebaseDamageData

⚠ 保留 BOM 与 LF；先整读再写。
"""
import io

PATH = r'Assets/Scripts/06Gameplay/Weapon/WeaponPlayerController.cs'
lines = io.open(PATH, encoding='utf-8-sig', newline='').read().split('\n')

# ---- 1) using ----
u = lines.index('using System;')
if 'using System.Collections.Generic;' not in lines:
    lines.insert(u + 1, 'using System.Collections.Generic;')

# ---- 2) 字段 + InitAttribute 末尾注入 ----
anchor = '            Set(Attr.ExplosionTypeHacker, (d, v) => d.SetExplosionTypeHacker(v));'
i = lines.index(anchor)
lines[i + 1:i + 1] = [
    '',
    '            // 子弹配置序号：基础值取当前 UseDamageIndex（信号枪等运行时切档的值不能在这里被重置），',
    '            // 只在属性真变时才回写字段。tag = IsHide ⇒ 参数面板永远不显示这一行。',
    '            if (!_cfg[Attr.DamageIndex]) _cfg.Add(Attr.DamageIndex, UseDamageIndex);',
    '            _cfg[Attr.DamageIndex].OnFinalValueChange += OnDamageIndexChanged;',
]

init_head = '        protected override void InitAttribute()'
j = lines.index(init_head)
lines[j:j] = [
    '        /// <summary>伤害类属性的"属性 → 当前 DamageData"订阅表（UnInitAttribute 整批退订用）。',
    '        /// <para>⚠ 不能再按 <c>Damages[0].SetXxx</c> 去退订：切过子弹配置后绑的已经不是 0 号那条了。</para></summary>',
    '        private readonly Dictionary<Attr, Action<PEInt>> _damageSetters = new();',
    '',
]

# ---- 3) 整段重写 UnInitAttribute ----
start = lines.index('        protected override void UnInitAttribute()')
end = start + 1
while lines[end] != '        }':
    end += 1

new_block = [
    '        protected override void UnInitAttribute()',
    '        {',
    '            var _cfg = cfg;',
    '',
    '            foreach (var item in _damageSetters)',
    '            {',
    '                var attr = _cfg[item.Key];',
    '                if (attr) attr.OnFinalValueChange -= item.Value;',
    '            }',
    '            _damageSetters.Clear();',
    '',
    '            if (_cfg[Attr.DamageIndex]) _cfg[Attr.DamageIndex].OnFinalValueChange -= OnDamageIndexChanged;',
    '        }',
    '',
    '        /// <summary>子弹配置序号（<see cref="Attr.DamageIndex"/>）落地：只换 <c>Damages</c> 下标，越界夹回合法范围。',
    '        /// <para>⚠ 信号枪的 0/1 档位由 <c>PlayerWeaponsManager</c> 运行时直接赋 <c>UseDamageIndex</c>，',
    '        /// 这类武器别再配"子弹配置序号"模组，否则两边互相覆盖。</para></summary>',
    '        private void OnDamageIndexChanged(PEInt value)',
    '        {',
    '            if (Damages == null || Damages.Count == 0) return;',
    '',
    '            var index = Mathf.Clamp(value.RawInt, 0, Damages.Count - 1);',
    '            if (index == UseDamageIndex) return;',
    '',
    '            UseDamageIndex = index;',
    '            // 伤害类属性的基线跟着换到新配置，否则面板显示与改装加成还停在旧那条配置上',
    '            RebaseDamageData();',
    '        }',
    '',
    '        /// <summary>把伤害类属性的基线换成"当前 DamageData"的原始值（Factor/Extra/ΣBase 保留）；',
    '        /// <see cref="GameAttribute.Rebase"/> 内部 Recalculate 会把结果写回新配置。</summary>',
    '        private void RebaseDamageData()',
    '        {',
    '            var data = CurrentDamgeData;',
    '            if (data == null) return;',
    '',
    '            foreach (var item in _damageSetters)',
    '            {',
    '                var attr = cfg[item.Key];',
    '                if (!attr) continue;',
    '                attr.Rebase(data.GetAttr(item.Key));',
    '            }',
    '        }',
]
lines[start:end + 1] = new_block

io.open(PATH, 'w', encoding='utf-8-sig', newline='').write('\n'.join(lines))
print('ok, lines =', len(lines))
