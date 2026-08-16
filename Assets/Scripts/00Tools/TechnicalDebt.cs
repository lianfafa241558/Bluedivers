namespace Unity.FPS
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

        #region 架构与解耦

        /*
         * [P1] I_Damagable / I_Entity 与 Unity 类型未解耦
         * 位置：00GameContract（I_Damagable / I_Entity 契约）
         * 现状：InflictDamage 等接口方法混用 GameObject / Vector3 / PEVector3 / Transform / TargetCfg，
         *       使逻辑层强依赖 Unity 类型，难以做纯逻辑单元测试与确定性重放。
         * 方案：定义纯逻辑 DTO（DamagePacket / SpawnCommand），接口只依赖 DTO 与定点数类型（PEInt/PEVector），
         *       由适配层在边界处转换 Unity 类型。
         */

        /*
         * [P2] God Class 审查
         * 位置：BattleManager（350+）、PlayerController（500+）、WeaponPlayerController
         * 现状：职责过多，单类体积大，违背单一职责原则。
         * 方案：按模块拆分（如 PlayerController 拆分移动/操作/交互/载具等部分），
         *       建议在新增功能迭代稳定后再做，避免重构与需求冲突。
         */

        #endregion

        #region 命名空间 / 程序集 / 命名规范

        /*
         * [P2] 命名空间与 asmdef rootNamespace 不统一
         * 现状：仅 00_Core（Core）、08_Map（Unity.FPS.Game）等少量程序集设置了 rootNamespace，
         *       其余 asmdef 的命名空间不统一，跨程序集引用时 using 混乱。
         * 方案：为每个 asmdef 补齐 rootNamespace，命名空间与程序集/目录对应。
         *       注意：改动命名空间会破坏大量 using，需一次性规划并全局替换。
         */

        /*
         * [P3] 枚举后缀不统一
         * 现状：GameStateEnum 带 Enum 后缀，ActorState / WeaponType 等不带。
         * 方案：新增枚举不加 Enum 后缀，存量按模块逐步统一（编码规范推荐）。
         */

        /*
         * [P3] 字段暴露方式不统一
         * 现状：public 字段与 [SerializeField] private 并存，public 较多。
         * 方案：新增字段优先 [SerializeField] private；存量 public 字段在重构时逐步迁移。
         */

        /*
         * [P3] 协程命名 / async 混用
         * 现状：项目主流用协程，几乎不用 async/await；协程名以动词开头。
         * 方案：新逻辑优先协程，避免引入 async void（事件处理器除外）。
         */

        #endregion

        #region 网络 / 单例 / RPC

        /*
         * [P1] SingletonNet 的 RPC 封装未跑通
         * 位置：Assets/Scripts/00Tools/SingletonNet.cs
         * 现状：期望用 nameof(action) 直接拿到目标方法名，但实际取到的是参数名而非方法名，
         *       导致 RPC 路由不生效。KCPNet 尚未完成，目前单机 demo 未受影响。
         * 方案：待 KCPNet 接入前重写该封装，明确 action 与方法名的映射。
         */

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

        /*
         * [P3] 矿物提交闭环的编辑器挂载待配置
         * 新增脚本已就绪，需在编辑器配置：
         *   - 玩家 prefab 挂 PlayerOOPartInventory
         *   - Kei prefab 挂 Furniture_KeiSubmit
         *   - OOPartBagWnd / KeiSubmitWnd 需指定 listRoot（LayoutGroup）+ itemPrefab（图标/名称/数量三子物体）
         */

        #endregion

    }
}
