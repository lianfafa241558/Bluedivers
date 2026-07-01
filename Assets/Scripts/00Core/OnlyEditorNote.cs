using UnityEngine;
namespace Core
{
    /// <summary>
    /// 单纯加个文本给自己做备注
    /// </summary>
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