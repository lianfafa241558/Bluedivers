---
name: handequip-inline-gravity
overview: 将 HandEquip 落地模拟重力从"复用 CCGravity 组件"改为"完全内联到 HandEquip 内部"，移除对 05_EffectComp（CCGravity）的跨层反向引用，使 02Game 只依赖底层模块，彻底解决 02→05 依赖方向违规。
---

