

using UnityEngine;
namespace Core
{
    /// <summary>形状类型</summary>
    public enum ShapeType
    {
        /// <summary>圆形</summary>
        Circle,
        /// <summary>椭圆</summary>
        Ellipse,
        /// <summary>矩形</summary>
        Rectangle,
        /// <summary>棱形</summary>
        Prismatic
    }


    public enum WindowStateEnum
    {
        [InspectorName("任意界面")] All,
        [InspectorName("游戏界面")] Game,
        [InspectorName("UI界面")] UI,
        [InspectorName("战备界面")] Airdrop,
        [InspectorName("自由视角")] FreeCamera,
    }

    public enum GameStateEnum
    {
        /// <summary>封面</summary>
        [InspectorName("封面")] Front = 1 << 0,
        /// <summary>舰桥</summary>
        [InspectorName("舰桥")] Bridge = 1 << 1,
        /// <summary>准备</summary>
        [InspectorName("准备")] Ready = 1 << 2,
        /// <summary>过场</summary>
        [InspectorName("过场")] Transition = 1 << 3,
        /// <summary>加载</summary>
        [InspectorName("加载")] Load = 1 << 4,
        /// <summary>游戏</summary>
        [InspectorName("游戏")] Game = 1 << 5,
        /// <summary>游戏结算</summary>
        [InspectorName("游戏结算")] GameEnd = 1 << 6,
        /// <summary>配置战备</summary>
        [InspectorName("配置战备")] Armament = 1 << 7,
    }

    public enum UnitTypeEnum
    {

        [InspectorName("玩家")] Player = 1 << 0,
        [InspectorName("盟友")] Friend = 1 << 1,
        [InspectorName("敌人")] Enemy = 1 << 2,
        [InspectorName("特殊单位")] SpecUnit = 1 << 3,
        [InspectorName("其他")] Other = 1 << 4,

        [InspectorName("全部")] All = ~0,
        [InspectorName("无")] None = 0,
    }

    public enum EnemyType
    {
        /// <summary>凯撒</summary>
        [InspectorName("凯撒")]
        Kaiser,

        /// <summary>十字神明</summary>
        [InspectorName("十字神明")]
        Decagrammaton,

        /// <summary>色彩</summary>
        [InspectorName("色彩")]
        Colour,
        /*
        /// <summary>贝阿特里斯</summary>
        [InspectorName("贝阿特里斯")]
        Beatrice = 1 << 3,
        */
        //All = ~0,
        //None = 0,
    }

    public enum EnemyVarietyType
    {
        /// <summary>凯撒</summary>
        [InspectorName("凯撒/基础")] KaiserBase,
        /// <summary>凯撒PMC</summary>
        [InspectorName("凯撒/PMC")] KaiserPMC,
        /// <summary>凯撒集团卫队</summary>
        [InspectorName("凯撒/集团卫队")] KaiserMengsk,
        /// <summary>黑市</summary>
        [InspectorName("凯撒/黑市")] BlackMarket,
        /// <summary>凯撒/占位符</summary>
        [InspectorName("凯撒/占位符")] Placeholder1,

        /// <summary>十字神明</summary>
        [InspectorName("十字神明/基础")] Decagrammaton,
        /// <summary>无名众神</summary>
        [InspectorName("十字神明/无名众神")] UnNamedGuardian,
        /// <summary>爆笑星际</summary>
        [InspectorName("十字神明/爆笑星际")] StarCraft,
        /// <summary>十字神明/占位符</summary>
        [InspectorName("十字神明/占位符2")] Placeholder2,
        /// <summary>十字神明/占位符</summary>
        [InspectorName("十字神明/占位符3")] Placeholder3,

        /// <summary>色彩</summary>
        [InspectorName("色彩/基础")] Colour,
        /// <summary>色彩/贝阿特里斯</summary>
        [InspectorName("色彩/贝阿特里斯")] Beatrice,
        /// <summary>色彩/占位符</summary>
        [InspectorName("色彩/占位符4")] Placeholder4,
        /// <summary>色彩/占位符</summary>
        [InspectorName("色彩/占位符5")] Placeholder5,
        /// <summary>色彩/占位符</summary>
        [InspectorName("色彩/占位符6")] Placeholder6,


    }

    public enum DamageTypeEnum
    {
        /// <summary>动能</summary>
        [InspectorName("动能")]
        Gun,
        /// <summary>爆炸</summary>
        [InspectorName("爆炸")]
        Explosion,
        /// <summary>护甲破坏</summary>
        [InspectorName("护甲破坏")]
        Destruction,
        /// <summary>真实-溶解</summary>
        [InspectorName("真实-溶解")]
        Real,
        /// <summary>毒</summary>
        [InspectorName("毒")]
        Toxicity,
        /// <summary>燃烧</summary>
        [InspectorName("燃烧")]
        Burn,
        /// <summary>冰冻</summary>
        [InspectorName("冰冻")]
        Freeze,
        /// <summary>电击</summary>
        [InspectorName("电击")]
        Electric,
        /// <summary>眩晕</summary>
        [InspectorName("眩晕")]
        Vertigo,
        /// <summary>恐惧</summary>
        [InspectorName("恐惧")]
        Terror,
        /// <summary>辐射</summary>
        [InspectorName("辐射")]
        Radiation,
        /// <summary>骇入</summary>
        [InspectorName("骇入")]
        Hacker,
        /// <summary>占位符</summary>
        [InspectorName("占位符")]
        placeholder,
    }

    public enum ActorState
    {
        None,
        Normal,
        Dead,
        Hide,
    }
    [System.Flags]
    public enum ActorFlag
    {
        /// <summary>无敌</summary>
        [InspectorName("无敌")]
        Invincible = 1 << 0,
        /// <summary>建筑</summary>
        [InspectorName("建筑")]
        Building = 1 << 1,
        /// <summary>首领</summary>
        [InspectorName("首领")]
        Boss = 1 << 2,
        /// <summary>自动注册(只对other有效)</summary>
        [InspectorName("自动注册(只对other有效,像特殊单位一样触发事件)")]
        AutoRegister = 1 << 3,
        /// <summary>允许浮空</summary>
        [InspectorName("允许浮空(正常会强制刷在地上)")]
        AllowFloating = 1 << 4,
        [InspectorName("小地图忽略")]
        MiniMapIgnore = 1 << 5,
        /// <summary>建筑</summary>
        [InspectorName("巢穴")]
        Nest = 1 << 6,
        /// <summary>不重要的</summary>
        [InspectorName("不重要的(只对非other有效,阻止它在小地图注册)")]
        Unimportant = 1 << 7,
    }


}