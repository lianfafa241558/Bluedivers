

using Unity.BaseTool;
namespace Core
{
    public enum WindowStateEnum
    {
        [CustomLabel("任意界面")] All,
        [CustomLabel("游戏界面")] Game,
        [CustomLabel("UI界面")] UI,
        [CustomLabel("战备界面")] Airdrop,
        [CustomLabel("自由视角")] FreeCamera,
    }

    public enum GameStateEnum
    {
        /// <summary>封面</summary>
        [CustomLabel("封面")] Front = 1 << 0,
        /// <summary>舰桥</summary>
        [CustomLabel("舰桥")] Bridge = 1 << 1,
        /// <summary>准备</summary>
        [CustomLabel("准备")] Ready = 1 << 2,
        /// <summary>过场</summary>
        [CustomLabel("过场")] Transition = 1 << 3,
        /// <summary>加载</summary>
        [CustomLabel("加载")] Load = 1 << 4,
        /// <summary>游戏</summary>
        [CustomLabel("游戏")] Game = 1 << 5,
        /// <summary>游戏结算</summary>
        [CustomLabel("游戏结算")] GameEnd = 1 << 6,
        /// <summary>配置战备</summary>
        [CustomLabel("配置战备")] Armament = 1 << 7,
    }

    public enum UnitTypeEnum
    {

        [CustomLabel("玩家")] Player = 1 << 0,
        [CustomLabel("盟友")] Friend = 1 << 1,
        [CustomLabel("敌人")] Enemy = 1 << 2,
        [CustomLabel("特殊单位")] SpecUnit = 1 << 3,
        [CustomLabel("其他")] Other = 1 << 4,

        [CustomLabel("全部")] All = ~0,
        [CustomLabel("无")] None = 0,
    }

    public enum EnemyType
    {
        /// <summary>凯撒</summary>
        [CustomLabel("凯撒")]
        Kaiser,

        /// <summary>十字神明</summary>
        [CustomLabel("十字神明")]
        Decagrammaton,

        /// <summary>色彩</summary>
        [CustomLabel("色彩")]
        Colour,
        /*
        /// <summary>贝阿特里切</summary>
        [CustomLabel("贝阿特里切")]
        Beatrice = 1 << 3,
        */
        //All = ~0,
        //None = 0,
    }

    public enum EnemyVarietyType
    {
        /// <summary>凯撒</summary>
        [CustomLabel("凯撒/基础")] KaiserBase,
        /// <summary>凯撒PMC</summary>
        [CustomLabel("凯撒/PMC")] KaiserPMC,
        /// <summary>凯撒集团卫队</summary>
        [CustomLabel("凯撒/集团卫队")] KaiserMengsk,
        /// <summary>黑市</summary>
        [CustomLabel("凯撒/黑市")] BlackMarket,
        /// <summary>占位符1</summary>
        [CustomLabel("凯撒/占位符1")] Placeholder1,

        /// <summary>十字神明</summary>
        [CustomLabel("十字神明/基础")] Decagrammaton,
        /// <summary>无名众神</summary>
        [CustomLabel("十字神明/无名众神")] UnNamedGuardian,
        /// <summary>爆笑星际</summary>
        [CustomLabel("十字神明/爆笑星际")] StarCraft,
        /// <summary>占位符2</summary>
        [CustomLabel("十字神明/占位符2")] Placeholder2,
        /// <summary>占位符3</summary>
        [CustomLabel("十字神明/占位符3")] Placeholder3,

        /// <summary>色彩</summary>
        [CustomLabel("色彩/基础")] Colour,
        /// <summary>贝阿特里切</summary>
        [CustomLabel("色彩/贝阿特里切")] Beatrice,
        /// <summary>占位符4</summary>
        [CustomLabel("色彩/占位符4")] Placeholder4,
        /// <summary>占位符5</summary>
        [CustomLabel("色彩/占位符5")] Placeholder5,
        /// <summary>占位符6</summary>
        [CustomLabel("色彩/占位符6")] Placeholder6,


    }

    public enum DamageTypeEnum
    {
        /// <summary>动能</summary>
        [CustomLabel("动能")]
        Gun,
        /// <summary>爆炸</summary>
        [CustomLabel("爆炸")]
        Explosion,
        /// <summary>护甲破坏</summary>
        [CustomLabel("护甲破坏")]
        Destruction,
        /// <summary>真实-溶解</summary>
        [CustomLabel("真实-溶解")]
        Real,
        /// <summary>毒性</summary>
        [CustomLabel("毒性")]
        Toxicity,
        /// <summary>燃烧</summary>
        [CustomLabel("燃烧")]
        Burn,
        /// <summary>冰冻</summary>
        [CustomLabel("冰冻")]
        Freeze,
        /// <summary>电击</summary>
        [CustomLabel("电击")]
        Electric,
        /// <summary>眩晕</summary>
        [CustomLabel("眩晕")]
        Vertigo,
        /// <summary>恐惧</summary>
        [CustomLabel("恐惧")]
        Terror,
        /// <summary>辐射</summary>
        [CustomLabel("辐射")]
        Radiation,
        /// <summary>骇入</summary>
        [CustomLabel("骇入")]
        Hacker,
        /// <summary>地形破坏</summary>
        [CustomLabel("地形破坏")]
        Terrain,
        /// <summary>弱点</summary>
        [CustomLabel("弱点")]
        Weakness,
    }

    public enum ActorState
    {
        None,
        Normal,
        Dead,
    }
    [System.Flags]
    public enum ActorFlag
    {
        /// <summary>无敌</summary>
        [CustomLabel("无敌")]
        Invincible = 1 << 0,
        /// <summary>建筑</summary>
        [CustomLabel("建筑")]
        Building = 1 << 1,
        /// <summary>首领</summary>
        [CustomLabel("首领")]
        Boss = 1 << 2,
        /// <summary>自动注册(只对other有效)</summary>
        [CustomLabel("自动注册(只对other有效,像特殊单位一样触发事件)")]
        AutoRegister = 1 << 3,
        /// <summary>允许浮空</summary>
        [CustomLabel("允许浮空(正常会强制刷在地面)")]
        AllowFloating = 1 << 4,
        [CustomLabel("小地图忽略")]
        MiniMapIgnore = 1 << 5,
        /// <summary>建筑</summary>
        [CustomLabel("巢穴")]
        Nest = 1 << 6,
        /// <summary>不重要的</summary>
        //[CustomLabel("不重要的")]
        //Unimportant = 1 << 4,
    }
}