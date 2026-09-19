using System.Linq;
using FPSGame.AI;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;
namespace FpsGame.Mission
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

        /// <summary>随队撤离的凯伊(动态撤离用它身上的回收信标发起撤离)</summary>
        protected SpecUnitKei kei;

        /// <summary>快速模式下是否允许用场景中 Tag=StartPoint 的物体顶替任务实体(动态撤离不需要，见 MissionEvacuateMobile)</summary>
        protected virtual bool UseSceneStartPoint => true;

        protected override void StartMission()
        {




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

            var keiGo = ResSvc.Instance.CreatPrefab("Prefabs/BattleBase/Kei", false, point.TransformPoint(0, 0, 10));
            if (keiGo) kei = keiGo.GetComponent<SpecUnitKei>();
            medivac = ResSvc.Instance.CreatPrefab("Prefabs/BattleBase/NeoNimbus", true, point.TransformPoint(0, 12, 0)).GetComponent<MedivacController>();
            medivac.SetType(MedivacController.MedivacState.Land);
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

            RemoveTag(MissionTag.hideAll);
            AddTag(MissionTag.IsActive);
            BattleEventSub.MissionStateChange(this, true);
            BattleEventSub.MissionEnityShow(entity);
            //UpdateText("激活撤离终端", "");
        }

    }

}