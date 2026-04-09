using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WndTools.WndRootTool;
using System.Linq;
using UnityEngine.Events;
using Unity.BaseTool;
using Utils;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class KeyScreen : MonoBehaviour
{
    public event UnityAction<int> OnUpdateStage;
    public event UnityAction OnComple;

    [SerializeField]
    private Color _LightColor = new(0, 0.627f, 1f);
    private const float _SwitchTime=0.5f;
    
    public List<GameObject> procedurePrefabs;


    public float LoadTime=5;
    public string showTitle;
    public List<Procedure> procedure;
    [SerializeField]
    Transform bg;
    Transform title, tip, stage,exit,inputs,load,wait, actionItem, paraModify, direction, unlock, password,end;
    [DisplayField]
    public Furniture_General furn;

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

        InitProcedre();
    }

    public void SetOwener(GameObject owner)
    {
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
                    AudioManager.PlaySound(new("UI/UI_ElementsA"));
                }
                GameRoot.CreateTimer(() => SetStage(0), LoadTime);
                //Debug.LogError("开始操作");
            }
        }
        SetActive(exit, owner.IsValid());

    }


    public void SetStage(int stage)
    {
        if (this == null) return;
        nowStage = stage;
        OnUpdateStage?.Invoke(stage);
        lastStageTime = Time.time;

        //有概率被OnUpdateStage事件修改
        if (stage != nowStage) return;
        if (stage < procedure.Count)
        {
            SetText(this.stage, (stage + 1) + "/" + procedure.Count);
            AudioManager.PlaySound(new("UI/UI_Reward2"));
            var now = nowProcedure;
            SetActive(false, inputs, load, wait, actionItem, paraModify, direction, unlock, password, end);
            SetText(tip, now.tip);
            furn.canOperate = true;
            dic[now.type].Item1.Invoke(now);
        }
        else//完成
        {
            OnComple?.Invoke();
            SetActive(false, inputs, load, wait, actionItem, paraModify, direction, unlock, password);
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
        if (!Utils.Tool.In(nowStage,-1,procedure.Count)) return;
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
        if (type == ProcedureType.Load || type == ProcedureType.Wait || type== ProcedureType.ParaModify) return true;
        return false;
    }

    public void AddTime(float time)
    {
        lastStageTime += time;
    }

    public float GetTime()=> Time.time - lastStageTime;


    [System.Serializable]
    public class Procedure{
        [CustomLabel("类型")]
        public ProcedureType type;
        public string tip;
        public bool eject;//是否弹出玩家 加载/等待

        public int minCount,maxCount;//输入
        public float time;//加载/等待
        public List<Furniture_Base> furns;//操作特定物体
        public List<string> UnlockItem;//解除锁定

    }
    public enum ProcedureType
    {
        /// <summary>输入</summary>
        [CustomLabel("输入")]
        Input,
        /// <summary>加载</summary>
        [CustomLabel("加载")]
        Load,
        /// <summary>等待</summary>
        [CustomLabel("等待")]
        Wait,
        /// <summary>操作特定物体</summary>
        [CustomLabel("操作特定物体")]
        ActionItem,
        /// <summary>调整系数</summary>
        [CustomLabel("调整系数")]
        ParaModify,
        /// <summary>旋转方向</summary>
        [CustomLabel("旋转方向")]
        Direction,
        /// <summary>解除锁定</summary>
        [CustomLabel("解除锁定")]
        Unlock,
        /// <summary>密码</summary>
        [CustomLabel("密码")]
        Password,
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

        // 绘制折叠箭头和标签
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


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return lineIntervalAndHeight;

        var typeProp = property.FindPropertyRelative("type");
        int lineCount = 2; // 基础属性(occasion+material+type)
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
        }

        // 如果是数组中的元素则增加额外间距
        if (property.propertyPath.Contains(".Array.data["))
            lineCount += 1;

        return lineCount * lineIntervalAndHeight;
    }
}
#endif
