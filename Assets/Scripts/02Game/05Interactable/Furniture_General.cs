using System;
using System.Collections;
using System.Collections.Generic;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class Furniture_General : Furniture_Base
{

    protected FurnAction<Furniture_General> action;

    private static WndManager wndManager => WndManager.Instance;

    private static Dictionary<string, FurnAction<Furniture_General>> furnData = new Dictionary<string, FurnAction<Furniture_General>>()
    {
        /*

        ["Shower"] = new()
        {
            _Operate = (furn) =>
            {
                var emiss = furn.particle.emission;
                furn.audio.enabled = furn.inOperate = emiss.enabled = !furn.inOperate;
                furn.time = furn.cfg.audioTime;
            },
            _CanOperate = (furn,unit) =>
            {
                if (unit is PlayerController) return furn.BaseCanOp(unit);
                else return furn.inOperate && furn.BaseCanOp(unit);
            },
            _InOperateUpdate = (furn) =>
            {
                //发出声音
                if ((furn.time += Time.deltaTime) > furn.cfg.audioTime)
                {
                    furn.time = 0;
                    AudioManaqer.StartWave(new() { point = furn.Pos, range = furn.cfg.audioRange, time =furn.cfg.audioTime });
                }
            }
        },*/
        ["SelectRole"] = new()
        {
            _Operate = (furn) =>
            {
                wndManager.selectRoleWnd.SetWndState(true);
                furn.BaseOp();
            }
        },
        ["SelectTask"] = new()
        {
            _Operate = (furn) =>
            {
                wndManager.selectMapWnd.SetWndState(true);
                furn.BaseOp();
            }
        },
        ["SelectVehicle"] = new() {
            _Operate = (furn) => {
                wndManager.vehicleWnd.SetWndState(true);
                furn.BaseOp();
            }
        },
        ["KeyScreen"] = new (){
            _Operate = (Furniture_General furn) => {
                furn.gameObject.GetComponent<KeyScreen>().SetOwener(furn.inOperate || !furn.owner ? null: furn.owner.gameObject);
                furn.desc = furn.inOperate ? "激活终端" : "";
                furn.BaseOp();
                //Debug.LogError("距离"+ Vector3.Distance(furn.transform.position, furn.relatedTrans.transform.position));
            },
            _InOperateUpdate= (Furniture_General furn) => {
                var delay = Time.time - furn.lastOperatetime;

                if (Tool.In(delay, -1f, 1) && furn.owner)
                {
                    var owner = furn.owner.transform;
                    var point = furn.relatedTrans;
                    owner.rotation = Quaternion.Slerp(owner.rotation, point.rotation,Time.deltaTime*4);
                    if (Vector3.Distance(point.position,owner.position)>0.15f) {
                        if (furn.owner.TryGetComponent(out CharacterController Controller))
                        {
                            Controller.TryMove((point.position - owner.position) * Time.deltaTime * 4);
                        }
                        else
                        {
                            owner.position = Vector3.Lerp(owner.position, point.position, Time.deltaTime * 4);
                        }
                    }
                    
                }
                else if(!furn.owner||Vector3.Distance(furn.relatedTrans.position,furn.owner.transform.position)>1)//被撞飞之类的
                {
                    furn.Operate();
                }
            }
        },
        ["SignaTower"] = new() {
            _InOperateUpdate = (Furniture_General furn) => {
                if (furn.ExtFloatParameter != 0)
                {
                    furn.relatedTrans.Rotate(0, furn.ExtFloatParameter, 0, Space.World);
                    furn.ExtFloatParameter = 0;
                }
            }
        },

        ["Supply"] = new() {
            _Operate = (Furniture_General furn) => {
                furn.transform.parent.parent.GetComponent<Animator>().Play("Exit",(int)furn.ExtFloatParameter,0);
                furn.owner.GetComponent<PlayerController>().UseSupply();
                furn.BaseOp();
                if(furn.owner.TryGetComponent(out PlayerController player)) BattleManager.Instance.AddBattleDataItem(player.PlayerIndex,"使用补给次数");
            }
        },
        ["BlackBox"] = new() {
            _Operate = (Furniture_General furn) => {
                furn.BaseOp();
                furn.relatedTrans.gameObject.SetActive(true);
                //Debug.LogError("开启了黑匣子");
            }
        },
        ["PipeTarget"] = new() {
            _Start = (furn) => {
                Furniture_Pipe.targets = Furniture_Pipe.targets.FindAll(item=>item.IsValid());
                Furniture_Pipe.targets.Add(furn);
            },           
            _Operate = (Furniture_General furn) => {
                furn.BaseOp();
            }
        },


    };



    #region 实现

    public override void EndHandle()
    {
        action._EndOperate?.Invoke(this);
        base.EndHandle();
    }

    private bool BaseCanOp(GameObject unit) => base.CanOperate(unit);
    private void BaseOp() => base.Operate();
    
    protected override void Start()
    {
        base.Start();
        if(!furnData.TryGetValue(Id,out action))
        {
            action = new();
        }
        action._Start?.Invoke(this);
    }

    public override void Operate() {
        //Debug.LogWarning(cfg.furnitureId + " "+ action._Operate);
        action._Operate?.Invoke(this);
    }

    public override bool CanOperate(GameObject unit)=> action._CanOperate!=null?action._CanOperate(this,unit) : base.CanOperate(unit);

    protected override void InOperateUpdate() => action._InOperateUpdate?.Invoke(this);
    #endregion

}
