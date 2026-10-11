using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Audio;

namespace FPSGame.Gameplay
{
using static FPSGame.WndTools.WndRootTool;
using System;
using System.Linq;
using UnityEngine.Events;

using FPSGame.Core.Interface;   // IsValidMono（接口引用判空）
using FPSGame.Game;          // ActorsManager（驱动端判定要认"本机玩家"）
using FPSGame.Utils;
using FPSGame.Attributes;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 按键序列终端（KeyScreen 及其分部）。
/// </summary>
[AddComponentMenu("交互/控制面板终端")]
public partial class KeyScreen : MonoBehaviour
{
    public event UnityAction<int> OnUpdateStage;
    public UnityEvent OnComple;

    /// <summary>成员端 = true：终端阶段**不自行推进**，只应用驱动端下发的阶段（同 MissionBase.RemoteDriven 口径）。
    /// <para>由 09 侧 NetKeyScreenBridge 在 Install / 房主掉线时置位复位。</para></summary>
    public static bool RemoteDriven;

    /// <summary>本端推进了一个阶段（**只由驱动端抛**）⇒ 09 侧 NetKeyScreenBridge 订阅后上报。
    /// <para>参数：(终端家具 SyncId, 新阶段)。SyncId = 0 表示本端终端没有身份源 ⇒ 不参与同步。</para></summary>
    public static event Action<int, int> OnLocalStageChanged;

    /// <summary>本端读到了一次"玩家对终端的输入"（**只由驱动端读本机键盘时抛**）⇒ 09 侧桥上报。
    /// <para>参数：(终端 SyncId, <c>InputState</c> 的 int, 类型)。类型：0 = 本帧按下、1 = 按住开始、2 = 松开。</para></summary>
    public static event Action<int, int, int> OnLocalInput;

    /// <summary>远端玩家的"按住"状态（非驱动端用它代替本机键盘）。</summary>
    private static readonly HashSet<InputState> _remoteHeld = new HashSet<InputState>();

    /// <summary>远端玩家"本帧按下"的键（<see cref="LateUpdate"/> 里清空；非驱动端拿它当 <c>GetDown</c>）。</summary>
    private static readonly HashSet<InputState> _remoteDown = new HashSet<InputState>();

    /// <summary>本机"按住"缓存：识别"按住 / 松开"的边沿（只在驱动端有意义）。</summary>
    private static readonly HashSet<InputState> _localHeld = new HashSet<InputState>();

    /// <summary>非驱动端 = 成员端且"操作者不是我" ⇒ 只复现远端输入、不吃本机键盘
    /// <para>（否则两个人同时按会互相干扰）。单机 / 驱动端恒为 false ⇒ 行为与改造前一致。</para></summary>
    private bool UseRemoteInput => RemoteDriven && !IsStageDriver();

    /// <summary>持续按住（对应 <c>InputManager.Get</c>）。</summary>
    protected bool InputGet(InputState key)
    {
        if (UseRemoteInput) return _remoteHeld.Contains(key);

        bool now = InputManager.Get(key);
        bool was = _localHeld.Contains(key);
        if (now != was)
        {
            if (now) _localHeld.Add(key);
            else _localHeld.Remove(key);
            OnLocalInput?.Invoke(SyncId, (int)key, now ? 1 : 2);
        }
        return now;
    }

    /// <summary>本帧按下（对应 <c>InputManager.GetDown</c>）。</summary>
    protected bool InputGetDown(InputState key)
    {
        if (UseRemoteInput) return _remoteDown.Contains(key);

        bool now = InputManager.GetDown(key);
        if (now) OnLocalInput?.Invoke(SyncId, (int)key, 0);
        return now;
    }

    /// <summary>【联机】应用远端玩家的一次输入（由 09 侧 NetKeyScreenBridge 调用）。</summary>
    public void ApplyRemoteInput(int key, int kind)
    {
        var state = (InputState)key;
        switch (kind)
        {
            case 0: _remoteDown.Add(state); break;
            case 1: _remoteHeld.Add(state); _remoteDown.Add(state); break;   // 按下那一帧同时也是"本帧按下"
            case 2: _remoteHeld.Remove(state); break;
        }
    }

