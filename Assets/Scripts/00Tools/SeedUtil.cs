namespace FPSGame.Utils
{
    /// <summary>
    /// 派生随机流的**用途标识**。
    ///
    /// <para>▍为什么需要它：项目里原本只有"一条全局静态流"（<see cref="RandomUtils"/>）与"每局一条战斗流"
    /// （<c>BattleManager.BattleRandom</c>）。凡是**必须两端一致**的随机（地形、装饰物、谜题序列…）
    /// 只要共用同一条流，就会被"音效/弹孔/武器散布"这类无关系统推进游标 ⇒ 结果漂移。
    /// 改为"按用途派生独立流"后，各用途互不干扰。</para>
    ///
    /// <para>▍⚠ 枚举值一旦发布**不可修改**（改值会改变所有人的随机结果，导致联机两端对不上）。</para>
    /// </summary>
    public enum SeedStream
    {
        /// <summary>全局静态流本体（保留占位，不改动 <see cref="RandomUtils"/> 的行为）。</summary>
        RandomUtils = 0,
        /// <summary>每局战斗流（与 <c>TaskState.Seed</c> 同源，仅供检索用）。</summary>
        Battle = 1,
        /// <summary>地形高度图（噪声偏移）。</summary>
        Terrain = 2,
        /// <summary>地形装饰（覆盖石 / 树 / 石块）。</summary>
        TerrainDecor = 3,
        /// <summary>谜题序列（实际用 <c>Puzzle * 1000 + puzzleId</c> 细分）。</summary>
        Puzzle = 4,
        /// <summary>任务/兴趣点实体的**朝向角**（位置当细分键）—— 各端各摇一次会让同一实体朝向不同。</summary>
        MissionEntity = 5,
        /// <summary>战斗波次（实际用 <c>Wave * 1000 + 本局第几波</c> 细分，与 <see cref="Puzzle"/> 同一约定）。</summary>
        Wave = 9,
        /// <summary>波次的**单位构成**抽取（同样用 <c>WaveUnits * 1000 + 波序</c> 细分）。</summary>
        WaveUnits = 10,
        /// <summary>单个单位的**落点抖动/朝向**（同样用 <c>UnitPlacement * 1000 + 波序</c> 细分）。</summary>
        UnitPlacement = 11,
        /// <summary>敌人的 **AI / 技能随机**（用 <c>Ai</c> 派生一次、再按实体的 <c>NetId</c> 细分 ⇒ 每个单位一条独立流）。</summary>
        Ai = 12,
        /// <summary>**武器/战备开火的随机**（弹道散布、炮击落点）—— 用 <c>Weapon * 1000 + 战备ID</c> 细分。
        /// <para>▍为什么必须独立：轨道轰炸这类战备**各端各自执行一次**，散布/落点随机若不播种，两端就是两片不同的火海
        /// （2026-10-07 用户实测"武器发射相关的随机没有使用种子，导致轨道轰炸的战备没有成功同步"）。</para></summary>
        Weapon = 13,
    }

    /// <summary>
    /// 派生随机流的**唯一入口**：把一个"本局权威种子"派生成若干条互不干扰的独立随机线。
    ///
    /// <para>▍放这里的原因（已核实程序集方向）：本文件在 <c>00Tools/</c>（asmdef = <c>00_Utils</c>），
    /// 而 <c>00_Utils</c> 被 <c>08_Map</c> / <c>09_Managers</c> / <c>06_Gameplay</c> / <c>10_Effect</c> /
    /// <c>04_Data</c> 全部引用 ⇒ 一处定义、全项目可用。
    /// ⚠ 但 <c>02_Net</c> 的 <c>references</c> 是空的（看不见本程序集）⇒ 网络层不要直接用它。</para>
    ///
    /// <para>▍纯函数要求：<see cref="Derive"/> 不读时间、不读任何可变静态状态 ⇒ 任何端、任何时刻、同参同值。</para>
    /// </summary>
    public static class SeedUtil
    {
        /// <summary>从权威种子派生一条独立流的种子（同 seed + 同 purpose ⇒ 恒等）。</summary>
        public static int Derive(int seed, int purpose) => unchecked(seed * 31 + purpose);

        /// <summary>同上，purpose 用枚举表达（可读性更好）。</summary>
        public static int Derive(int seed, SeedStream stream) => Derive(seed, (int)stream);

        /// <summary>新建一条派生随机线。<paramref name="seed"/> = 0（未指定）时返回 null，由调用方自行回落到本地随机。</summary>
        public static System.Random NewStream(int seed, int purpose)
            => seed == 0 ? null : new System.Random(Derive(seed, purpose));

        /// <summary>新建一条派生随机线（枚举版）。</summary>
        public static System.Random NewStream(int seed, SeedStream stream) => NewStream(seed, (int)stream);
    }
}
