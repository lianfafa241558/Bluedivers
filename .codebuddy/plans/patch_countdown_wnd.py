# -*- coding: utf-8 -*-
"""CountDownWnd 归零不关闭 repair + activeBelow 随 StartDown 传入。

理由：改动横跨 4 个文件、其中 CountDownWnd.cs 是 BOM+CRLF、WndManager.cs 是 CRLF，
多行插入需保持原行尾 ⇒ 走字节级替换并对每个锚点断言"命中恰 1 次"，命中数不符即中止不落盘。
"""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]

PATCHES = [
    # ---------------- 1. CountDownWnd.cs ----------------
    ("Assets/Scripts/10UI/Wnd/CountDownWnd.cs", [
        (
            """/// 由调用方通过 <see cref="StartDown(Func{int})"/> 传入值提供者，每帧读取返回值驱动显示，""",
            """/// 由调用方通过 <see cref="StartDown(Func{int}, CountDownTypeEnum, int)"/> 传入值提供者与启动阈值，每帧读取返回值驱动显示，""",
        ),
        (
            """    /// 以外部驱动模式启动，每帧读取 provider 的返回值驱动显示。
    /// 兼容旧用法：传 ()=>TaskManager.Instance.nowTask.Countdown。""",
            """    /// 以外部驱动模式启动，每帧读取 provider 的返回值驱动显示。
    /// <paramref name="activeBelow"/> 为启动阈值(低于它才展开动画并播放音效)，≤0 表示沿用 Inspector 配置。
    /// 兼容旧用法：传 ()=>TaskManager.Instance.nowTask.Countdown。""",
        ),
        (
            """    public void StartDown(Func<int> provider,CountDownTypeEnum type)
    {
        _valueProvider = provider;""",
            """    public void StartDown(Func<int> provider, CountDownTypeEnum type, int activeBelow = -1)
    {
        // 传入阈值(>0)优先于 Inspector 配置；-1/0 表示沿用序列化值
        if (activeBelow > 0) this.activeBelow = activeBelow;
        // 以阈值同步"首次显示值"，避免首帧把残留的 countDown 误判成数值跳变
        countDown = this.activeBelow;

        _valueProvider = provider;""",
        ),
        (
            """        int nowcd = _valueProvider();
        // 只在数值发生变化时刷新,避免逐帧冗余操作
        if (nowcd != countDown)
        {
            if (countDown == 0)
            {
                SetWndState(false);
            }
            else {
                countDown = nowcd;
                ApplyValue(nowcd);
            }

        }""",
            """        int nowcd = _valueProvider();
        // 只在数值发生变化时刷新,避免逐帧冗余操作
        if (nowcd == countDown) return;

        countDown = nowcd;

        // 归零 ⇒ 停止驱动并关闭窗口
        // ⚠ 原判据是 `countDown == 0`：countDown 上一步刚被赋成 provider 的读数，
        //   归零那一帧只会走 else 刷出 00:00，之后 nowcd==countDown 恒真 ⇒ 窗口永不消失。
        if (nowcd <= 0)
        {
            Stop();
            SetWndState(false);
            return;
        }

        ApplyValue(nowcd);""",
        ),
    ]),
    # ---------------- 2. WndHub.cs（UI 层登记） ----------------
    ("Assets/Scripts/10UI/WndHub.cs", [
        (
            """        WindowRegistry.CreatCountDown = (provider, type) =>
        {
            var cd = CountDown;
            if (cd != null) cd.StartDown(provider, type);
        };""",
            """        WindowRegistry.CreatCountDown = (provider, type, activeBelow) =>
        {
            var cd = CountDown;
            if (cd != null) cd.StartDown(provider, type, activeBelow);
        };""",
        ),
    ]),
    # ---------------- 3. WindowRegistry.cs（契约层插槽） ----------------
    ("Assets/Scripts/00GameContract/WindowRegistry.cs", [
        (
            """        /// <summary>(倒计时读数提供者, 样式) → 启动倒计时窗。</summary>
        public static Action<Func<int>, CountDownTypeEnum> CreatCountDown { get; internal set; }""",
            """        /// <summary>(倒计时读数提供者, 样式, 启动阈值) → 启动倒计时窗；阈值 ≤0 表示沿用窗口 Inspector 配置。</summary>
        public static Action<Func<int>, CountDownTypeEnum, int> CreatCountDown { get; internal set; }""",
        ),
        (
            """        /// <summary>启动倒计时窗（未登记时静默跳过）。</summary>
        public static void CountDown(Func<int> provider, CountDownTypeEnum type)
        {
            EnsureScanned();
            if (CreatCountDown != null) CreatCountDown(provider, type);
        }""",
            """        /// <summary>启动倒计时窗（未登记时静默跳过）。<paramref name="activeBelow"/> ≤0 表示沿用窗口 Inspector 配置。</summary>
        public static void CountDown(Func<int> provider, CountDownTypeEnum type, int activeBelow = -1)
        {
            EnsureScanned();
            if (CreatCountDown != null) CreatCountDown(provider, type, activeBelow);
        }""",
        ),
    ]),
    # ---------------- 4. WndManager.cs（管理器门面） ----------------
    ("Assets/Scripts/09Manager/Global/WndManager.cs", [
        (
            """    /// <summary>启动倒计时窗（转调契约注册表；UI 未就绪时静默跳过）。</summary>
    public void CreatCountDown(Func<int> func, CountDownTypeEnum type)
    {
        WindowRegistry.CountDown(func, type);
    }""",
            """    /// <summary>启动倒计时窗（转调契约注册表；UI 未就绪时静默跳过）。
    /// <paramref name="activeBelow"/> ≤0 表示沿用窗口 Inspector 上配置的启动阈值。</summary>
    public void CreatCountDown(Func<int> func, CountDownTypeEnum type, int activeBelow = -1)
    {
        WindowRegistry.CountDown(func, type, activeBelow);
    }""",
        ),
    ]),
]


def patch(rel, pairs):
    path = ROOT / rel
    data = path.read_bytes()
    for idx, (old, new) in enumerate(pairs):
        done = False
        for nl in (b"\r\n", b"\n"):
            o = old.replace("\n", nl.decode()).encode("utf-8")
            n = new.replace("\n", nl.decode()).encode("utf-8")
            cnt = data.count(o)
            if cnt:
                assert cnt == 1, "%s 锚点%d 命中 %d 次（应恰 1 次）" % (rel, idx, cnt)
                data = data.replace(o, n)
                done = True
                break
        if not done:
            # 幂等：重跑时该锚点可能已是 new 版本
            for nl in (b"\r\n", b"\n"):
                n = new.replace("\n", nl.decode()).encode("utf-8")
                if data.count(n) == 1:
                    done = True
                    print("  skip(已应用)", rel, idx)
                    break
        assert done, "%s 锚点%d 未命中：\n%s" % (rel, idx, old[:100])
    path.write_bytes(data)
    print("patched", rel)


if __name__ == "__main__":
    for rel, pairs in PATCHES:
        patch(rel, pairs)
    print("ALL OK")