    /// <summary>清掉远端输入（换阶段 / 应用远端阶段时）：避免上一阶段的按键残留把下一阶段推走。</summary>
    private void ClearRemoteInput()
    {
        _remoteHeld.Clear();
        _remoteDown.Clear();
    }

    private void LateUpdate()
    {
        // 远端"本帧按下"只对下一帧的 Update 有效（同 InputManager.GetDown 的语义）
        if (_remoteDown.Count > 0) _remoteDown.Clear();
    }

    [SerializeField]
    private Color _LightColor = new(0, 0.627f, 1f);
    private const float _SwitchTime=0.5f;
    
    public List<GameObject> procedurePrefabs;


    public float LoadTime=5;
    public string showTitle;
    public List<Procedure> procedure;
    [SerializeField]
    Transform bg;
    Transform title, tip, stage,exit,inputs,load,wait, actionItem, paraModify, direction, unlock, password,end, artillery;
    [DisplayField]
    public Furniture_General furn;

    [DisplayField]
    [SerializeField]
    private int nowStage=-1;
    [DisplayField]
    public GameObject owner;
    [SerializeField]
    private Animator m_anim;
    private float lastStageTime,trySwitchTime;

    public bool IsActive => nowStage >= 0&& nowStage < procedure.Count;
    public bool IsEnd => nowStage >= procedure.Count;
    public Procedure nowProcedure => procedure[nowStage];

    private void Start()
    {
        furn = GetComponent<Furniture_General>();
        var list = procedure.Select(item => (int)item.type).Distinct().ToList();
        for (int i=0;i< list.Count;++i)
        {
            var tmp = procedurePrefabs[list[i]];
            var go = Instantiate(tmp,bg);
            go.name = tmp.name;
        }

        title = bg.Find("Title");
        tip = bg.Find("Tip");
        stage = bg.Find("Stage");
        inputs = bg.Find("Inputs");
        exit = bg.Find("Exit");
        load = bg.Find("Load");
        wait = bg.Find("Wait");
        actionItem = bg.Find("ActionItem");
        paraModify = bg.Find("ParaModify");
        direction = bg.Find("Direction");
        unlock = bg.Find("Unlock");
        password = bg.Find("Password");
        end = bg.Find("End");
        artillery = bg.Find("Artillery");

        InitProcedre();
    }

    public void SetOwener(GameObject owner)
    {
        // 联机诊断：终端有没有被"开起来"（远端重放家具交互时会走到这里）——两端各看一遍即可判断
        FPSGame.Utils.NetSyncLog.SyncLog("终端/开关", $"{name} SetOwener({(owner != null ? owner.name : "null")})");
        this.owner = owner;
        if (owner.IsValid())
        {
            //Debug.LogError("开始操作"+ owner);
            if (!m_anim.enabled)
            {
                m_anim.enabled = true;
                if (LoadTime <= 1)
                {
                    m_anim.Play("Init", 0, 1);
                }
                else
                {

                    AudioSvc.PlaySound(new("Beacon/Beacon_StartLoad"));
                }
                FPSGame.Core.TimerHost.CreateTimer(() => SetStage(0), LoadTime);
                //Debug.LogError("开始操作");
            }
        }
        SetActive(exit, owner.IsValid());

    }


    /// <summary>本终端家具的跨端稳定键（= <c>Furniture_Attached.SyncId</c>；没有身份源时 0 = 不参与同步）。</summary>
    public int SyncId => furn ? furn.SyncId : 0;

    /// <summary>
    /// 本端是不是"阶段驱动端"：有人操作 ⇒ 只有操作者所在那台驱动；无人操作（Load/Wait 这类自动阶段）⇒ 房主驱动。
    /// <para>▍单机（<see cref="RemoteDriven"/> = false）恒为 true ⇒ 行为与改造前完全一致。</para>
    /// <para>⚠ 非驱动端调用 <see cref="SetStage"/> 会被丢弃（等同步过来）：两端各推各的必然分叉。</para>
    /// </summary>
    private bool IsStageDriver()
    {
        if (owner != null)
        {
            var self = ActorsManager.Player;
            return self.IsValidMono() && self.gameObject == owner;
        }
        return !RemoteDriven;   // 无人操作：房主驱动（单机时 RemoteDriven 也是 false）
    }

