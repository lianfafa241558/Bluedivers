using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;

//using Unity.FPS.Game;
using UnityEngine;
using Utils;

/// <summary>
/// 天气控制器：由 BattleManager 开局随机抽取天气后通过 Create 工厂创建。
/// 具体天气组件（WeatherEffect 子类）挂在 Prefabs/Weather 下的特效预制体上，
/// 控制器按天气类型匹配预制体实例化，并统一驱动周期风暴计时与跟随玩家。
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    /// <summary>天气特效预制体所在目录（Resources 相对路径）</summary>
    private const string EffectFolder = "Prefabs/Weather";

    /// <summary>权重表为空或总权重非正时的兜底天气</summary>
    public const WeatherType DefaultWeather = WeatherType.Sunny;

    /// <summary>当前生效的天气</summary>
    public WeatherType Weather { get; private set; }

    /// <summary>当前天气特效实例（挂有 WeatherEffect 子类组件）</summary>
    private WeatherEffect _effect;

    // 风暴计时与状态
    private float _stormTimer;
    private bool _stormActive;

    /// <summary>
    /// 按地图配置的天气权重表抽取天气（Key=天气，Value=权重）。
    /// 使用 BattleRandom 时同种子结果确定；权重表为空或总权重非正时返回 Sunny。
    /// </summary>
    /// <param name="weatherInfos">天气权重表，可为 null</param>
    /// <param name="random">抽取用随机源（开局 BattleRandom）</param>
    public static WeatherType RollWeather(List<SKVP<WeatherType, int>> weatherInfos, System.Random random)
    {
        if (weatherInfos == null || weatherInfos.Count == 0 || random == null) return DefaultWeather;

        int totalWeight = weatherInfos.Sum(item => item.Value);
        if (totalWeight <= 0) return DefaultWeather;

        return weatherInfos.WeightTake(totalWeight, random);
    }

    /// <summary>
    /// 工厂：开局应用指定天气（同种子 BattleRandom 下结果确定）
    /// </summary>
    public static WeatherSystem Create(WeatherType weather, Transform parent)
    {
        var go = new GameObject("WeatherCont");
        if (parent != null) go.transform.SetParent(parent);
        var instance = go.AddComponent<WeatherSystem>();
        instance.ApplyWeather(weather);
        return instance;
    }

    /// <summary>
    /// 应用指定天气：切换积雪渲染开关，按天气类型匹配特效预制体并实例化。
    /// 异常条件：EffectFolder 下找不到对应天气的预制体时输出错误并中止
    /// </summary>
    public void ApplyWeather(WeatherType weather)
    {
        Weather = weather;

        // 积雪开关由 WeatherEffectSnow 自己管理（OnInit 开、OnDisable 关），控制器不再集中干预
        if (weather == WeatherType.Sunny) return;

        // 扫描天气预制体目录，按具体天气组件声明的类型匹配
        var prefab = ResSvc.Instance.LoadObjects<WeatherEffect>(EffectFolder)
            .FirstOrDefault(item => item != null && item.Type == weather);
        if (prefab == null)
        {
            Debug.LogError($"[WeatherSystem] 未找到天气({weather})对应的特效预制体，请检查目录: {EffectFolder}");
            return;
        }

        _effect = Instantiate(prefab, transform);
        _effect.name = prefab.name;
        _effect.OnInit();
    }

    private void Update()
    {
        if (_effect == null || !_effect.UseStormCycle) return;
        TickStorm();
    }

    /// <summary>
    /// 效果物体跟随相机（LateUpdate 避免落后一帧）。
    /// 大地图上天气粒子只覆盖相机周围区域，位置取相机位置；
    /// 相机不存在时保持原位
    /// </summary>
    private void LateUpdate()
    {
        if(!BattleManager.Instance.IsStartBattle) return;
        if (_effect == null) return;
        var cam = ActorsManager.Player;
        if (!cam.IsValid()) return;
        _effect.transform.position = cam.Pos;
    }

    /// <summary>周期风暴计时：平静 CalmDuration 秒后进入风暴，持续 StormDuration 秒后回到平静</summary>
    private void TickStorm()
    {
        _stormTimer += Time.deltaTime;
        if (!_stormActive && _stormTimer >= _effect.CalmDuration)
        {
            _stormActive = true;
            _stormTimer = 0f;
            StartStorm();
        }
        else if (_stormActive && _stormTimer >= _effect.StormDuration)
        {
            _stormActive = false;
            _stormTimer = 0f;
            EndStorm();
        }
    }

    /// <summary>风暴开始：通知具体天气组件（基类切换状态物体，子类叠加差异行为）</summary>
    private void StartStorm()
    {
        _effect.OnStormStart();
    }

    /// <summary>风暴结束：通知具体天气组件恢复平时状态</summary>
    private void EndStorm()
    {
        _effect.OnStormEnd();
    }
}
