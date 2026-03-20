using System.Collections;
using System.Collections.Generic;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

public class MedivacController : MonoBehaviour
{
    public event UnityAction Complete;

    [SerializeField]
    private Transform point,cam,target;
  

    [CustomLabel("状态")]
    public MedivacState state;
    BoxCollider box;
    TaskManager taskManager;
    private float time = 0;
    Animator anim;
    bool allowInit;
    bool allowComp,allowComplete;
    private List<I_Actor> players => ActorsManager.Players;

    private void Start()
    {
        Init();
    }

    public void Init()
    {
        if (allowInit) return;
        allowInit = true;
        taskManager = TaskManager.Instance;
        anim = GetComponent<Animator>();
        box = point.GetComponent<BoxCollider>();
    }

    void Update()
    {
        if (!taskManager.nowTask.IsValid()) return;
        cam.LookAt(target);
        if ((time += Time.deltaTime) > 1)
        {
            time -= 1;
            int count = 0;
            // 获取旋转矩阵的逆矩阵（局部→世界）
            Matrix4x4 localToWorld = box.transform.localToWorldMatrix;
            Matrix4x4 worldToLocal = localToWorld.inverse;
            Vector3 colliderSize = box.size;
            foreach (var item in players)
            {
                // 将点转换到碰撞体局部空间
                Vector3 localPoint = worldToLocal.MultiplyPoint3x4(item.transform.position);

                // 检测局部坐标是否在[-0.5, 0.5]范围内（标准BoxCollider尺寸）
                if(Mathf.Abs(localPoint.x) <= colliderSize.x * 0.5f 
                    && Mathf.Abs(localPoint.y) <= colliderSize.y * 0.5f
                    && Mathf.Abs(localPoint.z) <= colliderSize.z * 0.5f
                ){
                    ++count;
                }
            }
            switch (state)
            {
                case MedivacState.Ready:
                    SortieUpdate(count, players.Count);
                    break;
                case MedivacState.Land:
                    LandUpdate(count, players.Count);
                    break;
                case MedivacState.Evacuate:
                    EvacuateUpdate(count, players.Count);
                    break;
            }
        }
        
    }

    public void SetType(MedivacState type)
    {
        state = type;
    }
    public void Play(string name)
    {
        anim.Play(name);
    }


    [SerializeField]
    private int showCount, ShowPlayerCount, showTime;
    private void SortieUpdate(int count,int playerCount)
    {
        if (taskManager.nowTask.Countdown >0)
        {
            if (count == 0)
            {
                taskManager.nowTask.Countdown = 16;
            }
            else if (count < playerCount)
            {
                --taskManager.nowTask.Countdown;
            }
            else
            {
                if (taskManager.nowTask.Countdown > 6) taskManager.nowTask.Countdown = 6;

                --taskManager.nowTask.Countdown;
            }

        }
        else
        {
            taskManager.EnterTransition();
            enabled = false;
            foreach (var item in players)
            {
                item.gameObject.SetActive(false);
            }
        }
        showCount = count;
        ShowPlayerCount= playerCount;
        showTime= taskManager.nowTask.Countdown;
    }
    private void LandUpdate(int count, int playerCount)
    {
        if (count == 0)
        {
            //起飞

        }
    }

    private void EvacuateUpdate(int count, int playerCount)
    {
        if (count == playerCount&& !allowComplete)
        {
            allowComplete = true;
            anim.Play("Evacuate");
            Complete?.Invoke();
            foreach (var item in players)
            {
                item.transform.parent = target;
                item.gameObject.SetActive(false);
            }
            BattleManager.Instance.EndGame(11);

        }
    }

    public enum MedivacState
    {

        /// <summary>准备</summary>
        [CustomLabel("准备")] Ready,
        /// <summary>降落</summary>
        [CustomLabel("降落")] Land,
        /// <summary>撤离</summary>
        [CustomLabel("撤离")] Evacuate,
    }
}