using FPSGame.GameData;
using FPSGame.Gameplay;
using UnityEngine;

namespace FPSGame.UI
{
/// <summary>
/// 按存档设置项控制自身显隐：Start 读一次，之后跟着 <c>GlobalEventBus.OnSettingCange</c> 实时刷新
/// （设置面板里一改，大厅提示立刻显/隐，不必重开窗口）。
/// <para>▍注意：本类靠 <c>SetActive(false)</c> 关自己，物体失活**不会**退订静态事件 ⇒ 还能被设置改回来。
/// 退订只放在 <c>OnDestroy</c> 一处（成对即可，别在显隐里动订阅）。</para>
/// </summary>
[AddComponentMenu("UI/按存档设置隐藏自己")]
public class HideByArchiveSetting : MonoBehaviour
{
    [InspectorName("设置项名")]
    [Tooltip("传给 ArchivesData_SO.GetSetting 的键，值 > 0 视为「开」。缺键时 GetSetting 会报错并按 0 处理。")]
    [SerializeField]
    private string settingKey = "隐藏大厅提示UI文本";

    [InspectorName("开启时隐藏自己")]
    [Tooltip("勾上 = 设置为开时隐藏；不勾 = 设置为开时显示（即反向）。")]
    [SerializeField]
    private bool hideWhenOn = true;

    private void Start()
    {
        var archive = ArchivesData_SO.Current;
        if (archive == null)
        {
            // 存档由 09 的 ArchiveLoader.Init() 写入；没就绪说明挂到了比它更早的物体上 ⇒ 保持预制体原状，别猜
            Debug.LogError("[按设置隐藏] 存档未就绪（ArchiveLoader.Init 还没跑），" + name + " 保持原状。");
            return;
        }

        // 订阅必须在这里（不是 Awake）：存档没就绪时上面已 return，订阅了也只会读到 0
        GlobalEventBus.OnSettingCange += OnSettingCange;
        Apply(archive.GetSetting(settingKey));
    }

    private void OnDestroy()
    {
        GlobalEventBus.OnSettingCange -= OnSettingCange;
    }

    private void OnSettingCange(string key, float value)
    {
        if (key != settingKey) return;   // 键比较用全等：事件会广播所有设置项
        Apply(value);
    }

    private void Apply(float value)
    {
        bool on = value > 0f;
        gameObject.SetActive(hideWhenOn ? !on : on);
    }
}
}
