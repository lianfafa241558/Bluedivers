using System.Collections.Generic;
using Core;
using PEMaths;

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
        [TextArea(3,5)]
        public string desc;

        [Foldout("升级", true)]
        [Header("重命名属性")]
        //[InspectorName("重命名属性")]
        //[SerializeField]
        //private DisplayDic<WeaponAttrType, string> m_Rename;
        [SerializeField]
        private List<RenameAttrData> m_RenameAttr;


        [Header("显示的属性")]
        [InspectorName("显示的属性")]
        [SerializeField]
        List<WeaponAttrType> showAttr;

        [Header("专有属性")]
        //[InspectorName("专有属性")]
        [SerializeField]
        private List<UniqueAttrData> m_UniqueAttr;


        [Header("改装")]

        [SerializeField]
        private List<KVP<int,List<WeaponUpgradeData_SO>>> Upgrade;

        public List<WeaponModuleData_SO> Modules;

        [InspectorName("当前使用的模块")]
        public WeaponModuleData_SO ActiveModule;

        public WeaponModuleData_SO SetModule(int index)
        {
            if(Tool.In(index, -1, Modules.Count))
            {
                ApplyModule(Modules.FindIndex(item=>item==ActiveModule), index);
                return ActiveModule = Modules[index];
            }
            else
            {
                Debug.LogError("武器"+WeaponName+"设置的模组下标越界"+index);
                ApplyModule(Modules.FindIndex(item => item == ActiveModule), 0);
                return ActiveModule = Modules[0];
            }
        }

        /// <summary>
        /// 应用武器修改
        /// </summary>
        /// <param name="select"></param>
        /// <param name="module"></param>
        public void ApplyUpgrade(int[] select,int module)
        {
            m_Parameter = new();
            for (var i = 0; i < showAttr.Count; ++i)
            {
                var item = showAttr[i];
                var name = GetAttrName(item);
                if(string.IsNullOrEmpty(name))Debug.LogError("错误"+""+item+"转为的name为空");
                m_Parameter[name] = NewAttr(item, name);
            }
            //额外属性是直接新建的不入cfg
            m_UniqueAttr.ForEach(item => {
                m_Parameter[item.name] = new AttrInfo(item.name,item.value,item.tag);
            });

            for (int i = 0; i < select.Length; ++i)
            {
                if (select[i] >= 0) ApplyUpgrade(i, select[i]);
            }
            if (Modules.IsValid() && Modules.Count > 0)
            {
                ActiveModule = Modules[module];
                ApplyModule(0, module);
            }

            WeaponUniqueAttributeApplier.Apply(this, m_Parameter);
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
                if (item.type== WeaponAttrType.Special)
                {
                    if (isAdd)
                    {
                        m_Parameter.TryAdd(GetModifyDataName(item), NewAttr(item.type,GetAttrName(item.type)));
                    }
                    else
                    {
                        m_Parameter.Remove(GetModifyDataName(item));
                    }
                }
                else
                {
                    //在try中就加上了可能添加的属性，这里是一定有的
                    m_Parameter[GetModifyDataName(item)].Modify(item.modifier, (isAdd ? 1 : -1) * item.value);
                    /*
                    //这里是可能存在不在字典的情况的，原展示属性里面没有，但是模组加上
                    if (!m_Parameter.TryGetValue(GetModifyDataName(item),out var para))
                    {
                        para = new(this, item.type, GetModifyDataName(item));
                        m_Parameter.Add(GetModifyDataName(item), para);
                    }
                    para.Modify(item.modifier, (isAdd ? 1 : -1) * item.value);
                    */
                }
            }
        }

        /// <summary>
        /// 显示武器界面右下角的参数
        /// </summary>
        /// <param name="info">名称,类型,是否是原始值,是否受影响/param>
        public void ShowText(out List<(string,string,bool,bool)> info)
        {
            List<(string, string, bool, bool)> special=new();
            info = new();

            var list = m_Parameter.GetKeys();
            for (var i = 0; i < m_Parameter.Count; ++i)
            {
                var re = m_Parameter[list[i]];
                if (re.typeEnum == WeaponAttrType.Special&&re.Value==0)
                {
                    special.Add(new(list[i],"",true,false));
                }
                else if (!re.IsHide())
                {
                    info.Add(new(list[i], re.ToString(), re.ChangeValue != re.PrimeValue,re.ChangeValue!= re.Value));
                }
            }
            info.AddRange(special);
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
                return type.GetEnumString();
            }
            
        }
        
        private string GetModifyDataName(ModifyAttrData data)
        {
            return string.IsNullOrEmpty(data.name) ? GetAttrName(data.type) : data.name;
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

            var list = m_Parameter.GetKeys();
            for (var i = 0; i < list.Length; ++i)
            {
                var baseItem = m_Parameter[list[i]];

                bool have = false;
                ModifyAttrData oldValue = default, newValue = default;
                have |= oldUpAttrDic.TryGetValue(list[i], out oldValue);
                have |= newUpAttrDic.TryGetValue(list[i], out newValue);
                //两个里面起码得有一个具有这个属性
                if (have) baseItem.TryModify(oldValue, newValue);
                else baseItem.ResetChangeValue();
            }
            //ShowText(out var info);
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
            return 2*(data.Speed * data.Speed) / data.Gravity;
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
                typeEnum = type;
                attr = weapon.cfg[type];
                tag = WeaponAttributeFactory.GetTag(type);
                this.name = name;
                ChangeValue = Value;
            }

            public AttrInfo(string name,float value, AttrTag flag)
            {
                typeEnum = WeaponAttrType.Special;
                attr = new GameAttribute(new(value),flag, ModifierType.All);
                tag = flag;
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
                if (HasFlag(AttrTag.DefaultHide))
                {
                    return Value == attr.PrimeValue.RawFloat && ChangeValue == Value;
                }
                if (HasFlag(AttrTag.OneHide))
                {
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
                    cv = 1 / cv;
                    fv = 1 / fv;
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
                        cv = Tool.Round(cv/ PrimeValue * 100);
                        fv = Tool.Round(fv/ PrimeValue * 100);
                    }
                    
                }

                if (cv != fv)
                {
                    var value = cv - fv;
                    Color Positive = new(0.2f, 1, 0.2f), Negative = new(1, 0.2f, 0.2f);
                    if (HasFlag(AttrTag.FlipPlus))
                    {
                        Color tmp = Negative;
                        Negative = Positive;
                        Positive = tmp;
                    }
                    re = string.Format("<color=#{1}>{0}{2}</color>",
                        value > 0 ? "+" : "", 
                        ColorUtility.ToHtmlStringRGB(value > 0 ? Positive : Negative),
                        Tool.Round(value) + (HasFlag(AttrTag.Percentage) ? "%" : ""));
                }
                var baseString = (Tool.Round(cv).ToString() + (HasFlag(AttrTag.Percentage) ? "%" : ""));
                re += baseString.PadLeft(15-Tool.TextLength(baseString,2,1,0.5f,2));
               
                return re;
            }

            public void Modify(ModifierType type,float value)
            {
               
                attr.AddModifier(type,new(value));
            }

            public void TryModify(ModifyAttrData oldData,ModifyAttrData newData)
            {
                Debug.LogError(oldData.type+"旧修饰类型" + oldData.modifier + "值" + oldData.value);
                Debug.LogError("新修饰类型" + newData.modifier + " " + newData.value);
                Modify(oldData.modifier, -oldData.value);//-10
                Modify(newData.modifier, newData.value);//+0
                ChangeValue = Value;
                Modify(oldData.modifier, oldData.value);//-10
                Modify(newData.modifier, -newData.value);//+0
            }
            public void ResetChangeValue()
            {
                ChangeValue = Value;
            }

        }

    }

    [System.Serializable]
    public struct ModifyAttrData
    {
        [InspectorName("名称")]
        [Compare("type", (int)WeaponAttrType.Special, CompareOperate.Equal)]
        public string name;
        [InspectorName("类型")]
        public WeaponAttrType type;
        public ModifierType modifier;
        public float value;
    }

    [System.Serializable]
    public struct UniqueAttrData
    {
        public string name;
        public float value;
        [InspectorName("标旗")]
        public AttrTag tag;
    }

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