    /// <summary>推进阶段（**驱动端**的唯一入口）：非驱动端调用会被丢弃。</summary>
    public void SetStage(int stage)
    {
        if (this == null) return;
        if (!IsStageDriver()) return;

        int syncId = SyncId;
        SetStageCore(stage);
        OnLocalStageChanged?.Invoke(syncId, stage);
    }

    /// <summary>【联机】应用远端下发的阶段：跳过驱动门（远端已判定过），且**不抛上报事件** ⇒ 不回环。</summary>
    public void ApplyRemoteStage(int stage)
    {
        if (this == null) return;
        if (stage == nowStage) return;      // 已经在这一阶段：幂等
        ClearRemoteInput();                 // 换阶段：清掉上一阶段残留的远端按键
        SetStageCore(stage);
    }

    /// <summary>阶段推进的实际逻辑（设值 + 派发 <see cref="OnUpdateStage"/> + 阶段 UI 初始化）。</summary>
    private void SetStageCore(int stage)
    {
        nowStage = stage;
        // 联机诊断：阶段推进的同步 = 驱动端上报、其余端应用（见 09 侧 NetKeyScreenBridge 与 IsStageDriver）。
        // 本行两端都会打 ⇒ 对照两端 "→ s/N" 就能看出是在自己推（驱动端）还是收来的（应用端）。
        FPSGame.Utils.NetSyncLog.SyncLog("终端/阶段", $"{name} → {stage}/{procedure.Count}（owner={(owner != null ? owner.name : "null")}）");
        OnUpdateStage?.Invoke(stage);
        lastStageTime = Time.time;

        //有概率被OnUpdateStage事件修改
        if (stage != nowStage) return;
        if (stage < procedure.Count)
        {
            SetText(this.stage, (stage + 1) + "/" + procedure.Count);
            AudioSvc.PlaySound(new("Beacon/Beacon_Finish1"));
            var now = nowProcedure;
            SetActive(false, inputs, load, wait, actionItem, paraModify, direction, unlock, password, end, artillery);
            SetText(tip, now.tip);
            furn.canOperate = true;
            dic[now.type].Item1.Invoke(now);
        }
        else//完成
        {
            AudioSvc.PlaySound(new("Beacon/Beacon_Finish2"));
            OnComple?.Invoke();
            SetActive(false, inputs, load, wait, actionItem, paraModify, direction, unlock, password, artillery);
            SetActive(end);
            SetText(title, "所有系统正常运转");
            SetText(tip,"感谢您的配合");
            //furn.canOperate = false;
            if(owner) furn.Operate();
            furn.canOperate = false;
        }


    }


    private void Update()
    {
        if (!FPSGame.Utils.Tool.In(nowStage,-1,procedure.Count)) return;
        // ⚠ 非驱动端**也要跑这一段**（它要复现远端输入来刷新本地显示），但输入来源已经换成远端
        //   （见 InputGet / InputGetDown）⇒ 它不会推进阶段（SetStage 里还有驱动门兜底）。
        var nowPro = nowProcedure;
        if (trySwitchTime >0)
        {
            if(Time.time - trySwitchTime > _SwitchTime)
            {
                SetStage(nowStage + 1);
                trySwitchTime = 0;
            }
            return;
        }
        if (!owner && !AllowUnmanned(nowPro.type)) return;
        bool next = dic[nowPro.type].Item2.Invoke();
        if (next) trySwitchTime = Time.time;
    }



    bool AllowUnmanned(ProcedureType type)
    {
        if (type == ProcedureType.Load || type == ProcedureType.Wait || type == ProcedureType.ParaModify || type == ProcedureType.Artillery) return true;
        return false;
    }

    public void AddTime(float time)
    {
        lastStageTime += time;
    }

    public float GetTime()=> Time.time - lastStageTime;


    [System.Serializable]
    public class Procedure{
        [InspectorName("类型")]
        public ProcedureType type;
        public string tip;
        public bool eject;//是否弹出玩家 加载/等待

