using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

public class MiniShopMove : MonoBehaviour
{
    
    public Transform pointRoot;

    protected void Start()
    {
        GameRoot.OnGameStateChange += OnGameStateChange;

    }

    private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        if(entry== GameStateEnum.Ready)
        {
            foreach (Transform item in pointRoot)
            {
               if( item.GetChild(1).GetComponent<TMPro.TextMeshPro>().text== TaskManager.Instance.nowTask.mapName)
                {
                    transform.position = item.GetChild(1).position+0.3f*Vector3.up;
                }
            }
            
        }
    }
}
