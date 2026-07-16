using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    /// <summary>
    /// 武器升级
    /// </summary>
    public partial class WeaponPlayerController
    {
        /*
        [ContextMenu("转移")]
        public void Trans()
        {
            m_RenameAttr = new();
            m_Rename.ForEach((type,name) => m_RenameAttr.Add(new(){ name= name, type = type, addTag=default,removeTag=default }));
        
        }*/

        private Dictionary<string, AttrInfo> m_Parameter;

        [Foldout("点位和信息", true)]
        [InspectorName("属性调试")]
        [TextArea(3, 10)]
        [SerializeField]
        private string _showAttrInfo;

        [Foldout("点位和信息", true)]
        [TextArea(3,5)]
        public string desc;

        [Foldout("升级", true)]
        [InspectorName("重命名属性")]
        //[InspectorName("重命名属性")]
        //[SerializeField]
        //private DisplayDic<WeaponAttrType, string> m_Rename;
        //[SerializeField]
        public List<RenameAttrData> m_RenameAttr;

        [InspectorName("显示的属性")]
        [SerializeField]
        List<WeaponAttrType> showAttr;

        [InspectorName("专有属性(必须在这里吧可能出现的特色属性都注册了!)")]
        [SerializeField]
        private List<UniqueAttrData> m_UniqueAttr;


        [InspectorName("改装")]
        [SerializeField]
        private List<KVP<int,List<WeaponUpgradeData_SO>>> Upgrade;

        public List<WeaponModuleData_SO> Modules;

        [InspectorName("当前使用的模块")]
        public WeaponModuleData_SO ActiveModule;

        public WeaponModuleData_SO SetModule(int index)
        {
            if (Modules.Count == 0)
            {
                Debug.LogError("武器" + WeaponName + "没有可用模组");
                return null;
            }
            if (Tool.In(index, -1, Modules.Count))
            {
                int oldIndex = Modules.FindIndex(item => item == ActiveModule);
                ApplyModule(oldIndex >= 0 ? oldIndex : 0, index);
                RefreshDebugAttrInfo();
                return ActiveModule = Modules[index];
            }
            else
            {
                Debug.LogError("武器" + WeaponName + "设置的模组下标越界" + index);
                return ActiveModule;
            }
        }

        /// <summary>
        /// 应用武器修改
        /// </summary>
        /// <param name="select"></param>
        /// <param name="module"></param>
        public void ApplyUpgrade(int[] select,int module)
        {
            if (m_Parameter == null)
            {
                m_Parameter = new Dictionary<string, AttrInfo>();
            }
            else
            {
                m_Parameter.Clear();
            }
            for (var i = 0; i < showAttr.Count; ++i)
            {
                var item = showAttr[i];
                var name = GetAttrName(item);
                if(string.IsNullOrEmpty(name))Debug.LogError("错误" + item + "转为的name为空");
                m_Parameter[name] = NewAttr(item, name);
            }
            //额外属性是直接新建的不入cfg
            for (int i = 0; i < m_UniqueAttr.Count; ++i)
            {
                var item = m_UniqueAttr[i];
                m_Parameter[item.name] = new AttrInfo(item.name, item.value, item.tag);
            }

            for (int i = 0; i < select.Length; ++i)
            {
                if (select[i] >= 0) ApplyUpgrade(i, select[i]);
            }
            if (Modules.IsValid() && Modules.Count > 0)
            {
                int oldIndex = Modules.FindIndex(item => item == ActiveModule);
                ActiveModule = Modules[module];
                ApplyModule(oldIndex >= 0 ? oldIndex : 0, module);
            }

            WeaponUniqueAttributeApplier.Apply(this, m_Parameter);
            RefreshDebugAttrInfo();
        }


        public void ApplyModule(int oldIndex, int newIndex)
        {
            if (Modules.Count == 0) return;
            ApplyAttrInfo(Modules[oldIndex].modifys, false);
            ApplyAttrInfo(Modules[newIndex].modifys, true);
        }

        public void ApplyUpgrade(int y,int x)
        {
            ApplyAttrInfo(GetUpgrade(y, x).modifys, true);
        }

        public void RemoveUpgrade(int y,int x)
        {
            ApplyAttrInfo(GetUpgrade(y, x).modifys, false);
        }

        public void ApplyAttrInfo(List<ModifyAttrData> data, bool isAdd)
        {
            foreach (var item in data)
            {
                if (item.type == WeaponAttrType.Special)
                {
                    if(m_Parameter.TryGetValue(GetModifyDataName(item),out var info))
                    {
                        info.Modify(item.modifier, (isAdd ? 1 : -1) * item.value);
                        info.ResetChangeValue();
                    }
                    else
                    {
                        Debug.LogError($"在属性字典里面没有找到自定义属性{GetModifyDataName(item)}");
                    }
                }
                else
                {
                    ////在try中就加上了可能添加的属性，这里是一定有的
                    //m_Parameter[GetModifyDataName(item)].Modify(item.modifier, (isAdd ? 1 : -1) * item.value);
                    
                    //这里是可能存在不在字典的情况的，原展示属性里面没有，但是模组加上
                    if (!m_Parameter.TryGetValue(GetModifyDataName(item),out var para))
                    {
                        para = new(this, item.type, GetModifyDataName(item));
                        m_Parameter.Add(GetModifyDataName(item), para);
                        //Debug.Log($"新建了基础属性{item.type} {GetModifyDataName(item)}");
                    }
                    para.Modify(item.modifier, (isAdd ? 1 : -1) * item.value);
                    para.ResetChangeValue();
                    
                }
                    
                
            }
        }

        /// <summary>
        /// 显示武器界面右下角的参数
        /// </summary>
        /// <param name="info">名称,类型,是否是原始值,是否受影响</param>
        public void ShowText(out List<(string,string,bool,bool)> info)
        {
            var normal = new List<(string, string, bool, bool)>();
            var textOnly = new List<(string, string, bool, bool)>();

            foreach (var kvp in m_Parameter)
            {
                var re = kvp.Value;
                var name = kvp.Key;
                if (re.typeEnum == WeaponAttrType.Special && re.Value == 0 && re.ChangeValue == 0)
                {
                    continue;
                }
                if (re.HasFlag(AttrTag.TextOnly))
                {
                    if (re.ChangeValue > 0)
                        //Item3=true表示预览新增(绿色)，false表示已拥有(青色)
                        textOnly.Add(new(name, "+", re.Value == 0, false));
                    else if (re.Value > 0)
                        textOnly.Add(new(name, "-", false, false));
                    continue;
                }
                if (!re.IsHide())
                {
                    normal.Add(new(name, re.ToString(), re.ChangeValue != re.PrimeValue, re.ChangeValue != re.Value));
                }
            }
            normal.AddRange(textOnly);
            info = normal;
        }

        public WeaponUpgradeData_SO GetUpgrade(int y,int x)
        {
            return Upgrade[y].Value[x];
        }
        public int[] UpgradeCount()
        {
            var re = new int[Upgrade.Count];
            for(int i = 0; i < re.Length; ++i)
            {
                re[i] = Upgrade[i].Value.Count;
            }
            return re;
        }
        public int[] UpgradeLevel()
        {
            var re = new int[Upgrade.Count];
            for (int i = 0; i < re.Length; ++i)
            {
                re[i] = Upgrade[i].Key;
            }
            return re;
        }
        public int UpgradeLevel(int y)=> Upgrade[y].Key;

        private string GetAttrName(WeaponAttrType type) {
            if (m_RenameAttr.TryGet(item=>item.type==type, out var re))
            {
                return re.name;
            }
            else
            {
                return type.GetEnumString().TrimStartPrefix();
            }
            
        }
        
        private string GetModifyDataName(ModifyAttrData data)
        {
            return data.type!= WeaponAttrType.Special ||string.IsNullOrEmpty(data.name) ? GetAttrName(data.type) : data.name;
        }


        private AttrInfo NewAttr(WeaponAttrType type,string name)
        {
            return new(this, type, name);
        }

        public void TryUpgrade(List<ModifyAttrData> oldModify, List<ModifyAttrData> newModify)
        {
            var oldUpAttrDic = oldModify.ToDictionary(item => GetModifyDataName(item));
            var newUpAttrDic = newModify.ToDictionary(item => GetModifyDataName(item));

            //先尝试加上原属性字典中没有的属性，并且获得基础值以及设置为默认时隐藏
            foreach (var item in newModify)
            {
                if (!m_Parameter.TryGetValue(GetModifyDataName(item), out var para))
                {
                    para = new(this, item.type, GetModifyDataName(item));
                    para.AddFlag(AttrTag.DefaultHide);
                    m_Parameter.Add(GetModifyDataName(item), para);
                }
            }

            foreach (var kvp in m_Parameter)
            {
                var key = kvp.Key;
                var baseItem = kvp.Value;

                bool have = false;
                ModifyAttrData oldValue = default, newValue = default;
                have |= oldUpAttrDic.TryGetValue(key, out oldValue);
                have |= newUpAttrDic.TryGetValue(key, out newValue);
                //两个里面起码得有一个具有这个属性
                if (have) baseItem.TryModify(oldValue, newValue);
                else baseItem.ResetChangeValue();
            }
            //ShowText(out var info);
            RefreshDebugAttrInfo();
        }

        /// <summary>重置所有属性的ChangeValue到当前Value，确保基线干净</summary>
        public void ResetAllChangeValues()
        {
            if (m_Parameter == null) return;
            foreach (var kvp in m_Parameter)
            {
                kvp.Value.ResetChangeValue();
            }
        }

        /// <summary>
        /// 刷新属性调试信息
        /// </summary>
        private void RefreshDebugAttrInfo()
        {
            if (m_Parameter == null)
            {
                _showAttrInfo = "(null)";
                return;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kvp in m_Parameter)
            {
                sb.AppendLine($"{kvp.Key}: Value={kvp.Value.Value:F2}, ChangeValue={kvp.Value.ChangeValue:F2}");
            }
            _showAttrInfo = sb.ToString();
        }

        /// <summary>
        /// 武器理论最大射程
        /// </summary>
        /// <returns></returns>
        private float MaxThrowRange()
        {
            //1.v0不只是水平速度，而是v0x/cos(θ),此时简化为v0x*角度系数
            //2.射程公式: R = (v0*v0* sin(2θ)) / g，其中θ=45°时简化为v0*v0/g
            //结合12:R=2*V0x*v0x/g
            var data = CurrentDamgeData;
            if (data.Gravity == 0)
            {
                return float.MaxValue;
            }
            return 2 * (data.Speed * data.Speed) / data.Gravity;
        }

        [System.Serializable]
        public class AttrInfo
        {
            
            /// <summary>类型</summary>
            [InspectorName("类型")]
            public WeaponAttrType typeEnum;
            /// <summary>名称</summary>
            [InspectorName("名称")]
            [Compare("typeEnum",(int)WeaponAttrType.Special, CompareOperate.Equal)]
            public string name;
            /// <summary> 默认值</summary>
            [InspectorName("默认值")]
            [Compare("typeEnum", (int)WeaponAttrType.Special, CompareOperate.NotEqual)]
            public float defaultValue;

            GameAttribute attr;
            public AttrTag tag;//info的实际tag是可能和挂靠的属性不一样的

            public AttrInfo(WeaponPlayerController weapon, WeaponAttrType type,string name)
            {
                //Debug.LogError($"创建属性info{type} {name}");
                typeEnum = type;
                attr = weapon.cfg[type];
                tag = WeaponAttributeFactory.GetTag(type);
                this.name = name;
                ChangeValue = Value;
            }

            public AttrInfo(string name,float value, AttrTag flag)
            {
                //Debug.LogError($"创建自定义属性info{name}");
                typeEnum = WeaponAttrType.Special;
                attr = new GameAttribute(new(value),flag, ModifierType.All);
                tag = flag| AttrTag.DefaultHide;
                this.name = name;
                ChangeValue = value;
            }

            public float ChangeValue { get; private set; }
            public float Value => attr.FinalValue.RawFloat;

            public float PrimeValue => attr.PrimeValue.RawFloat;

            public bool HasFlag(AttrTag tag) => this.tag.HasFlag(tag);
  
            public void AddFlag(AttrTag tag)
            {
                this.tag |= tag;
            }

            public bool IsHide()
            {
                if (HasFlag(AttrTag.IsHide)) return true;
                if (HasFlag(AttrTag.DefaultHide))
                {
                    return Value == attr.PrimeValue.RawFloat && ChangeValue == Value;
                }
                if (HasFlag(AttrTag.OneHide))
                {
                    if (HasFlag(AttrTag.ScaleFromPrime))
                    {
                        return Value == attr.PrimeValue.RawFloat && ChangeValue == Value;
                    }
                    return Value == 1 && ChangeValue == 1;
                }
                return false;
            }

            public override string ToString()
            {
                string re = "";
                var cv = ChangeValue;
                var fv = Value;
                if (HasFlag(AttrTag.Reciprocal))
                {
                    cv = cv != 0 ? 1 / cv : float.MaxValue;
                    fv = fv != 0 ? 1 / fv : float.MaxValue;
                }
                if (HasFlag(AttrTag.Percentage))
                {
                    if (HasFlag(AttrTag.OneHide))
                    {
                        cv = Tool.Round(cv * 100);
                        fv = Tool.Round(fv * 100);
                    }
                    else
                    {
                        cv = Tool.Round(cv / PrimeValue * 100);
                        fv = Tool.Round(fv / PrimeValue * 100);
                    }
                    
                }
                if (HasFlag(AttrTag.ScaleFromPrime))
                {
                    var prime = PrimeValue;
                    if (prime != 0)
                    {
                        cv = Tool.Round(cv / prime * 100);
                        fv = Tool.Round(fv / prime * 100);
                    }
                }

                var diff = Tool.Round(cv - fv);
                if (diff != 0)
                {
                    Color Positive = new(0.2f, 1, 0.2f), Negative = new(1, 0.2f, 0.2f);
                    if (HasFlag(AttrTag.FlipPlus))
                    {
                        Color tmp = Negative;
                        Negative = Positive;
                        Positive = tmp;
                    }
                    re = string.Format("<color=#{1}>{0}{2}</color>",
                        diff > 0 ? "+" : "", 
                        ColorUtility.ToHtmlStringRGB(diff > 0 ? Positive : Negative),
                        diff + (HasFlag(AttrTag.ScaleFromPrime) ? "%" : HasFlag(AttrTag.Percentage) ? "%" : ""));
                }
                var baseString = (Tool.Round(cv).ToString() + (HasFlag(AttrTag.ScaleFromPrime) ? "%" : HasFlag(AttrTag.Percentage) ? "%" : ""));
                var padWidth = Mathf.Max(0, 15 - Tool.TextLength(baseString, 2, 1, 0.5f, 2));
                re += baseString.PadLeft(padWidth);
               
                return re;
            }

            public void Modify(ModifierType type,float value)
            {
               
                attr.AddModifier(type,new(value));
            }

            public void TryModify(ModifyAttrData oldData,ModifyAttrData newData)
            {
                Modify(oldData.modifier, -oldData.value);
                Modify(newData.modifier, newData.value);
                ChangeValue = Value;
                Modify(oldData.modifier, oldData.value);
                Modify(newData.modifier, -newData.value);
            }
            
            public void ResetChangeValue()
            {
                ChangeValue = Value;
            }

        }

    }
    //[Singleline]
    [System.Serializable]
    public struct ModifyAttrData
    {
        [InspectorName("名称")]
        //[Compare("type", (int)WeaponAttrType.Special, CompareOperate.Equal)]
        public string name;
        [InspectorName("类型")]
        public WeaponAttrType type;
        [InspectorName("修正")]
        public ModifierType modifier;
        [InspectorName("值")]
        public float value;
    }
    [Singleline]
    [System.Serializable]
    public struct UniqueAttrData
    {
        public string name;
        public float value;
        [InspectorName("标旗")]
        public AttrTag tag;
    }

    [Singleline]
    [System.Serializable]
    public struct RenameAttrData
    {
        public string name;
        [InspectorName("类型")]
        public WeaponAttrType type;
        [InspectorName("添加标旗")]
        public AttrTag addTag;
        [InspectorName("移除标旗")]
        public AttrTag removeTag;
    }
}