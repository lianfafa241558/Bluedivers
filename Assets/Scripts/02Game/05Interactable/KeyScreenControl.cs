using System.Collections.Generic;
using System.Linq;
using Core;
using Unity.BaseTool;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;
public partial class KeyScreen
{
    Dictionary<ProcedureType, (System.Action<Procedure>, System.Func<bool>)> dic;

    List<DirectionEnum> nowInput, targetInput;
    int nowSelectIndex;
    bool[] itemState;
    [SerializeField]
    int[] itemValue, targetValue;

    void InitProcedre()
    {
        SetActive(exit, false);
        SetActive(bg, false);
        SetText(title, showTitle);
        SetText(tip, "");
        SetText(stage, (nowStage + 1) + "/" + procedure.Count);
        SetActive(end,false);
        m_anim.speed = 5 / Mathf.Max(LoadTime, 0.01f);
        itemState = new bool[5];
        itemValue = new int[5];
        targetValue = new int[5];

        dic = new() {
            [ProcedureType.Input] = (InputInit, InputUpdate),
            [ProcedureType.Load] = (LoadInit, LoadUpdate),
            [ProcedureType.Wait] = (WaitInit, WaitUpdate),
            [ProcedureType.ActionItem] = (ActionItemInit, ActionItemUpdate),
            [ProcedureType.ParaModify] = (ParaModifyInit, ParaModifyUpdate),
            [ProcedureType.Direction] = (DirectionInit, DirectionUpdate),
            [ProcedureType.Unlock] = (UnlockInit, UnlockUpdate),
            [ProcedureType.Password] = (PasswordInit, PasswordUpdate)
        };
    }

    void PlaySound(AudioPlayInfo info) => AudioManager.PlaySound(info);
    #region 输入
    void InputInit(Procedure now)
    {
        SetActive(inputs, true);
        targetInput = new();
        nowInput = new();
        //每次长度一样是因为时间种子的问题
        int len = RandomUtils.Range(now.minCount, now.maxCount + 1);
        //Debug.LogError("开始输入"+"长度"+len);
        for (int i = 0; i < len; ++i)//同步保证每个人一样
        {
            targetInput.Add((DirectionEnum)RandomUtils.Range(0, 4));
        }
        SetText(inputs, targetInput.OpterTMPString());
    }
    bool InputUpdate()
    {
        bool haveInput = false;
        if (InputManager.GetDown(InputState.Left))
        {
            haveInput = true;
            nowInput.Add(DirectionEnum.Left);
        }
        else if (InputManager.GetDown(InputState.Right))
        {
            haveInput = true;
            nowInput.Add(DirectionEnum.Right);
        }
        else if (InputManager.GetDown(InputState.Up))
        {
            haveInput = true;
            nowInput.Add(DirectionEnum.Up);
        }
        else if (InputManager.GetDown(InputState.Down))
        {
            haveInput = true;
            nowInput.Add(DirectionEnum.Down);
        }
        if (haveInput)
        {
            if (targetInput.Compare(nowInput))
            {
                //更新文本
                SetText(inputs, targetInput.OpterColorString(nowInput.Count - 1, _LightColor, _LightColor, Color.white));
                PlaySound(new("AirDrop/superbeacon_button"));
                if (nowInput.Count == targetInput.Count)
                {
                    return true;
                }
            }
            else
            {
                nowInput.Clear();
                SetText(inputs, targetInput.OpterTMPString());
                PlaySound(new("AirDrop/superbeacon_throw"));
            }

        }
        return false;
    }
    #endregion
    #region 加载
    void LoadInit(Procedure now)
    {
        SetActive(load, true);
        SetColor(load.Find("Image"),_LightColor);
        for (int i = 0; i < now.furns.Count; ++i)
        {
            var anim = now.furns[i].GetComponent<Animator>();
            anim.enabled = true;
            anim.SetFloat("Speed",10/now.time);
            if (i< now.UnlockItem.Count&&!string.IsNullOrEmpty(now.UnlockItem[i])) anim.Play(now.UnlockItem[i]);
        }
        if (now.eject&&owner)
        {
            furn.Operate();
        }
    }

    bool LoadUpdate()
    {
        var nowTime = Time.time - lastStageTime;
        if(string.IsNullOrEmpty(nowProcedure.tip))SetText(tip, "加载倒计时:" + Tool.FloatToTime(nowProcedure.time - nowTime));
        SetFill(load.GetChild(0), nowTime / nowProcedure.time);
        SetText(load.GetChild(1), Mathf.Round(nowTime / nowProcedure.time * 100) + "%");
        return nowTime > nowProcedure.time;
    }
    #endregion
    #region 等待
    void WaitInit(Procedure now)
    {
        SetActive(wait, true);
        furn.canOperate = false;
        furn.Operate();
    }

