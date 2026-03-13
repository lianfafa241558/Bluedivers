using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "new Data", menuName = "Data/更新说明")]
public class UpdateData_SO : ScriptableObject
{
    public string title;
    [TextArea(5,10)]
    public string desc;
    public string time;


    [ContextMenu("记录时间")]
    public void Init()
    {
        //time = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        time = DateTime.Now.ToString("yyyy-MM-dd");
        Debug.LogError(time);
    }
}
