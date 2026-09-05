using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

/// <summary>
/// 光环的发光变化控制器
/// </summary>
public class EmittController : MonoBehaviour
{

    [SerializeField]
    [SerializeReference]
    [SubclassSelector]
    private List<BaseModify> modifies;

    private MpbController mpb;

    void Start()
    {
        mpb = new(transform);
    }

    void LateUpdate()
    {
        if (mpb.IsAnyVisible()) Refresh();
    }

    private void OnBecameVisible()
    {
        // 从离屏变可见的当帧补一次，避免可见瞬间用旧值画 1 帧
        Refresh();
    }

    private void Refresh()
    {
        foreach (var item in modifies) item.Update(mpb);
        mpb.Apply();
    }

    [Serializable]
    private abstract class BaseModify
    {
        [SerializeField]
        protected string fieldName;
        [SerializeField]
        [Range(-3, 3)]
        protected float speedScale = 1;

        // 运行时缓存 PropertyID，不参与序列化；SerializeReference 反序列化不保证执行字段初始值，故用 bool 标记未解析
        [NonSerialized]
        private int fieldId;
        [NonSerialized]
        private bool idResolved;

        protected int GetFieldId()
        {
            if (!idResolved)
            {
                fieldId = Shader.PropertyToID(fieldName);
                idResolved = true;
            }
            return fieldId;
        }

        public abstract void Update(MpbController mpb);

    }

    [Serializable]
    private class ScaleModify : BaseModify
    {
        [SerializeField]
        [Range(0, 1)]
        private float emittScale = 0.5f;

        public override void Update(MpbController mpb)
        {
            mpb.Set(GetFieldId(), 1 + emittScale * Mathf.Sin(Time.time * speedScale));
        }
    }
    [Serializable]
    private class OffsetModify : BaseModify
    {
        [SerializeField]
        [Range(0, 5)]
        private float cycleScale = 0;
        [SerializeField]
        private bool isY;
        public override void Update(MpbController mpb)
        {
            int id = GetFieldId();
            float value;
            if (cycleScale > 0) value = cycleScale * Mathf.Sin(Time.time * speedScale) % 1;
            else value = Time.time * speedScale % 1;

            if (isY) mpb.SetOffsetY(id, value);
            else mpb.SetOffsetX(id, value);
        }
    }
}