    bool WaitUpdate()
    {
        var nowTime = Time.time - lastStageTime;
        SetText(wait, Tool.FloatToTime(nowProcedure.time - nowTime));
        return nowTime > nowProcedure.time;
    }
    #endregion
    #region 开启物体
    void ActionItemInit(Procedure now)
    {
        SetActive(actionItem, true);
        for (int i = 0; i < 4; ++i)//最多4个
        {
            var item = actionItem.GetChild(0,i);
            if (i < now.furns.Count)
            {
                SetActive(item, true);
                SetText(item.GetChild(1), now.UnlockItem[i]);
                SetColor(item, new(1,0.2f,0.2f));
                now.furns[i].canOperate = true;
                itemState[i] = false;
            }
            else
            {
                SetActive(item, false);
            }
        }
        itemState[4] = false;
        GlobalEventManager.OnFurnitureOperate += OnFurnitureOperate;
    }

    bool ActionItemUpdate()
    {
        if (InputManager.GetDown(InputState.Up))
        {
            if (itemState[4])
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                actionItem.GetChild(1).GetComponent<Animator>().SetBool("Active", true);
                return true;
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        return false;
    }

    void OnFurnitureOperate(GameObject user, Furniture_Base furniture)
    {
        bool switchState = furniture.HaveFlag(FurnitureFlag.SwitchState);
        var now = nowProcedure;
        var index = now.furns.FindIndex(item => item == furniture);
        if (index > -1)
        {
            itemState[index] = true;
            SetColor(actionItem.GetChild(0, index), new(0.35f, 1, 0.35f));
            bool complete=true;
            for (int i = 0; i < now.furns.Count; ++i)//最多4个
            {
                if (itemState[i] == false)
                {
                    complete = false;
                    break;
                }
            }
            if (complete)
            {
                itemState[4] = true;
                GlobalEventManager.OnFurnitureOperate -= OnFurnitureOperate;
                PlaySound(new("UI/UI_Reward2"));
                SetColor(actionItem.GetChild(1), _LightColor);
            }
        }
    }

    #endregion
    #region 调整参数
    void ParaModifyInit(Procedure now)
    {
        SetActive(paraModify, true);
        targetValue[0] = RandomUtils.Range(0, 36000);
        targetValue[1] = RandomUtils.Range(0, 10000);
        itemValue[0] = RandomUtils.Range(0, 36000);
        itemValue[1] = RandomUtils.Range(0, 10000);
        itemValue[2] = CalculatedPower();

        SetText(paraModify.GetChild(2, 1), itemValue[0] / 100);
        paraModify.GetChild(2, 0).transform.localEulerAngles = new(0, 0, itemValue[0] / 100f);

        SetFill(paraModify.GetChild(3, 0), itemValue[1] / 10000f);
        ((RectTransform)paraModify.GetChild(3, 1)).anchoredPosition = new(25.2f * itemValue[1] / 10000f - 1.2f, 1);

        SetText(paraModify.GetChild(1, 0), itemValue[2] + "%");

        var baseHeight = itemValue[2] * 0.01f;
        for (int i = 0; i < paraModify.GetChild(4).childCount; ++i)
        {
            var item = paraModify.GetChild(4, i);
            bool main = i / 3 == 1;
            if (main) SetSizeDelta(item, 2, 2 + 14 * baseHeight);
            else SetSizeDelta(item, 2,16 - 14 * Mathf.Clamp01(2 - 2 * baseHeight));
        }

    }

    bool ParaModifyUpdate()
    {
        //高度16
        var baseHeight = itemValue[2] * 0.01f;
        for (int i=0; i< paraModify.GetChild(4).childCount; ++i)
        {
            var item = paraModify.GetChild(4, i);
            var nowHeight = GetSizeDelta(item).y;
            bool main = i / 3 == 1;
            var finalHelght =Mathf.Clamp01(baseHeight + (main?0.15f:0.1f) * Mathf.Sin(4*Time.time+i));

            if (main) SetSizeDelta(item, 2,Mathf.Lerp(nowHeight,2 + 14 * finalHelght,Time.deltaTime*3));
            else SetSizeDelta(item, 2, Mathf.Lerp(nowHeight, 16 - 14 * Mathf.Clamp01(2 - 2* finalHelght), Time.deltaTime*3));
        }

        if (!owner) return false;
        bool haveInput1=false, haveInput2 = false;
        if (InputManager.Get(InputState.Left))
        {
            itemValue[0] = (int)(itemValue[0] + 2500 * Time.deltaTime) % 36000;
            haveInput1 = true;
        }
        else if (InputManager.Get(InputState.Right))
        {
            itemValue[0] = (int)(itemValue[0] + 36000 - 2500 * Time.deltaTime) % 36000;
            haveInput1 = true;
        }
        else if (InputManager.Get(InputState.Up))
        {
            itemValue[1] = Mathf.CeilToInt(Mathf.Clamp(itemValue[1] + 2000 * Time.deltaTime,0,10000));
            haveInput2 = true;
        }
        else if (InputManager.Get(InputState.Down))
        {
            itemValue[1] = Mathf.CeilToInt(Mathf.Clamp(itemValue[1] - 2000 * Time.deltaTime, 0, 10000));
            haveInput2 = true;
        }
        if (haveInput1|| haveInput2)
        {
            if (haveInput1)
            {
                SetText(paraModify.GetChild(2, 1), itemValue[0] / 100);
                paraModify.GetChild(2, 0).transform.localEulerAngles=new(0,0, itemValue[0] / 100f);
            }
            if (haveInput2)
            {
                SetFill(paraModify.GetChild(3, 0), itemValue[1] / 10000f);
                paraModify.GetRectChild(3, 1).anchoredPosition = new(25.2f * itemValue[1] / 10000f-1.2f, 1);
            }

            var re = itemValue[2] = CalculatedPower();
            SetText(paraModify.GetChild(1, 0), re + "%");
            return re == 100;
        }

        return false;
    }

    int CalculatedPower()
    {
        var angle = Mathf.Abs(Mathf.DeltaAngle(itemValue[0]/100f, targetValue[0] / 100f))/180f;//0-1
        var height =Mathf.Abs(itemValue[1] - targetValue[1])/10000f;//0-1
        return Mathf.RoundToInt(100f * Mathf.Clamp01(1.05f - height) * Mathf.Clamp01(1.05f - angle));
    }

    #endregion
    #region 方向
    void DirectionInit(Procedure now)
    {
        SetActive(direction, true);
        SetColor(direction.GetChild(0,2), _LightColor);

        targetValue[0] = RandomUtils.Range(0, 36000);
        itemValue[0] = RandomUtils.Range(0, 36000);
        itemState[0] = false;
        direction.GetChild(0, 0).transform.localEulerAngles = new(0, 0, targetValue[0]/100f - 22.5f);
        direction.GetChild(0, 1).transform.localEulerAngles = new(0, 0, targetValue[0]/100f + 22.5f);
        direction.GetChild(1).GetComponent<Animator>().SetBool("Active", false);

        now.furns[0].inOperate = true;
        var anim = now.furns[0].GetComponent<Animator>();
        anim.enabled = false;
        CalibrationDirection();
    }

    bool DirectionUpdate()
    {
        //实际上应该是控制广播塔
        if (InputManager.Get(InputState.Left))
        {
            //Debug.LogError("按住左键"+ (int)(itemValue[0] + 2000 * Time.deltaTime) % 36000);
            itemValue[0] = (int)(itemValue[0] + 1000 * Time.deltaTime) % 36000;
            nowProcedure.furns[0].ExtFloatParameter+=10 * Time.deltaTime;
            CalibrationDirection();
        }
        else if (InputManager.Get(InputState.Right))
        {
            itemValue[0] = (int)(itemValue[0] + 36000 - 1000*Time.deltaTime) % 36000;
            nowProcedure.furns[0].ExtFloatParameter -= 10 * Time.deltaTime;
            CalibrationDirection();
        }
        else if (InputManager.GetDown(InputState.Up))
        {
            if (itemState[0])
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                direction.GetChild(1).GetComponent<Animator>().SetBool("Active", true);
                return true;
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        return false;
    }
    private void CalibrationDirection()
    {
        direction.GetChild(0, 2).transform.localEulerAngles = new(0, 0, itemValue[0]/100f);
        var angle = Mathf.Abs(Mathf.DeltaAngle(itemValue[0]/100f, targetValue[0]/100f));
        if (angle <= 22.5f)
        {
            if (!itemState[0])
            {
                itemState[0] = true;
                Color green = new(0.35f, 1, 0.35f);
                SetColor(direction.GetChild(0), green);
                SetColor(direction.GetChild(0, 0), green);
                SetColor(direction.GetChild(0, 1), green);
                SetColor(direction.GetChild(0, 3), green);
                SetColor(direction.GetChild(1), _LightColor);
                PlaySound(new("UI/UI_Reward2"));

            }

        }
        else if (itemState[0])
        {
            itemState[0] = false;
            SetColor(direction.GetChild(0), Color.white);
            SetColor(direction.GetChild(0, 0), Color.white);
            SetColor(direction.GetChild(0, 1), Color.white);
            SetColor(direction.GetChild(0, 3), new(1, 0.2f, 0.2f));
            SetColor(direction.GetChild(1), Color.white);
            PlaySound(new("UI/PerformanceLevelNewRewardUnlockedA_1"));
        }
    }


    #endregion
    #region 解锁
    void UnlockInit(Procedure now)
    {
        SetActive(unlock, true);
        nowSelectIndex = 0;
        for (int i = 0; i < 5; ++i)
        {
            var item = unlock.GetChild(i);
            if (i < now.UnlockItem.Count)
            {
                SetActive(item, true);
                SetText(item.GetChild(3), now.UnlockItem[i]);
                SetColor(item, i == 0 ? _LightColor : Color.white);
                itemState[i] = false;
            }
            else
            {
                SetActive(item, false);
            }
        }
    }

    bool UnlockUpdate()
    {
        var nowPro = nowProcedure;
        if (InputManager.GetDown(InputState.Left))
        {
            if (nowSelectIndex > 0)
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                SetColor(unlock.GetChild(nowSelectIndex), Color.white);
                --nowSelectIndex;
                SetColor(unlock.GetChild(nowSelectIndex), _LightColor);
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        else if (InputManager.GetDown(InputState.Right))
        {
            if (nowSelectIndex < nowPro.UnlockItem.Count - 1)
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                SetColor(unlock.GetChild(nowSelectIndex), Color.white);
                ++nowSelectIndex;
                SetColor(unlock.GetChild(nowSelectIndex), _LightColor);
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        else if (InputManager.GetDown(InputState.Up))
        {
            if (itemState[nowSelectIndex] == false)
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                unlock.GetChild(nowSelectIndex).GetComponent<Animator>().SetBool("Active", true);
                itemState[nowSelectIndex] = true;
                int count = 0;
                //检测是否完成
                for (int i = 0; i < nowPro.UnlockItem.Count; ++i)
                {
                    if (itemState[i])
                    {
                        ++count;
                    }
                }
                if (count == nowPro.UnlockItem.Count)
                {
                    return true;
                }
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        else if (InputManager.GetDown(InputState.Down))
        {
            if (itemState[nowSelectIndex] == true)
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                unlock.GetChild(nowSelectIndex).GetComponent<Animator>().SetBool("Active", false);
                itemState[nowSelectIndex] = false;
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }


        return false;
    }
    #endregion
    #region 密码
    void PasswordInit(Procedure now)
    {
        SetActive(password, true);
        nowSelectIndex = 0;
        for (int i = 0; i < 5; ++i)
        {
            var item = password.GetChild(0, i);
            SetColor(item.GetChild(0), i == 0 ? _LightColor : Color.white);
            targetValue[i] = RandomUtils.Range(0, 10);
            itemValue[i] = 0;
            SetText(item.GetChild(0), 0);
        }
        //password.GetChild(1).transform.position = password.GetChild(0, 0).transform.position;
    }

    bool PasswordUpdate()
    {
        bool haveInput = false;
        if (InputManager.GetDown(InputState.Left))
        {
            if (nowSelectIndex > 0)
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                SetColor(password.GetChild(0, nowSelectIndex, 0), Color.white);
                --nowSelectIndex;
                SetColor(password.GetChild(0, nowSelectIndex, 0), _LightColor);
                password.GetChild(1).transform.position = password.GetChild(0, nowSelectIndex).transform.position;
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        else if (InputManager.GetDown(InputState.Right))
        {
            if (nowSelectIndex < 4)
            {
                PlaySound(new("AirDrop/superbeacon_button"));
                SetColor(password.GetChild(0, nowSelectIndex, 0), Color.white);
                ++nowSelectIndex;
                SetColor(password.GetChild(0, nowSelectIndex, 0), _LightColor);
                password.GetChild(1).transform.position = password.GetChild(0, nowSelectIndex).transform.position;
            }
            else
            {
                PlaySound(new("AirDrop/superbeacon_throw"));
            }
        }
        else if (InputManager.GetDown(InputState.Up))
        {
            PlaySound(new("AirDrop/superbeacon_button"));
            itemValue[nowSelectIndex] = (itemValue[nowSelectIndex] + 1) % 10;
            SetText(password.GetChild(0, nowSelectIndex, 0), itemValue[nowSelectIndex]);
            if (itemValue[nowSelectIndex] ==targetValue[nowSelectIndex])
            {
                PlaySound(new("UI/UI_ElementsA"));
            }
            haveInput = true;
        }
        else if (InputManager.GetDown(InputState.Down))
        {
            PlaySound(new("AirDrop/superbeacon_button"));
            itemValue[nowSelectIndex] = (itemValue[nowSelectIndex] + 9) % 10;
            SetText(password.GetChild(0, nowSelectIndex, 0), itemValue[nowSelectIndex]);
            if (itemValue[nowSelectIndex] == targetValue[nowSelectIndex])
            {
                PlaySound(new("UI/UI_ElementsA"));
            }
            haveInput = true;
        }
        if (haveInput)
        {
            return itemValue.Zip(targetValue, (i, t) => i == t).All(b => b);
        }
        return false;
    }

    #endregion

}
