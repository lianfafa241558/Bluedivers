// 鼠标拖拽旋转模块类
using UnityEngine;

public class DragRotate : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 5f;
    [SerializeField] private float autoReturnSpeed = 2f;
    [SerializeField] private bool enableAutoReturn = true;

    private Vector3 lastDragPoint;
    private Quaternion targetRotation;
    private bool isDragging;

    void Update()
    {
        if (InputManager.GetDown(InputState.Fire))
        {
            lastDragPoint = Input.mousePosition;
            isDragging = true;
        }
        else if (InputManager.Get(InputState.Fire))
        {
            var dx = (Input.mousePosition - lastDragPoint).x;
            lastDragPoint = Input.mousePosition;
            var delta = dx * rotateSpeed * Time.deltaTime;
            transform.localEulerAngles += Vector3.up * delta;
            targetRotation = transform.localRotation;
        }
        else if (isDragging && enableAutoReturn)
        {
            // 自动返回逻辑
            isDragging = false;
            targetRotation = Quaternion.identity;
        }

        if (!isDragging && enableAutoReturn)
        {
            transform.localRotation = Quaternion.Lerp(
                transform.localRotation,
                targetRotation,
                Time.deltaTime * autoReturnSpeed
            );
        }
    }
}