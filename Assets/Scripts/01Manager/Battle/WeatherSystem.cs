using Unity.FPS.Game;
using UnityEngine;

/// <summary>天气类型</summary>
public enum WeatherType
{
    /// <summary>晴天（无天气效果）</summary>
    Sunny,
    /// <summary>雨天</summary>
    Rain,
    /// <summary>沙漠</summary>
    Desert,
    /// <summary>下雪</summary>
    Snow,
}

/// <summary>
/// 天气系统基类：由 BattleManager 开局随机抽取天气后通过 Create 工厂创建对应子类并生效。
/// 晴天直接使用基类（EffectPath 为空，无任何效果）；
/// 雨天/沙漠/下雪由子类配置 EffectPath 指定效果物体；
/// 启用 UseStormCycle 的子类（沙漠/下雪）在 Update 中按"平静时长→风暴时长"周期触发/停止风暴效果。
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    [Header("风暴周期（UseStormCycle=true 时生效）")]
    [InspectorName("平静时长(秒)")] [SerializeField] protected float _calmDuration = 45f;
    [InspectorName("风暴时长(秒)")] [SerializeField] protected float _stormDuration = 20f;

    [Header("跟随玩家")]
    [InspectorName("高度偏移")] [SerializeField] protected float _heightOffset = 0f;

    // 风暴计时与状态
    private float _stormTimer;
    private bool _stormActive;

    /// <summary>当前生效的天气</summary>
    public WeatherType Weather { get; private set; }

    /// <summary>本天气的效果物体实例（风暴周期子类初始为隐藏）</summary>
    protected GameObject EffectObject;

    /// <summary>效果物体资源路径（Resources 相对路径），由子类配置；为空表示无效果（晴天）</summary>
    protected virtual string EffectPath => null;

    /// <summary>是否启用周期风暴（沙漠沙尘暴/下雪暴雪）</summary>
    protected virtual bool UseStormCycle => false;

    /// <summary>风暴开始时回调（启用效果物体等）</summary>
    protected virtual void OnStormStart() { }

    /// <summary>风暴结束时回调（停用效果物体等）</summary>
    protected virtual void OnStormEnd() { }

    /// <summary>
    /// 工厂：按天气类型创建对应天气子类并应用（同种子 BattleRandom 下结果确定）
    /// </summary>
    public static WeatherSystem Create(WeatherType weather, Transform parent)
    {
        var go = new GameObject("WeatherCont");
        if (parent != null) go.transform.SetParent(parent);

        WeatherSystem instance = weather switch
        {
            WeatherType.Rain => go.AddComponent<WeatherRain>(),
            WeatherType.Desert => go.AddComponent<WeatherDesert>(),
            WeatherType.Snow => go.AddComponent<WeatherSnow>(),
            _ => go.AddComponent<WeatherSystem>()
        };

        instance.ApplyWeather(weather);
        return instance;
    }

    /// <summary>
    /// 应用指定天气：切换积雪渲染开关，并按 EffectPath 生成效果物体。
    /// 异常条件：EffectPath 加载失败时仅输出警告，不中断流程
    /// </summary>
    public virtual void ApplyWeather(WeatherType weather)
    {
        Weather = weather;

        // 积雪渲染只在下雪时生效
        SnowController.SetEnabled(weather == WeatherType.Snow);

        var prefab = LoadEffect(EffectPath);
        if (prefab == null) return;

        EffectObject = Instantiate(prefab, transform);
        EffectObject.name = prefab.name;
        // 周期风暴子类初始平静（隐藏），持续型天气（如雨天）直接显示
        EffectObject.SetActive(!UseStormCycle);
    }

    private void Update()
    {
        if (UseStormCycle) TickStorm();
    }

    /// <summary>
    /// 效果物体跟随玩家（LateUpdate 避免落后一帧）。
    /// 大地图上天气粒子只覆盖玩家周围区域，位置取玩家 Pos（高度可加 _heightOffset 偏移）；
    /// 玩家出生前或不存在时保持原位
    /// </summary>
    private void LateUpdate()
    {
        if (EffectObject == null) return;
        var player = ActorsManager.Player;
        if (player == null) return;
        var pos = player.Pos;
        EffectObject.transform.position = new Vector3(pos.x, pos.y + _heightOffset, pos.z);
    }

    /// <summary>周期风暴计时：平静 _calmDuration 秒后进入风暴，持续 _stormDuration 秒后回到平静</summary>
    private void TickStorm()
    {
        _stormTimer += Time.deltaTime;
        if (!_stormActive && _stormTimer >= _calmDuration)
        {
            _stormActive = true;
            _stormTimer = 0f;
            OnStormStart();
        }
        else if (_stormActive && _stormTimer >= _stormDuration)
        {
            _stormActive = false;
            _stormTimer = 0f;
            OnStormEnd();
        }
    }

    /// <summary>按路径加载效果物体预制体（子类 EffectPath 使用）</summary>
    protected GameObject LoadEffect(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;
        var prefab = ResSvc.Instance.LoadObject<GameObject>(resPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[WeatherSystem] 天气效果物体加载失败: {resPath}");
        }
        return prefab;
    }

    /// <summary>启停效果物体</summary>
    protected void SetEffectActive(bool active)
    {
        if (EffectObject != null) EffectObject.SetActive(active);
    }
}
