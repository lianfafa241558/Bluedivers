using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Data;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Utils;

namespace FPSGame.Gameplay
{

    /// <summary>
    /// 医疗撤离流程驱动。
    /// </summary>
    [AddComponentMenu("特效/医疗撤离")]
    public class MedivacController : TickBehaviour
    {
        public event UnityAction Complete;

        [SerializeField]
        private Transform point,cam,target;
  

        [InspectorName("状态")]
        public MedivacState state;
        BoxCollider box;
        //private float time = 0;
        Animator anim;
        [SerializeField]
        AudioSource aud;


        [SerializeField]
        AudioClip landCilp;
        [SerializeField]
        bool complete, arrive,nextComplete;
        public Transform targetPoint;

        private List<IActor> players => ActorsManager.Players;

        private void Awake()
        {
            anim = GetComponent<Animator>();
            box = point.GetComponent<BoxCollider>();
            //aud = GetComponent<AudioSource>();
        }

        protected override void Start()
        {
            base.Start();
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
            if (!TaskState.HasTask) return true;
            if (!enabled) return true;
            //Debug.LogWarning("有任务");
            //统计当前在登船舱内的玩家数
            // ⚠ 必须过滤"已销毁"的接口引用：盟友离场时实体被 Destroy，若注册表里还留着它，
            //   这里访问 transform 会抛 MissingReferenceException（2026-10-07 实测）；
            //   接口引用不能用 == null 判（不走 Unity 的 Object 重载）⇒ 用工程约定的 IsValidMono()。
            //   total 只统计有效项，避免"人数分母"把已销毁的人也数进去。
            int count = 0;
            int total = 0;
            foreach (var item in players)
            {
                if (!item.IsValidMono()) continue;
                ++total;
                if (IsInBox(item.transform.position)) ++count;
            }
            switch (state)
            {
                case MedivacState.Ready:
                    SortieTick(count, total);
                    break;
                case MedivacState.Land:
                    LandTick(count, total);
                    break;
                case MedivacState.Evacuate:
                    EvacuateTick(count, total);
                    break;
            }
            return true;
        }

        protected override void Update()
        {
            base.Update();
            if (!TaskState.HasTask) return;
       
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
            
            // 撤离倒计时走「数据自持」（2026-10-01 取代 ServiceLocator.Task 槽）：
            // TaskState.Countdown 是**共享读数**——本类推进它、TaskManager 与倒计时 UI 读它。
            if (TaskState.Countdown >0)
            {
                if (count == 0)
                {
                    TaskState.Countdown = Mathf.RoundToInt(16);
                }
                else if (count < playerCount)
                {
                    --TaskState.Countdown;
                }
                else
                {
                    int fullCount = Mathf.RoundToInt(6);
                    if (TaskState.Countdown > fullCount) TaskState.Countdown = fullCount;
                    --TaskState.Countdown;
                }

            }
            else
            {
                //Debug.LogError("设置进入过场");
                //切阶段走**事件**（原 `ServiceLocator.Flow.SetGameState`；更早是 `ServiceLocator.Task.EnterTransition()`）：
                //撤离流程到此结束 ⇒ 请求进入"配置战备"阶段，由 `GameRoot` 订阅后落成既有的静态 setter（照旧发 SceneChange 广播）。
                //⚠ 倒计时读数**不在这里复位**：复位是"开始新一局"的职责，已在 `TaskManager.SetTask()` 里
                //   （本组件一局只会走到这里一次，已确认）。
                GlobalEventSub.RequestGameState(GameStateEnum.Armament);
                enabled = false;
                foreach (var item in players)
                {
                    item.gameObject.SetActive(false);
                }
            }
            showCount = count;
            ShowPlayerCount= playerCount;
            showTime= TaskState.Countdown;
        }

        /// <summary>
        /// 游戏开始降落
        /// </summary>
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

        /// <summary>
        /// 撤离阶段等人
        /// </summary>
        private void EvacuateTick(int count, int playerCount)
        {
            //全员登船才起飞(超时强制起飞走 ForceTakeOff)
            if (count == playerCount && !complete)
            {
                TakeOff();
            }
        }

        /// <summary>
        /// 强制起飞：不等全员登机，只把当前已在舱内的玩家送上船。
        /// 供撤离超时等"必须收尾"的场景调用；同样会派发 Complete。
        /// </summary>
        public void ForceTakeOff()
        {
            if (complete) return;
            TakeOff();
        }

        /// <summary>起飞：播放撤离动画/切镜头/派发 Complete，并隐藏成功登船(在舱内)的玩家</summary>
        private void TakeOff()
        {
            complete = true;
            anim.Play("Evacuate");
            cam.gameObject.SetActive(true);
            Complete?.Invoke();
            foreach (var item in players)
            {
                if (!item.IsValidMono()) continue;
                //只隐藏已登船的玩家，没上船的不动
                if (!IsInBox(item.transform.position)) continue;
                item.transform.parent = target;
                item.gameObject.SetActive(false);
            }
        }

        /// <summary>世界坐标是否位于登船舱(BoxCollider)内</summary>
        private bool IsInBox(Vector3 worldPos)
        {
            if (!box) return false;
            Vector3 localPoint = box.transform.localToWorldMatrix.inverse.MultiplyPoint3x4(worldPos);
            Vector3 colliderSize = box.size;
            return Mathf.Abs(localPoint.x) <= colliderSize.x * 0.5f
                && Mathf.Abs(localPoint.y) <= colliderSize.y * 0.5f
                && Mathf.Abs(localPoint.z) <= colliderSize.z * 0.5f;
        }


        // 外面定义一个变量缓存速度
        private Vector3 _vel = Vector3.zero;

        private void LandUpdate()
        {
            //Debug.LogError(1);
            if (arrive) return;
            
            Vector3 offset = transform.TransformVector(new Vector3(0, 4.5f, -1.5f));
            Vector3 targetPos = targetPoint.position + offset;
            var lastPos = transform.position;
            float distance = Vector3.Distance(transform.position, targetPos);
            //Debug.DrawLine(transform.position, targetPos,Color.red,Time.deltaTime*2);
            //Tool.DrawLabel(transform.position,"当前点 ",Time.deltaTime * 2);
            //Tool.DrawLabel(targetPos, "，目标点 ", Time.deltaTime * 2);
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
                //ActorsManager.Players.ForEach(item => item.transform.position+= dx);
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
}
