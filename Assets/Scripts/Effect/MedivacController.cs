using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

public class MedivacController : TickBehaviour
{
    public event UnityAction Complete;

    [SerializeField]
    private Transform point,cam,target;
  

    [InspectorName("状态")]
    public MedivacState state;
    BoxCollider box;
    TaskManager taskManager;
    //private float time = 0;
    Animator anim;
    [SerializeField]
    AudioSource aud;


    [SerializeField]
    AudioClip landCilp;
    [SerializeField]
    bool complete, arrive,nextComplete;
    public Transform targetPoint;

    private List<I_Actor> players => ActorsManager.Players;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        box = point.GetComponent<BoxCollider>();
        //aud = GetComponent<AudioSource>();
    }

    protected override void Start()
    {
        base.Start();
        taskManager = TaskManager.Instance;
        //TickTime = 1;
        switch (state)
        {
            case MedivacState.Land:
                LandInit();
                break;
        }
    }

    public override bool Tick()
    {
        if (!taskManager.nowTask.IsValid()) return true;
        if (!enabled) return true;

        int count = 0;
        // 获取旋转矩阵的逆矩阵（局部→世界)
        Matrix4x4 localToWorld = box.transform.localToWorldMatrix;
        Matrix4x4 worldToLocal = localToWorld.inverse;
        Vector3 colliderSize = box.size;
        foreach (var item in players)
        {
            // 将点转换到碰撞体局部空间
            Vector3 localPoint = worldToLocal.MultiplyPoint3x4(item.transform.position);
            //Debug.LogError("玩家位置"+ localPoint, item.gameObject);
            // 检测局部坐标是否在[-0.5, 0.5]范围内（标准BoxCollider尺寸)
            if (Mathf.Abs(localPoint.x) <= colliderSize.x * 0.5f
                && Mathf.Abs(localPoint.y) <= colliderSize.y * 0.5f
                && Mathf.Abs(localPoint.z) <= colliderSize.z * 0.5f
            )
            {
                ++count;
            }
        }
        switch (state)
        {
            case MedivacState.Ready:
                SortieTick(count, players.Count);
                break;
            case MedivacState.Land:
                LandTick(count, players.Count);
                break;
            case MedivacState.Evacuate:
                EvacuateTick(count, players.Count);
                break;
        }
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (!taskManager.nowTask.IsValid()) return;
       
        switch (state)
        {
            case MedivacState.Land:
                LandUpdate();
                break;
            case MedivacState.Evacuate:
                cam.LookAt(target);
                break;
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

    private void LandInit()
    {
        TickTime = 3;
        anim.Play("Idle",0,0);
    }


    [SerializeField]
    private int showCount, ShowPlayerCount, showTime;
    private void SortieTick(int count,int playerCount)
    {
        if (taskManager.nowTask.Countdown >0)
        {
            // 全队强化"专家救援飞行员"：缩短撤离等待时间
            float mul = BattleManager.Instance&&BattleManager.Instance.HaveBooster(BoosterType.ExpertPilot)?0.6f:1f;
            if (count == 0)
            {
                taskManager.nowTask.Countdown = Mathf.RoundToInt(16 * mul);
            }
            else if (count < playerCount)
            {
                --taskManager.nowTask.Countdown;
            }
            else
            {
                int fullCount = Mathf.RoundToInt(6 * mul);
                if (taskManager.nowTask.Countdown > fullCount) taskManager.nowTask.Countdown = fullCount;

                --taskManager.nowTask.Countdown;
            }

        }
        else
        {
            //Debug.LogError("设置进入过场");
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
    private void LandTick(int count, int playerCount)
    {
        if (complete) return;
        if (nextComplete)
        {
            complete = true;
            anim.Play("Evacuate");
            Destroy(gameObject, 14);
        }
        if (count == 0)
        {
            nextComplete = true;
        }

    }

    private void EvacuateTick(int count, int playerCount)
    {
        if (count == playerCount&& !complete)
        {
            complete = true;
            anim.Play("Evacuate");
            cam.gameObject.SetActive(true);
            Complete?.Invoke();
            foreach (var item in players)
            {
                item.transform.parent = target;
                item.gameObject.SetActive(false);
            }
            BattleManager.Instance.EndGame(14);

        }
    }


    // 外面定义一个变量缓存速度
    private Vector3 _vel = Vector3.zero;

    private void LandUpdate()
    {
        if (arrive) return;

        Vector3 offset = transform.TransformVector(new Vector3(0, 4.5f, -1.5f));
        Vector3 targetPos = targetPoint.position + offset;
        var lastPos = transform.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        // 距离足够近,直接到位，停止抖动
        if (distance < 0.1f)
        {
            transform.position = targetPos;
            // 清空速度，防止惯性继续飘
            _vel = Vector3.zero;
            anim.Play("Wait");
            aud.PlayOneShot(landCilp);
            arrive = true;
            return;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _vel,
                0.7f,
                3
            );
            var dx = transform.position - lastPos;
            //ActorsManager.Players.ForEach(item=>item.gameObject.GetComponent<CharacterController>().Move(dx));
            ActorsManager.Players.ForEach(item => item.transform.position+= dx);
        }

      
        //if (count > 0)
        {

            //Vector3 targetPos = targetPoint.position + transform.TransformVector(new(0, 4f, -1.5f));
            //transform.position = Vector3.MoveTowards(transform.position, targetPos, 10 * Time.deltaTime);

        }
        //else
        //{
        //    allowComplete = true;
        //    anim.Play("Evacuate");
        //起飞
        //transform.position = Vector3.Lerp(transform.position, targetPoint.position + (Vector3.up * 5.5f + targetPoint.forward * -5.5f) * transform.lossyScale.x, 15 * Time.deltaTime);
        //}
    }

    public enum MedivacState
    {

        /// <summary>准备</summary>
        [InspectorName("准备")] Ready,
        /// <summary>降落</summary>
        [InspectorName("降落")] Land,
        /// <summary>撤离</summary>
        [InspectorName("撤离")] Evacuate,
    }
}