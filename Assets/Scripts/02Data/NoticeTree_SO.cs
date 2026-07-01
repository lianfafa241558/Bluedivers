using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using static DynamicBoneColliderBase;
using Unity.FPS.Game;







#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

[CreateAssetMenu(fileName = "NT_", menuName = "Data/角色台词组")]
public class NoticeTree_SO : ScriptableObject
{
    public string SourceName;
    public string ID;
    public Sprite Portrait;
    public bool UseResLoad;
    public List<SoundGroup_SO> sounds = new List<SoundGroup_SO>();
}
 