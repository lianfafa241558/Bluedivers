using UnityEngine;

/// <summary>
/// 光环控制器：让特效带柔和滞后地跟随父物体
/// 通过私有 _current 维护当前位置状态，避免"父物体移动自动带动子物体"污染插值起点，
/// 使节点虽然挂在父物体层级下，但滞后效果与独立物体一致
/// </summary>
public class HaloController : MonoBehaviour
{

    [InspectorName("跟随目标")]
    public Transform parent;
    public Vector3 dv;
    public Vector3 lastPos;

    [InspectorName("滞后恢复速度")]
    public float speed = 10;

    [InspectorName("回正速度")]
    public float returnSpeed = 10;

    [InspectorName("瞬移判定距离")]
    public float snapDistance = 6;

    [InspectorName("调试日志")]
    public bool debug;

    private Vector3 _prevParentPos;
    private Vector3 _current;

    void Start()
    {
        // 默认跟随目标：优先取"真正被 CharacterController 移动的祖先"，否则取父级
        if (!parent)
        {
            var cc = GetComponentInParent<CharacterController>();
            parent = cc != null ? cc.transform : transform.parent;
        }

        // 相对跟随目标的本地偏移
        dv = parent.InverseTransformPoint(transform.position);
        _current = transform.position;
        lastPos = parent.position;
        _prevParentPos = parent.position;
    }

    void LateUpdate()
    {
        UpdateHalo();
    }

    private void UpdateHalo()
    {
        if (parent == null) return;

        if (debug)
        {
            Debug.Log($"[Halo] frame{Time.frameCount} parentMove={Vector3.Distance(parent.position, _prevParentPos):F3} parent={parent.position} halo={transform.position}");
        }

        // 瞬移检测：按父物体帧间位移突变判定，正常滞后不会误触发硬贴
        if (Vector3.Distance(parent.position, _prevParentPos) > snapDistance)
        {
            // 帧间位移过大疑似瞬移（传送/场景重置），直接跟
            _current = parent.TransformPoint(dv);
            lastPos = parent.position;
        }
        else
        {
            // 目标点 = 父物体当前位置 + 相对偏移 + 上一帧位置差量，制造滞后
            Vector3 target = parent.TransformPoint(dv) + (lastPos - parent.position);
            // 用私有 _current 插值：父级自动带动不会污染插值起点，滞后稳定成立
            _current = Vector3.Lerp(_current, target, Mathf.Clamp01(returnSpeed * Time.deltaTime));
            lastPos = Vector3.Lerp(lastPos, parent.position, Mathf.Clamp01(speed * Time.deltaTime));
        }

        // 写回世界位置（Unity 自动换算为相对父级的 localPosition，节点仍留在父级层级下）
        transform.position = _current;
        _prevParentPos = parent.position;
    }
}
