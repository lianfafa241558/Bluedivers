using System.Collections.Generic;
using System.Linq;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.AI;
using FPSGame.Gameplay;
using FPSGame.MapUtils;
namespace FPSGame.Mission
{

    public abstract class MissionEvacuateBase : MissionBase
    {
       
        public bool IsFast;
        [SerializeField]
        protected Transform _startPointEntity;

        protected int countDown;
        protected int suspendCountDown;


        protected Vector3 areaPoint;

        protected Transform area;

        protected MedivacController medivac;
        protected bool IsComplete;

        /// <summary>运输机落点(创建运输机时算一次并顺手清场，降落时直接复用，保证"清过的就是落点")</summary>
        protected Vector3 medivacLandPos;

        /// <summary>落点周边要清掉的树/石块/覆盖物半径(米)：机体水平占位约 13.6×16.0，取半长再留余量。</summary>
        protected const float MedivacClearRadius = 12f;

        /// <summary>随队撤离的凯伊(动态撤离用它身上的回收信标发起撤离)</summary>
        protected SpecUnitKei kei;

        /// <summary>地图上可选的其他撤离点(次要撤离区)，任务创建后由 MissionController 注入</summary>
        protected readonly List<MissionEvacuateSecondary> _secondaryZones = new();

        /// <summary>快速模式下是否允许用场景中 Tag=StartPoint 的物体顶替任务实体(动态撤离不需要，见 MissionEvacuateMobile)</summary>
        protected virtual bool UseSceneStartPoint => true;

        protected override void StartMission()
        {

            medivac.SetType(MedivacController.MedivacState.Land);


        }

        protected override void InitMission()
        {
            if (!entity)
            {
                if (IsFast && UseSceneStartPoint)
                {

                    var go = GameObject.FindWithTag("StartPoint");
                    if (go)
                    {
                        Debug.LogWarning("尝试设置init" + gameObject, gameObject);
                        entity = go.GetComponent<MissionView>();
                        entity.Init(this, new int[0]);
                    }
                    else
                    {
                        base.InitMission();
                    }
                }
                else
                {
                    base.InitMission();
                }
            }


            if (!entity)
            {
                Debug.LogError("撤离任务缺少任务实体：既没有场景实体也没有可实例化的 prefabs，无法继续初始化", this);
                return;
            }

            pos = entity.Pos;
            area = entity.transform;
            areaPoint = area.transform.position;
            // 注意：不能用 ?? 对 Unity Object 判空（无法识别未赋值/已销毁的伪 null），改用 Unity 重载的 != null
            Transform point = _startPointEntity != null ? _startPointEntity : area;
            if (point == null)
            {
                Debug.LogError("撤离任务起始点为空：_startPointEntity 与 area 均为空，无法创建撤离单位", this);
                return;
            }

            var keiGo = Res.CreatPrefab("Prefabs/BattleBase/Kei", false, point.TransformPoint(0, 0, 10));
            if (keiGo) kei = keiGo.GetComponent<SpecUnitKei>();
            medivac = Res.CreatPrefab("Prefabs/BattleBase/NeoNimbus", true, point.TransformPoint(0, 12, 0)).GetComponent<MedivacController>();
            medivac.SetType(MedivacController.MedivacState.Load);
            medivac.targetPoint = point;
        }

        public override void Link(MissionBase mission)
        {
            mission.OnMissionEnd += Activation;

        }

        public virtual void Activation(MissionBase mission)
        {
            mission.OnMissionEnd -= Activation;
            IsComplete = mission.completed;
            //stage = EvacuateState.Activation;

            RemoveTag(MissionTag.HideAll);
            AddTag(MissionTag.IsActive);
            BattleEventBus.MissionStateChange(this, true);
            BattleEventBus.MissionEnityShow(entity);
            // 激活会改 tag，但 UI 只在 MissionUpdate 时才读 tag ⇒ 必须补一次，否则 HUD 条目停在 0.3 透明、
            // 小地图图标停用"未激活"的灰色（要等下一次 UpdateTip/UpdateText 才纠正）。
            UpdateMission();
            //UpdateText("激活撤离终端", "");
        }

        /// <summary>
        /// 注入可选撤离点(次要撤离区)。
        /// <para>静态撤离在激活时会往每处各呼叫一个撤离信标，由玩家自行选择在哪一处撤离；</para>
        /// <para>动态撤离则在发起撤离时自动选取离玩家最近的一处作为实际使用的撤离点。</para>
        /// </summary>
        public void SetSecondaryZones(List<MissionEvacuateSecondary> zones)
        {
            _secondaryZones.Clear();
            if (zones != null) _secondaryZones.AddRange(zones);
        }

        /// <summary>运输机落地停稳时枢轴离地的高度(机体最低点在枢轴下约 3.3m，与 MedivacController.LandUpdate 的 4.5 同口径)。</summary>
        protected const float MedivacPivotHeight = 4.5f;

        /// <summary>
        /// 把落点的 Y 换成该处**真实地面**的高度（只改 Y，XZ 用调用方算好的位置）。
        /// <para>▍为什么不能直接用 areaPoint.y：area/areaPoint 取自任务实体，而次要撤离区的实体枢轴未必贴地
        /// （高地 / 坡边 / 悬空标记）⇒ 照它的 y 落，运输机就悬在半空（2026-11-09 反馈）。</para>
        /// <para>▍必须在**落点自身**的 XZ 上打射线：落点比 areaPoint 前后让开了几米，坡地上高度可能差很多。</para>
        /// </summary>
        protected static Vector3 ToGround(Vector3 flatPos)
        {
            const float probeUp = 50f;
            if (Physics.Raycast(flatPos + Vector3.up * probeUp, Vector3.down, out RaycastHit hit,
                    probeUp * 2f, LayerDefinition.GroundLayers, QueryTriggerInteraction.Ignore))
            {
                flatPos.y = hit.point.y;
            }
            return flatPos;
        }

        /// <summary>
        /// 创建运输机时调：算出落点(<see cref="medivacLandPos"/>)并**清掉落点周边的地表物**。
        /// <para>▍为什么要在创建时就清：树/石块是入队 + 限流提交的(树 4/s)，等降落那一刻再清会出现
        /// "石块先穿进机腹、半秒后才消失"；创建时喊一嗓子，等运输机真正压下来时场地已经干净。</para>
        /// <para>▍为什么用圆形不用矩形：落点朝向取自撤离区(随机 Y 旋转)，而 <c>ClearInRectXZ</c> 只支持
        /// 世界轴对齐矩形(见 <see cref="ModifyTerrain"/> 里的同款注释)，用矩形会在斜向点位漏清/多清。</para>
        /// </summary>
        protected Vector3 PrepareMedivacLanding(Transform ship)
        {
            float scale = ship.lossyScale.x;
            Vector3 forward = Vector3.ProjectOnPlane(area.forward, Vector3.up).normalized;
            // 尾门侧让开撤离点一段，再把高度投到该处真实地面（areaPoint 未必贴地，见 ToGround）
            medivacLandPos = ToGround(areaPoint - forward * (5.5f * scale)) + Vector3.up * (MedivacPivotHeight * scale);
            // 走"清除"语义(忽略可被摧毁白名单)：落点上的树/石块/悬崖一律让位
            TerrainClearer.ClearInRadius(medivacLandPos, MedivacClearRadius, TerrainClearTarget.All);
            return medivacLandPos;
        }

    }

}
