using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    [Header("=== 时间设置 ===")]
    [Tooltip("一天总时长（秒），默认1800秒=30分钟")]
    public float fullDayDuration = 1800f;


    [Header("=== 环境光颜色 ===")]
    public Color noonAmbient = new Color(1f, 0.96f, 0.88f);    // 正午环境色
    public Color sunsetAmbient = new Color(1f, 0.6f, 0.4f);    // 黄昏环境色
    public Color nightAmbient = new Color(0.15f, 0.2f, 0.35f);  // 夜晚环境色
    public Color dawnAmbient = new Color(0.8f, 0.9f, 1f);      // 黎明环境色

    [Header("=== 雾效设置 ===")]
    //public bool enableFog = true;
    public Color noonFog = new Color(0.8f, 0.9f, 1f);
    public Color nightFog = new Color(0.1f, 0.12f, 0.2f);
    [Range(0.001f, 0.05f)] public float noonFogDensity = 0.01f;
    [Range(0.001f, 0.05f)] public float nightFogDensity = 0.02f;

    [Header("=== 天空球 ===")]
    public Material skyboxMaterial;


    [Header("=== 显示 ===")]
    [SerializeField]
    [Tooltip("当前时间进度 0~1")]
    [Range(0f,1f)]float currentTime = 0f;

    [SerializeField]
    Color showFogColor;
    [SerializeField]
    Color showLightColor;
    [SerializeField]
    Color showAmbColor;

    private bool isNoon;
    Light sunLight;


    private void Awake()
    {
        colorValue = new Color[] { nightAmbient, sunsetAmbient, noonAmbient, noonAmbient, dawnAmbient, nightAmbient };
        // 自动获取自身Light组件
        if (sunLight == null)
            sunLight = GetComponent<Light>();

        RenderSettings.skybox = skyboxMaterial;
        // 强制开启场景雾
        //RenderSettings.fog = enableFog;
        // 获取当前本地时间的 分钟 + 秒
        System.DateTime now = System.DateTime.Now;
        float minuteSecond = 60 * now.Minute + now.Second;

        // 对1800取模，作为初始时间
        currentTime = (minuteSecond % fullDayDuration)/ fullDayDuration;
        //currentTime = 0.75f;
        StartCoroutine(SetDayState());
    }

    IEnumerator SetDayState()
    {
        yield return new WaitForSeconds(1f);
        if (currentTime < 0.2f || currentTime > 0.8f)
        {
            GlobalEventManager.DaySwitch(isNoon = false);
        }
        else
        {
            isNoon = true;
        }
    }


    private void Update()
    {
        // 更新时间进度
        currentTime += Time.deltaTime / fullDayDuration;
        currentTime = Mathf.Repeat(currentTime, 1f);

        // 执行所有昼夜变化逻辑
        UpdateSunRotation();
        UpdateAmbientLight();
        UpdateFog();
        UpdateSkybox();

        if (isNoon && currentTime>= dayStageTime[3])
        {
            GlobalEventManager.DaySwitch(isNoon = false);
        }
        else if (!isNoon && currentTime > dayStageTime[1] && currentTime <= dayStageTime[3])
        {
            GlobalEventManager.DaySwitch(isNoon = true);
        }
    }

    // 1. 旋转太阳（360度循环）
    private void UpdateSunRotation()
    {
        float angle = currentTime * 360f - 90f;
        transform.rotation = Quaternion.Euler(angle, 0f, 0f);
    }


    private Color[] colorValue;
    // 3. 环境光颜色变化
    private void UpdateAmbientLight()
    {
        Color ambientColor = LerpScale(colorValue);
        sunLight.color = ambientColor;
        RenderSettings.ambientLight = ambientColor;
        showAmbColor = ambientColor;
    }

    private float[] fogValue = new float[] { 0, 0.3f, 1,1, 0.2f,0 };
    // 4. 雾效颜色 & 浓度
    private void UpdateFog()
    {

        float t = LerpScale(fogValue);

        // 雾颜色
        RenderSettings.fogColor = Color.Lerp(nightFog, noonFog, t);

        // 雾浓度
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, noonFogDensity, t);
        showFogColor = RenderSettings.fogColor;
    }

    private float[] skyValue = new float[] { 0, 0.3f, 1, 1, 0.3f, 0 };
    void UpdateSkybox()
    {
        skyboxMaterial.SetFloat("_Lerp", LerpScale(skyValue));
    }


    float[] dayStageTime = new float[] { 0f, 0.167f, 0.333f,0.542f, 0.708f, 0.792f,1};
    /// <summary>返回一天中的阶段，并且标准化</summary>
    /// <param name="value">对应阶段的值,长度6，分别为午夜值，清晨值，正午值，正午值，黄昏值，午夜值</param>
    float LerpScale(float[] value)
    {
        float t = Mathf.Repeat(currentTime, 1f);
        float scale;
        for (int i = 0; i < 6; ++i)
        {
            if (t < dayStageTime[i+1])
            {
                scale = Mathf.InverseLerp(dayStageTime[i], dayStageTime[i+1], t);
                return Mathf.Lerp(value[(i + 6 - 1) % 6],value[i],scale);
            }
        }
        return value[0];
    }


    /// <summary>返回一天中的阶段，并且标准化</summary>
    /// <param name="value">对应阶段的值,长度6，分别为午夜值，清晨值，正午值,正午值，黄昏值，午夜值</param>
    Color LerpScale(Color[] value)
    {
        float t = Mathf.Repeat(currentTime, 1f);
        float scale = 0;
        for (int i = 0; i < 6; ++i)
        {
            if (t < dayStageTime[i + 1])
            {
                scale = Mathf.InverseLerp(dayStageTime[i], dayStageTime[i + 1], t);
                return Color.Lerp(value[(i + 6 - 1) % 6], value[i], scale);
            }
        }
        return value[0];
    }

    // 手动跳转到指定时间（0~1）
    public void SetTime(float time01)
    {
        currentTime = Mathf.Clamp01(time01);
    }
}