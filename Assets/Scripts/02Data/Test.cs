using UnityEngine;

public class TestInline : MonoBehaviour
{
    [Header("应该在一行显示")]
    public PlayerStats stats;
    public PlayerStats[] stats2;
    [Header("对比：正常的会多行显示")]
    public Vector3 normalVector; // 默认就是多行
}
[System.Serializable]
[Inline]
public class PlayerStats
{
    public int health = 100;
    public int mana = 50;
    public float speed = 5.5f;
    public string name = "Hero";
}