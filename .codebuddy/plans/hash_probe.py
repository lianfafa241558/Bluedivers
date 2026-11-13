# -*- coding: utf-8 -*-
"""反推 Unity Animator.StringToHash 算法：拿 mcp execute_code 在 Unity 里算出的真值对照。
已知真值（2026-11-13，Bluedivers 实例实算）：
    Reset=-1856204727 Chatget=1314759673 ChatgetSpeed=-561804707 MoveSpeed=526065662
    Attack=1080829965 SpecialAttack1=-938659887 SpecialAttack2=1359249003
    SpecialAttack3=637751037 Alerted=-1854995305 OnDamaged=-1403378696
    OnDeath=-42616333 IsDeath=569220492 OnReload=1522553424 IsActive=1929417870 Entry=-354373004
"""
KNOWN = {
    "Reset": -1856204727, "Chatget": 1314759673, "ChatgetSpeed": -561804707,
    "MoveSpeed": 526065662, "Attack": 1080829965, "SpecialAttack1": -938659887,
    "SpecialAttack2": 1359249003, "SpecialAttack3": 637751037, "Alerted": -1854995305,
    "OnDamaged": -1403378696, "OnDeath": -42616333, "IsDeath": 569220492,
    "OnReload": 1522553424, "IsActive": 1929417870, "Entry": -354373004,
}


def s32(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x >= (1 << 31) else x


def make(mul, add, op, init, right_shift):
    def fn(s):
        h = init
        for ch in s:
            c = ord(ch)
            if op == '+':
                h = (h * mul + add * c) & 0xFFFFFFFF
            else:
                h = ((h * mul) ^ c) & 0xFFFFFFFF
            if right_shift:
                h = (h ^ (h >> 27)) & 0xFFFFFFFF
        return s32(h)
    return fn


hits = []
for mul in list(range(2, 132)) + [65599, 16777619]:
    for init in (0, 5381, 2166136261, 5381 * 33):
        for op in ('+', '^'):
            fn = make(mul, 1, op, init, False)
            if all(fn(k) == v for k, v in KNOWN.items()):
                hits.append((mul, init, op))

print("命中参数：", hits if hits else "无")

# 参考：Unity 官方 C# 实现写法（hash = c + (hash<<5) + (hash>>27)）的实际输出
def unity_cs(s):
    h = 0
    for c in s:
        h = s32(ord(c) + (h << 5) + (h >> 27))
    return h


print("unity_cs(Attack) =", unity_cs("Attack"), "真值 1080829965")
