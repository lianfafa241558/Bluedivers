using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;

using UnityEngine;
using FPSGame.Utils;
using FPSGame.Audio;

namespace FPSGame.Gameplay
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Data;
public partial class KeyScreen
{
    private const string ErrorS = "Beacon/Beacon_Error";
    private const string ActionS = "Beacon/Beacon_Action";
    private const string FinishS = "Beacon/Beacon_Finish1";


    Dictionary<ProcedureType, (System.Action<Procedure>, System.Func<bool>)> dic;

    List<DirectionEnum> nowInput, targetInput;
    int nowSelectIndex;
    bool[] itemState;
    int artilleryProgress;
    bool artilleryComplete;
    [SerializeField]
    int[] itemValue, targetValue;

    #region 谜题随机（2026-10-06 联机改造）

    /// <summary>
    /// 本谜题的**稳定身份**：世界坐标量化（0.1m）后哈希。
    /// <para>▍为什么不用 <c>Furniture_Attached.NumberID</c>：那是各端静态自增，实例化顺序可能不同
    /// ⇒ 两端会得到不同身份。位置由场景 / 地形决定 ⇒ 跨端稳定。</para>
    /// <para>▍量化到 0.1m 是为了吃掉浮点抖动（两端同一物体的世界坐标可能有 1e-6 级差异）。</para>
    /// </summary>
    private int _puzzleId = int.MinValue;
    private int PuzzleId
    {
        get
        {
            if (_puzzleId != int.MinValue) return _puzzleId;
            Vector3 p = transform.position;
            _puzzleId = unchecked(Mathf.RoundToInt(p.x * 10f) * 73856093
                                ^ Mathf.RoundToInt(p.y * 10f) * 19349663
                                ^ Mathf.RoundToInt(p.z * 10f) * 83492791);
            return _puzzleId;
        }
    }

    /// <summary>
    /// 谜题取值（**纯函数**：同 seed + 同谜题身份 + 同 stage + 同 index ⇒ 恒等）。
    ///
    /// <para>▍为什么用纯函数而不是"一条流"：谜题阶段**可能被重试 / 重进**。若用流，
    /// "重试次数不同的两端"会拿到不同的目标值（A 端重试过一次、B 端没有 ⇒ 序列错位）。
    /// 纯函数让重试也拿到同一组值 ⇒ 两端永远一致。</para>
    ///
    /// <para>▍单机（<c>TaskState.Seed == 0</c>）保持原行为：走全局静态流、每次重试都重新随机。</para>
    /// </summary>
    private int PuzzleRange(int stage, int index, int min, int max)
    {
        if (max <= min) return min;

        int seed = TaskState.Seed;
        if (seed == 0) return RandomUtils.Range(min, max);

        int h = SeedUtil.Derive(seed, PuzzleId);
        h = SeedUtil.Derive(h, stage);
        h = SeedUtil.Derive(h, index);
        return min + (int)(unchecked((uint)h) % (uint)(max - min));
    }

