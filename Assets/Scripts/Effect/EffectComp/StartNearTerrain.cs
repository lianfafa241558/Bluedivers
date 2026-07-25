using UnityEngine;

/// <summary>
/// 初始紧贴地面，不需要给actor,自带
/// </summary>
public class StartNearTerrain : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(ExecuteAfterDelay());
    }

    System.Collections.IEnumerator ExecuteAfterDelay()
    {
        // 等待1秒
        yield return new WaitForSeconds(1f);

        // 执行原来的逻辑
        transform.position = TerrainUtils.WSToTS(transform.position);

        // 执行完后销毁自己
        Destroy(this);
    }
}