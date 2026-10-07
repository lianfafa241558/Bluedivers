namespace FPSGame.Utils
{
    /// <summary>
    /// 技术债与待办事项清单（文档载体，不参与运行逻辑）。
    /// 本类仅用于集中记录项目当前已知的技术债、架构问题与规划中的改进，
    /// 方便日后逐项核对、修订与实现。新增技术债请按分类追加条目，并标注记录日期。
    /// 历史条目在解决后可移入"已解决"区段。
    /// </summary>
    public static class TechnicalDebt
    {


        #region 性能与分配优化

        /*
         * [P1] UnitQueryGrid / BattleManager 的空间查询分配
         * 位置：Assets/Scripts/02Game/Game/UnitQueryGrid.cs、Assets/Scripts/01Manager/Battle/BattleManager.cs
         * 现状：QueryUnits 内部节点列表已用对象池复用；但 FindUnits / GetOverlapsUnits 仍每次
         *       new List<I_Actor>(...) / new HashSet，高频调用（武器锁敌、AI、爆炸、小地图）会造成 GC 压力。
         * 方案：为高频调用方引入可复用的缓冲 List（传入缓存实例，或按调用线程复用池化列表），
         *       需注意返回值被调用方修改（OrderBy/Remove/Count）时的语义。
         */

        /*
         * [P2] 全局搜索"每帧 new"的分配点
         * 编码规范要求 Update/FixedUpdate 中避免 new（含闭包、临时集合、字符串拼接）。
         * 重点排查：PlayerController / WeaponPlayerController / BattleManager 的 Update 相关逻辑。
         */

        #endregion

        #region 命名空间 / 程序集 / 命名规范

        /*
         * [P3] 协程命名 / async 混用
         * 现状：项目主流用协程，几乎不用 async/await；协程名以动词开头。
         * 方案：新逻辑优先协程，避免引入 async void（事件处理器除外）。
         */

        #endregion

        #region 网络 / 单例 / RPC

        /*
         * [P3] 轻服务器策略待落地
         * 规划：服务器仅提供房间列表，其余数据全部存本地；暂不做多人在线进度同步。
         * 状态：KCPNet 未完成，暂不迁移；单机 demo 阶段不阻塞。
         */

        #endregion

        #region 游戏系统半成品 / 待完善

        /*
         * [P2] 解放度系统半成品
         * 现状：occupierDic（DisplayDic<string, List<ArchOccupierData>>）无势力模板初始化，
         *       每个地图的势力列表为空，SelectMapWnd 只能显示进度条但无数据来源与增长逻辑。
         * 方案：待设计（是否按地图配置势力、解放度数值规则、胜利/失败是否增长等），当前不阻塞。
         */

        #endregion

    }
}
