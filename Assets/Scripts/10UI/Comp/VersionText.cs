using TMPro;
using UnityEngine;

namespace FPSGame.UI
{
    /// <summary>
    /// 把 TMP 文本刷成游戏版本号（ProjectSettings 的 bundleVersion，即 <c>Application.version</c>）。
    /// 只在 Awake 取一次，版本号运行期不会变，不需要每帧刷新。
    /// </summary>
    [AddComponentMenu("UI/工具/版本号文本")]
    public class VersionText : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("目标文本")]
        TMP_Text _text;

        [SerializeField]
        [InspectorName("显示格式")]
        string _format = "v{0}";

        void Awake()
        {
            // 留空就用本物体上的 TMP，免得在每个 prefab 上重复拖引用
            if (_text == null) _text = GetComponent<TMP_Text>();

            if (_text == null)
            {
                Debug.LogError("[VersionText] 本物体及其指定字段上都没有 TMP_Text", this);
                return;
            }

            _text.text = string.Format(_format, Application.version);
        }
    }
}