        public int minCount,maxCount;//输入
        public float time;//加载/等待
        public List<Furniture_Attached> furns;//操作特定物体
        public List<string> UnlockItem;//解除锁定

    }
    public enum ProcedureType
    {
        /// <summary>输入</summary>
        [InspectorName("输入")]
        Input,
        /// <summary>加载</summary>
        [InspectorName("加载")]
        Load,
        /// <summary>等待</summary>
        [InspectorName("等待")]
        Wait,
        /// <summary>操作特定物体</summary>
        [InspectorName("操作特定物体")]
        ActionItem,
        /// <summary>调整系数</summary>
        [InspectorName("调整系数")]
        ParaModify,
        /// <summary>旋转方向</summary>
        [InspectorName("旋转方向")]
        Direction,
        /// <summary>解除锁定</summary>
        [InspectorName("解除锁定")]
        Unlock,
        /// <summary>密码</summary>
        [InspectorName("密码")]
        Password,
        /// <summary>炮弹架</summary>
        [InspectorName("炮弹架")]
        Artillery,
    }
}
#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(KeyScreen.Procedure))]
public class ProcedureEditor : PropertyDrawer
{
    private const float lineHeight = 20f;
    private const float lineIntervalAndHeight = 22f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var typeProp = property.FindPropertyRelative("type");
        var typeValue = (KeyScreen.ProcedureType)typeProp.enumValueIndex;

