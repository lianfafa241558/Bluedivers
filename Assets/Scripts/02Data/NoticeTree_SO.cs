using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "NT_", menuName = "Data/角色台词组")]
public class NoticeTree_SO : ScriptableObject
{
    public string SourceName;
    public string ID;
    public Sprite Portrait;
    public bool UseResLoad;
    public List<SoundGroup_SO> sounds = new List<SoundGroup_SO>();
}
 