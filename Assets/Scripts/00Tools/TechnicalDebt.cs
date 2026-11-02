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
         * [已解决 2026-11-02] UnitQueryGrid / BattleManager 的空间查询分配
         * 位置：Assets/Scripts/05UnitCore/Query/UnitQueryGrid.cs、UnitQuery.cs、09Manager/Battle/BattleManager.cs
         * 做法：新增零分配重载 QueryUnits / FindUnits(range|targetCfg, cfg, filter, List<IActor> result)，
         *       内部用实例缓冲 _overlapsUnits / _unitsInRange；老的三参重载保留（语义不变），
         *       高频调用点（地雷、AI 索敌、爆炸、范围动能、圣水、弹射、制导炮击、武器锁定）已切到新重载。
         * ⚠ 新重载的硬约束：customFilter 里不能再发起范围查询（会覆盖共享草稿缓冲）。
         * 待办：剩余低频调用点（小地图 2s、任务 tick、技能释放）按需再迁。
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
         * 现状：势力占领度是设计期资产 Resources/GameData/OccupierData（OccupierData_SO，逐任务点一份），
         *       由 OccupierState 只读转发；但**只有读，没有增长逻辑**
         *       （打赢/打输、完成什么任务各涨多少，尚未设计），SelectMapWnd 显示的永远是配置值。
         * 方案：待设计（数值规则 + 由谁结算 + 归属哪一端权威）；真做增长时另立运行期状态盖在 SO 上，别改资产。
         */

        #endregion

    }
}
