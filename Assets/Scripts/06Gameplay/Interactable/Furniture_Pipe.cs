using System;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Utils;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEngine;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace FPSGame.Gameplay
{
// 原有一行 `using static UnityEngine.Rendering.DebugUI;` 是废弃 using（全文件 0 处使用），
// 而 DebugUI 属 Unity.RenderPipelines.Core 程序集 ⇒ 会把 06_Gameplay 拖上一个不必要的包依赖，已删。

//4个状状态
//Pipe 投影状态
//PipeLink 等待连接态
//PipeWait 等待安装态
//PipeComplete 完成态



    /// <summary>
    /// 管道
    /// </summary>
    [AddComponentMenu("交互/管道")]
    public class Furniture_Pipe : Furniture_Attached
    {
        public static List<Furniture_Attached> targets=new();

        /// <summary>本根管道状态**已落定**（在 <see cref="SetId"/> 里、Operate 的 switch 执行完之后触发）。
        /// <para>▍为什么不能监听 <c>Furniture_Attached.OnOperate</c>：它在 <c>base.Operate()</c> 里就发出，
        /// 早于 switch ⇒ 监听方此刻看到的 <see cref="Id"/> 还是旧的（PipeWait），
        /// 判不出"下一根是否已可建造"（<see cref="MissionSubConnectPipes"/> 的 Wait 标记就靠这条链推进）。</para></summary>
        public event Action OnStateSettled;

        [Foldout("配置", true)]
        [SerializeField]
        GameObject root;

        [SerializeField]
        GameObject Temp;

        [SerializeField]
        Renderer pipeSkin;
        [SerializeField]
        Renderer pipeSkeleton;

        [SerializeField]
        /// <summary>上一根管道</summary>
        Furniture_Pipe lastPipeline;
        [SerializeField]
        /// <summary>下一根管道</summary>
        Furniture_Pipe nextPipeline;

        private Furniture_Attached target;

        [SerializeField]
        private GameObject tip;
        public override void Operate()
        {
            base.Operate();
            /*
            var user = owner;
            if (HaveFlag(FurnitureFlag.Speech))
            {
                GlobalEventManager.PlayMeetSoeech(user, SpeechTypeEnum.Responded);
            }
            if (audioOper) PlaySound(audioOper);
            lastOperatetime = Time.time;
            GlobalEventManager.FurnitureOperate(user, this);
            */
            switch (Id)
            {
                case "Pipe":
                    Place();
                    break;
                case "PipeLink":
                    Link();
                    break;
                case "PipeWait":
                    Complete();
                    break;
                case "PipeComplete":
                    Link();//正常情况下是canoper已经关掉的，但是初始的那根除外
                    break;
                case "PipeError":
                    Complete();
                    break;
            }
        }



        /// <summary>PipeWait 的"现在就能建"判据：下一根已就位、上一根已完成（首根没有上一根也算）。
        /// <para>⚠ 任务侧点 tip（<see cref="MissionSubConnectPipes"/>）也用这一条，别在两处各写一份判据。</para></summary>
        public bool CanBuildNow => Id == "PipeWait" && nextPipeline && (lastPipeline == null || lastPipeline.Id == "PipeComplete");

        public override bool CanOperate(GameObject unit)
        {
            if (Id == "PipeWait" && !CanBuildNow) return false;
            return canOperate;
        }

        protected override void InOperateUpdate()
        {
            if (!owner) return;

            var nearest = targets.Find(t =>t.IsValid() && Vector3.Distance(relatedTrans.position, t.relatedTrans.position) < 2);

            if (nearest.IsValid())
            {
                target = nearest;
                relatedTrans.position = nearest.relatedTrans.position;
            }

            else if (Physics.Raycast(owner.transform.TransformPoint(Vector3.forward * 1.1f) + Vector3.up * 10, Vector3.down, out var hitInfo, 20, LayerDefinition.GroundLayers))
            {
                relatedTrans.position = hitInfo.point+Vector3.up*0.5f;
            }

        }

        [ContextMenu("确定位置")]
        public void Place()
        {

            //初始管道一开始就是link；不用走这个阶段
            if (pipeSkeleton) pipeSkeleton.sharedMaterial = pipeSkin.sharedMaterial;
            inOperate = false;
            SetId("PipeLink");
            audioOper = null;
            // ⚠ 下面这些是"本端表现 / 会改地形"的副作用：**远端重放时跳过** —— 两端各做一次会叠加
            //   （ModifyHeightMap 是往地形高度图上挖坑，不是幂等操作）。
            if (!IsRemoteReplay)
            {
                if (pipeSkin) FpsHelper.UpdatePipeBounds(pipeSkin.GetComponent<SkinnedMeshRenderer>(),transform);
                if (pipeSkeleton) FpsHelper.UpdatePipeBounds(pipeSkeleton.GetComponent<SkinnedMeshRenderer>(),transform);
            }
            if (root.TryGetComponent<CCDIK>(out var cCDIK))
            {
                cCDIK.enabled = false;
                foreach (var item in root.GetComponentsInChildren<Collider>())
                {
                    item.enabled = true;
                    item.gameObject.layer = 0;
                    //ModifyHeightMap 是协程（迭代器），必须用 StartCoroutine 启动，直接调用不会执行
                    // ⚠ 远端重放要跳过：这是"改本端地形高度图"的副作用，两端各挖一次会叠加（见 IsRemoteReplay）。
                    if (!IsRemoteReplay)
                        FPSGame.Core.TimerHost.RunCoroutine(TerrainUtils.ModifyHeightMap(item.transform.position,0, 2f,0.5f, ShapeType.Circle, false,false));
                }
            }

            if (target)
            {
                Complete();
                target.relatedTrans = transform;
                target.Operate();
            }
            else if (!lastPipeline.lastPipeline.IsValid())
            {
                Complete();
                canOperate = true;
            }

        }
    
        public List<Furniture_Pipe> GetAllPipes()
        {
            var re = new List<Furniture_Pipe>();

            Furniture_Pipe item = this;
            re.Add(item);

            while (item.lastPipeline.IsValid())//往前取
            {
                item = item.lastPipeline;
                re.Add(item);
            }
            item = this;
            while (item.nextPipeline.IsValid())//往后取
            {
                item = item.nextPipeline;
                re.Add(item);
            }
            return re;
        }

        [ContextMenu("连接管线")]
        public void Link()
        {
            //Debug.LogError("连接",gameObject);
            nextPipeline = Instantiate(Temp, transform.position, transform.rotation, root.transform.parent).GetComponentInChildren<Furniture_Pipe>();
            nextPipeline.lastPipeline = this;
            nextPipeline.owner = owner;
            if (lastPipeline)
            {
                SetId("PipeWait");
                desc = "建造管道";
                inOperate = false;
                meetTime = 3;
            }
            else
            {
                Complete();
            }

        }
        /// <summary>点亮"等待建造"标记（tip = prefab 里那个名为 Wait 的子物体）。
        /// 平台上的起始管道（OilPlane.prefab 里那 3 根，Id=PipeLink）没配它 ⇒ 这里可能为空。</summary>
        public void Tip()
        {
            if (tip) tip.SetActive(true);
        }

        /// <summary>熄灭"等待建造"标记（移交下一根时用）。</summary>
        public void UnTip()
        {
            if (tip) tip.SetActive(false);
        }

        [ContextMenu("完成")]
        public void Complete()
        {
            //Debug.LogError("完成", gameObject);
            SetId("PipeComplete");
            UnTip();
            PlaySound(audioClose);
            flags &= FurnitureFlag.ControlAnim;

            canOperate = false;
            inOperate = false;
            meetTime = 3;
            Press = 3;
            if(relatedTrans2) relatedTrans2.gameObject.SetActive(false);
        }

        [ContextMenu("错误")]
        public void Error()
        {
            SetId("PipeError");
            canOperate = true;
            meetTime = 5;
            Press = 0;
            if (relatedTrans2) relatedTrans2.gameObject.SetActive(true);
        }

        /// <summary>
        /// 写入状态 Id（PipeLink/PipeWait/PipeComplete/PipeError，任务与 <see cref="MissionSubConnectPipes"/> 依赖它）。
        /// <para>身份数据统一保存在身份组件（BaseObject）上，家具本地不另存一份，故这里写回身份源。</para>
        /// </summary>
        /// <summary>状态名的 int 编码（接收端**优先**用原始串解析，这个只在对方没带 Key 时兜底）。</summary>
        public const int StatePipe = 0, StateLink = 1, StateWait = 2, StateComplete = 3, StateError = 4;

        private void SetId(string value)
        {
        if (Identity.IsValidMono()) Identity.Id = value;
        // ★ 联机（2026-10-11）：管道状态（决定 CanOperate 与任务进度）必须两端一致 ⇒ 上报（键 = 家具 SyncId，值 = 状态名）。
        //   没有交互驱动的那条路径（任务 EnterRepair 直接调 Error()）也能靠它传出去。
        ReportState(EncodeState(value), value);
        // 状态已写进身份源 ⇒ 此时 CanBuildNow / GetAllPipes 才是新值，通知监听方重算
        OnStateSettled?.Invoke();
        }

        private static int EncodeState(string value)
        {
            switch (value)
            {
                case "Pipe": return StatePipe;
                case "PipeLink": return StateLink;
                case "PipeWait": return StateWait;
                case "PipeComplete": return StateComplete;
                case "PipeError": return StateError;
                default: return -1;
            }
        }

        private static string DecodeState(int code)
        {
            switch (code)
            {
                case StatePipe: return "Pipe";
                case StateLink: return "PipeLink";
                case StateWait: return "PipeWait";
                case StateComplete: return "PipeComplete";
                case StateError: return "PipeError";
                default: return null;
            }
        }

        /// <summary>【联机】应用远端状态：还原到同一个 <c>Id</c>（<c>CanOperate</c> 与任务的判定随之一致）。</summary>
        public override void ApplyRemoteState(int code, string key)
        {
            string value = !string.IsNullOrEmpty(key) ? key : DecodeState(code);
            if (string.IsNullOrEmpty(value)) return;
            SetId(value);
        }

    }
}