    #endregion

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
            [ProcedureType.Password] = (PasswordInit, PasswordUpdate),
            [ProcedureType.Artillery] = (ArtilleryInit, ArtilleryUpdate)
        };
    }

    void PlaySound(AudioPlayInfo info) => AudioSvc.PlaySound(info);
    #region 输入
    void InputInit(Procedure now)
    {

        SetActive(inputs, true);
        targetInput = new();
        nowInput = new();
        // 谜题取值改走「本局种子 + 谜题身份 + stage + index」的纯函数（联机两端恒等；单机保持随机）：
        // 原注释"同步保证每个人一样"的**意图是对的**，但当时吃的是全局静态流 ——
        // 会被音效 / 弹孔 / 武器散布等无关系统推进游标，导致两端序列漂移。
        int len = PuzzleRange(nowStage, 0, now.minCount, now.maxCount + 1);
        //Debug.LogError("开始输入"+"长度"+len);
        for (int i = 0; i < len; ++i)
        {
            targetInput.Add((DirectionEnum)PuzzleRange(nowStage, 1 + i, 0, 4));
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
                PlayInputCilp();
                if (nowInput.Count == targetInput.Count)
                {
                    return true;
                }
            }
            else
            {
                nowInput.Clear();
                SetText(inputs, targetInput.OpterTMPString());
                PlaySound(new(ErrorS));
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
        if(string.IsNullOrEmpty(nowProcedure.tip))SetText(tip, "加载倒计时" + Tool.FloatToTime(nowProcedure.time - nowTime));
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
        GlobalEventBus.OnFurnitureOperate += OnFurnitureOperate;
    }

    bool ActionItemUpdate()
    {
        if (InputManager.GetDown(InputState.Up))
        {
            if (itemState[4])
            {
                PlaySound(new(ActionS));
                actionItem.GetChild(1).GetComponent<Animator>().SetBool("Active", true);
                return true;
            }
            else
            {
                PlaySound(new(ErrorS));
            }
        }
        return false;
    }

    void OnFurnitureOperate(GameObject user, IFurniture furniture)
    {
        bool switchState = furniture.HaveFlag(FurnitureFlag.SwitchState);
        var now = nowProcedure;
        var index = now.furns.FindIndex(item => item.NumberID == furniture.NumberID);
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
                GlobalEventBus.OnFurnitureOperate -= OnFurnitureOperate;
                PlaySound(new(FinishS));
                SetColor(actionItem.GetChild(1), _LightColor);
            }
        }
    }

    #endregion
    #region 调整参数
    void ParaModifyInit(Procedure now)
    {
        SetActive(paraModify, true);
        targetValue[0] = PuzzleRange(nowStage, 0, 0, 36000);
        targetValue[1] = PuzzleRange(nowStage, 1, 0, 10000);
        itemValue[0] = PuzzleRange(nowStage, 2, 0, 36000);
        itemValue[1] = PuzzleRange(nowStage, 3, 0, 10000);
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

        targetValue[0] = PuzzleRange(nowStage, 0, 0, 36000);
        itemValue[0] = PuzzleRange(nowStage, 1, 0, 36000);
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
        //实际上应该是控制广播
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
                PlaySound(new(FinishS));
                direction.GetChild(1).GetComponent<Animator>().SetBool("Active", true);
                return true;
            }
            else
            {
                PlaySound(new(ErrorS));
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
                PlaySound(new(ActionS));

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
            PlaySound(new(ErrorS));
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
                item.GetComponent<Animator>().SetBool("Active", false);
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
                PlayInputCilp();
                SetColor(unlock.GetChild(nowSelectIndex), Color.white);
                --nowSelectIndex;
                SetColor(unlock.GetChild(nowSelectIndex), _LightColor);
            }
            else
            {
                PlaySound(new(ErrorS));
            }
        }
        else if (InputManager.GetDown(InputState.Right))
        {
            if (nowSelectIndex < nowPro.UnlockItem.Count - 1)
            {
                PlayInputCilp();
                SetColor(unlock.GetChild(nowSelectIndex), Color.white);
                ++nowSelectIndex;
                SetColor(unlock.GetChild(nowSelectIndex), _LightColor);
            }
            else
            {
                PlaySound(new(ErrorS));
            }
        }
        else if (InputManager.GetDown(InputState.Up))
        {
            if (itemState[nowSelectIndex] == false)
            {
                PlaySound(new(ActionS));
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
                PlayInputCilp();
            }
        }
        else if (InputManager.GetDown(InputState.Down))
        {
            if (itemState[nowSelectIndex] == true)
            {
                PlayInputCilp();
                unlock.GetChild(nowSelectIndex).GetComponent<Animator>().SetBool("Active", false);
                itemState[nowSelectIndex] = false;
            }
            else
            {
                PlayInputCilp();
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
            targetValue[i] = PuzzleRange(nowStage, i, 0, 10);
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
                PlayInputCilp();
                SetColor(password.GetChild(0, nowSelectIndex, 0), Color.white);
                --nowSelectIndex;
                SetColor(password.GetChild(0, nowSelectIndex, 0), _LightColor);
                password.GetChild(1).transform.position = password.GetChild(0, nowSelectIndex).transform.position;
            }
            else
            {
                PlaySound(new(ErrorS));
            }
        }
        else if (InputManager.GetDown(InputState.Right))
        {
            if (nowSelectIndex < 4)
            {
                PlayInputCilp();
                SetColor(password.GetChild(0, nowSelectIndex, 0), Color.white);
                ++nowSelectIndex;
                SetColor(password.GetChild(0, nowSelectIndex, 0), _LightColor);
                password.GetChild(1).transform.position = password.GetChild(0, nowSelectIndex).transform.position;
            }
            else
            {
                PlaySound(new(ErrorS));
            }
        }
        else if (InputManager.GetDown(InputState.Up))
        {
            PlayInputCilp();

            itemValue[nowSelectIndex] = (itemValue[nowSelectIndex] + 1) % 10;
            SetText(password.GetChild(0, nowSelectIndex, 0), itemValue[nowSelectIndex]);
            if (itemValue[nowSelectIndex] ==targetValue[nowSelectIndex])
            {
                PlaySound(new(FinishS));
            }
            haveInput = true;
        }
        else if (InputManager.GetDown(InputState.Down))
        {
            PlayInputCilp();
            itemValue[nowSelectIndex] = (itemValue[nowSelectIndex] + 9) % 10;
            SetText(password.GetChild(0, nowSelectIndex, 0), itemValue[nowSelectIndex]);
            if (itemValue[nowSelectIndex] == targetValue[nowSelectIndex])
            {
                PlaySound(new(FinishS));
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
    #region 炮弹架
    void ArtilleryInit(Procedure now)
    {
        SetActive(artillery, true);
        artilleryProgress = 0;
        artilleryComplete = false;

        //把目标家具设为可操作
        for (int i = 0; i < now.furns.Count; ++i)
        {
            now.furns[i].canOperate = true;
        }
        //隐藏所有槽位的显示物体（第0个子物体）
        for (int i = 0; i < artillery.childCount; ++i)
        {
            SetActive(artillery.GetChild(i, 0), false);
        }
        GlobalEventBus.OnFurnitureOperate += OnArtilleryFurnitureOperate;
    }

    bool ArtilleryUpdate()
    {
        //达到5立即完成
        if (artilleryComplete)
        {
            PlaySound(new(ActionS));
            return true;
        }
        return false;
    }

    void OnArtilleryFurnitureOperate(GameObject user, IFurniture furniture)
    {
        var now = nowProcedure;
        //只响应本次阶段监听的目标家具
        if (now.type != ProcedureType.Artillery) return;
        if (now.furns.FindIndex(item => item.NumberID == furniture.NumberID) < 0) return;

        ++artilleryProgress;
        //对应位置的子物体下的第0个子物体设为显示
        var index = artilleryProgress - 1;
        if (index < artillery.childCount)
        {
            SetActive(artillery.GetChild(index, 0), true);
        }
        PlaySound(new(FinishS));

        if (artilleryProgress >= 5)
        {
            artilleryComplete = true;
            GlobalEventBus.OnFurnitureOperate -= OnArtilleryFurnitureOperate;
            SetColor(artillery.GetChild(0), _LightColor);
        }
    }
    #endregion

    private void PlayInputCilp()
    {
        PlaySound(new("Beacon/Beacon_Input" + Random.Range(1, 5)));
    }
}
}
