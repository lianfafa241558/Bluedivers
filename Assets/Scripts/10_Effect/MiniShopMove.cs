using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using UnityEngine;
using FPSGame.Managers;
using FPSGame.Gameplay;

namespace FPSGame.Effect
{

/// <summary>
/// 小商店与展示台的移动表现。
/// </summary>
[AddComponentMenu("特效/小商店移动")]
public class MiniShopMove : MonoBehaviour
{
    
    public Transform pointRoot;

    protected void Start()
    {
        GlobalEventBus.OnGameStateChange += OnGameStateChange;

    }

    private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        if(entry== GameStateEnum.Ready&& pointRoot!=null)
        {
            if (pointRoot == null) return;

            // 把子物体先转 List，避免IL2CPP 遍历bug
            List<Transform> children = new List<Transform>();
            for(int i=0;i< pointRoot.childCount; ++i)
            {
                children.Add(pointRoot.GetChild(i));
            }
            //foreach (Transform t in pointRoot)
                

            // 现在安全遍历
            foreach (Transform item in children)
            {
                if ( item.GetChild(1).GetComponent<TMPro.TextMeshPro>().text== TaskManager.Instance.MapId)
                {
                    transform.position = item.GetChild(1).position+0.3f*Vector3.up;
                }
            }
            
        }
    }
}
}