        // 绘制折叠箭头和标志
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, lineHeight),
            property.isExpanded, label);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        // 计算起始Y位置
        float y = position.y + lineHeight;

        float LabelWidth = EditorGUIUtility.labelWidth;

        EditorGUIUtility.labelWidth = position.width * 0.3f;

        // 绘制公共属性
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("type"));
        y += lineIntervalAndHeight;
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("tip"));
        y += lineIntervalAndHeight;

        // 根据类型显示不同字段
        switch (typeValue)
        {
            case KeyScreen.ProcedureType.Input:
                DrawInputProperties(position, property, ref y);
                break;

            case KeyScreen.ProcedureType.Load:
                DrawLoadProperties(position, property, ref y);
                break;
            case KeyScreen.ProcedureType.Wait:
                DrawWaitProperties(position, property, ref y);
                break;
            case KeyScreen.ProcedureType.ActionItem:
                DrawActionItemProperties(position, property, ref y);
                break;
            case KeyScreen.ProcedureType.ParaModify:

                break;
            case KeyScreen.ProcedureType.Direction:
                DrawDirectionProperties(position, property, ref y);
                break;
            case KeyScreen.ProcedureType.Unlock:
                DrawUnlockProperties(position, property, ref y);
                break;
            case KeyScreen.ProcedureType.Password:

                break;
            case KeyScreen.ProcedureType.Artillery:
                DrawArtilleryProperties(position, property, ref y);
                break;
        }

        EditorGUIUtility.labelWidth = LabelWidth;
        EditorGUI.EndProperty();
    }

    private void DrawInputProperties(Rect position, SerializedProperty property, ref float y)
    {
        EditorGUI.PropertyField(
            new Rect(position.x + 0, y, (position.width) / 2 - 0, lineHeight),
            property.FindPropertyRelative("minCount"),new GUIContent("最小数量"));
        EditorGUI.PropertyField(
            new Rect(position.x + 0 + (position.width) / 2 + 20, y, (position.width) / 2 - 20, lineHeight),
            property.FindPropertyRelative("maxCount"), new GUIContent("最大数量"));
        y += lineIntervalAndHeight;


    }
    private void DrawLoadProperties(Rect position, SerializedProperty property, ref float y)
    {
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("time"));
        y += lineIntervalAndHeight;
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("eject"), new GUIContent("打断交互"));
        y += lineIntervalAndHeight;
        
        var furns = property.FindPropertyRelative("furns");
        float height = lineIntervalAndHeight * (furns.isExpanded ? Mathf.Max(furns.arraySize, 1) + 3 : 1);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, height),
            furns, new GUIContent("开启物体"));
        y += height;
        var UnlockItem = property.FindPropertyRelative("UnlockItem");
        height = lineIntervalAndHeight * (UnlockItem.isExpanded ? Mathf.Max(UnlockItem.arraySize, 1) + 3 : 1);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, height),
            UnlockItem, new GUIContent("播放动画"));
        y += height;

        
    }
    private void DrawWaitProperties(Rect position, SerializedProperty property, ref float y)
    {
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("time"));
        y += lineIntervalAndHeight;
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("eject"));
        y += lineIntervalAndHeight;
    }

    private void DrawUnlockProperties(Rect position, SerializedProperty property, ref float y)
    {
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, lineHeight),
            property.FindPropertyRelative("UnlockItem"));
        y += lineIntervalAndHeight;
    }
    private void DrawActionItemProperties(Rect position, SerializedProperty property, ref float y)
    {
        var furns = property.FindPropertyRelative("furns");
        float height = lineIntervalAndHeight * (furns.isExpanded ? Mathf.Max(furns.arraySize, 1) + 3 : 1);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, height),
            furns, new GUIContent("对应物体"));
        y += height;
        var UnlockItem = property.FindPropertyRelative("UnlockItem");
        height = lineIntervalAndHeight * (UnlockItem.isExpanded ? Mathf.Max(UnlockItem.arraySize, 1) + 3 : 1);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, height),
            UnlockItem, new GUIContent("物体名称"));
        y += height;
    }


    private void DrawDirectionProperties(Rect position, SerializedProperty property, ref float y)
    {
        var furns = property.FindPropertyRelative("furns");
        float height = lineIntervalAndHeight * (furns.isExpanded ? Mathf.Max(furns.arraySize, 1)+3 : 1);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, height),
            furns, new GUIContent("对应物体"));
        y += height;
    }

    private void DrawArtilleryProperties(Rect position, SerializedProperty property, ref float y)
    {
        var furns = property.FindPropertyRelative("furns");
        float height = lineIntervalAndHeight * (furns.isExpanded ? Mathf.Max(furns.arraySize, 1) + 3 : 1);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, height),
            furns, new GUIContent("监听物体"));
        y += height;
    }


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return lineIntervalAndHeight;

        var typeProp = property.FindPropertyRelative("type");
        int lineCount = 2; // 基础属性 occasion+material+type)
        switch ((KeyScreen.ProcedureType)typeProp.enumValueIndex)
        {
            case KeyScreen.ProcedureType.Input:
                lineCount += 1;
                break;
            case KeyScreen.ProcedureType.Load:
                lineCount += 4;
                var furns2 = property.FindPropertyRelative("furns");
                lineCount += furns2.isExpanded ? Mathf.Max(furns2.arraySize, 1) + 2 : 0;
                var UnlockItem3 = property.FindPropertyRelative("UnlockItem");
                lineCount += UnlockItem3.isExpanded ? Mathf.Max(UnlockItem3.arraySize, 1) + 2 : 0;
                break;
            case KeyScreen.ProcedureType.Wait:
                lineCount += 2;
                break;
            case KeyScreen.ProcedureType.ActionItem:
                lineCount += 2;
                var furns = property.FindPropertyRelative("furns");
                lineCount += furns.isExpanded ? Mathf.Max(furns.arraySize,1)+2 : 0;
                var UnlockItem2 = property.FindPropertyRelative("UnlockItem");
                lineCount += UnlockItem2.isExpanded ? Mathf.Max(UnlockItem2.arraySize, 1)+2 : 0;
                break;
            case KeyScreen.ProcedureType.ParaModify:
                lineCount += 0;
                break;
            case KeyScreen.ProcedureType.Direction:
                lineCount += 1;
                var furns3 = property.FindPropertyRelative("furns");
                lineCount += furns3.isExpanded ? Mathf.Max(furns3.arraySize, 1) + 2 : 0;
                break;
            case KeyScreen.ProcedureType.Unlock:
                lineCount += 3;
                var UnlockItem = property.FindPropertyRelative("UnlockItem");
                lineCount += UnlockItem.isExpanded ?Mathf.Max(UnlockItem.arraySize,1) : 0;
                break;
            case KeyScreen.ProcedureType.Artillery:
                lineCount += 1;
                var furns4 = property.FindPropertyRelative("furns");
                lineCount += furns4.isExpanded ? Mathf.Max(furns4.arraySize, 1) + 2 : 0;
                break;
        }

        // 如果是数组中的元素则增加额外间距
        if (property.propertyPath.Contains(".Array.data["))
            lineCount += 1;

        return lineCount * lineIntervalAndHeight;
    }
}
#endif
}
