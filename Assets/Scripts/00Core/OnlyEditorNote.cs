using UnityEngine;
namespace FPSGame.Core
{
    /// <summary>
    /// 单纯加个文本给自己做备注
    /// </summary>
    [AddComponentMenu("框架/编辑器备注")]
    public class OnlyEditorNote : MonoBehaviour
    {
        [TextArea(5, 5)]
        public string note;

        void Start()
        {
            Destroy(this);
        }

    }
}
