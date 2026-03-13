using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

public class SkyboxManager : MonoBehaviour
{
    public List<Material> newSkybox; // 在Inspector中拖拽你的天空盒材质

    void Start()
    {
        RenderSettings.skybox = newSkybox.RandomTake();
        // 如果需要更新光照，可以调用以下方法
        DynamicGI.UpdateEnvironment();
    }
}
