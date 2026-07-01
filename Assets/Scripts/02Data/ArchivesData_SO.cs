using System;
using System.Collections.Generic;
using Core;
using Unity.FPS.Game;

using UnityEngine;
using Utils;

[CreateAssetMenu(fileName = "new Data", menuName = "Data/存档")]
/// <summary>
/// 存档信息
/// </summary>
public class ArchivesData_SO : ArchivesDataBase_SO
{

    public override string Path() => "KivotosCraftArc.json";

    #region 属性
    [Header("基础")]

    [Header("角色")]
    public string lastSelectRole;


    #endregion


    #region 角色信息
    [Space]
    [InspectorName("角色信息")]
    public DisplayDic<string, ArchRoleData> roleDataDic = new(true,(id) => {
        return new() {
            ID = id,
            Level = 1,
            Exp = 0,
            weaponSelect = new(true,new List<KVP<WeaponTypeEnum,int>>() {
                new(WeaponTypeEnum.Primary,0),
                new(WeaponTypeEnum.Secondary,0),
                new(WeaponTypeEnum.Special,0),
                new(WeaponTypeEnum.Grenade,0),
                new(WeaponTypeEnum.FlareGun,0),
                new(WeaponTypeEnum.Armor,0),
            })
        };
    });
    public void SetRoleLevel(string ID, int level, int exp)
    {
        if (exp >= 1000)
        {
            level += exp / 1000;
            exp %= 1000;
        }

       var data= roleDataDic[ID];
        data.Level=level;
        data.Exp= exp;
    }

    public void GetRoleLevel(string ID, out int level, out float expScale)
    {
        var data = roleDataDic[ID];

        level = data.Level;
        expScale = data.Exp/1000f;
    }

    public ArchRoleData GetRoleCfg(string ID) {
        return roleDataDic[ID];
    }
    public int[] GetWeaponSelect(string ID)
    {
        return roleDataDic[ID].weaponSelect.Values;
    }

    public void GainRoleExp(string ID,int gainValue, out int level, out float expScale)
    {
        var data= roleDataDic[ID];
        data.Exp += gainValue;
        if (data.Exp / 1000 > 0)
        {
            data.Level += data.Exp / 1000;
            data.Exp %= 1000;
        }

        level = data.Level;
        expScale = data.Exp/1000f;

        GlobalEventSub.OnGainExp?.Invoke(ID, level, expScale);
    }

    #endregion

    #region 地图势力信息
    [Space]
    [InspectorName("势力信息")]
    public DisplayDic<string, List<ArchOccupierData>> occupierDic = new();



    #endregion

    #region 武器改装
    [Space]
    [InspectorName("武器改装")]
    public DisplayDic<string, WeaponUpgradeData> weaponUpgradeDic = new();

    public int[][] GetWeaponUpgrade(string ID)
    {
        var re = new int[6][];
        var role = roleDataDic[ID];
        //Debug.LogError("查询"+ "GameData/Role/RD_" + ID+" 返回"+ Resources.Load<RoleData_SO>("GameData/Role/RD_" + ID));
        var data = Resources.Load<RoleData_SO>("GameData/Role/RD_"+ ID).weapons;
        for(int i = 0; i < 6; ++i)
        {
            var weapon = data[(WeaponTypeEnum)i][role.weaponSelect[(WeaponTypeEnum)i]];
            re[i]=weaponUpgradeDic.TryGet(ID + "_" + weapon.WeaponName,new(ID + "_" + weapon.WeaponName, weapon.UpgradeCount().Length)).selectIndex;
        }
        return re;
    }
    #endregion

    #region 载具改装

    [Space]
    [InspectorName("载具改装")]
    public DisplayDic<string, ArchVehicleData> VehicleCustomDic = new();


    #endregion

    #region 资源
    [Space]
    [InspectorName("资源和道具")]
    [SerializeField]
    public DisplayDic<OOPartEnum, int> propertys = new();

    #endregion

    #region 空投
    [Space]
    [Header("已购买的空投")]
    public List<int> AirdropBuyDic = new();


    #endregion
    #region 设置
    [Space]
    [InspectorName("设置")]
    public DisplayDic<string, ArchSettingData> settingDic;
    #endregion




    #region IO


    public void Save()
    {
        SaveFile();
    }

    protected override void InLoad()
    {
        Debug.LogWarning("加载了存档"+name+"数据数量"+roleDataDic.Count);
    }

    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    //private static void InitRef2() {
    //    InitRef();
    //}



    #endregion
    [System.Serializable]
    public class ArchRoleData
    {
        public string ID;
        public int Level;
        public int Exp;

        public DisplayDic<WeaponTypeEnum, int> weaponSelect;
    }

    [System.Serializable]
    public class ArchOccupierData
    {
        public string name;
        public int value;
    }

    [System.Serializable]
    public class WeaponUpgradeData
    {
        public string ID;
        public int[] selectIndex;
        [SerializeField]
        private int[] buyIndex;

        public int selectModuleIndex;
        [SerializeField]
        private int[] buyModuleIndex;

        public WeaponUpgradeData(string id, int lenght)
        {
            ID = id;
            selectIndex = new int[lenght];
            buyIndex = new int[lenght];
            for (int i = 0; i < lenght; ++i)
            {
                selectIndex[i] = -1;
                buyIndex[i] = 0;
            }
        }
        public bool GetBuy(int y, int x)
        {
            if (y > buyIndex.Length) return false;
            if (x > 2) return false;
            return (buyIndex[y] & (1 << x)) > 0;
        }
        public int BuyCount
        {
            get{
                int count = 0;
                for (int i=0;i<buyIndex.Length;++i)
                {
                    count += buyIndex[i].CountOnes();
                }
                return count;
            }
        }
        public void SetBuy(int y, int x)
        {
            buyIndex[y] |= (1 << x);
        }
        

    }

    [System.Serializable]
    public class ArchVehicleData
    {
        public int leftWeaponIndex;
        public int rightWeaponIndex;
        public int skinIndex;
        public int blendIndex;
        public ArchivesFloat blendScale=0.1f;
    }

    [System.Serializable]
    public class ArchSettingData
    {
        public string titile;
        public SettingBtnType type;
        public ArchivesFloat value;
        public string[] showTexts;
        public Vector2Int sliderRange;
        public string sliderSuffix;
    }
    public enum SettingBtnType { Dropdown, Toggle, Slider }

    [Serializable]
    public struct ArchivesFloat
    {
        const int digit = 2;
        [SerializeField]
        private string value;
        public ArchivesFloat(float value)
        {
            this.value = value.ToString("F" + digit); // 保留X位小数
        }

        public static implicit operator ArchivesFloat(float value)
        {
            return new ArchivesFloat(value);
        }

        public float RawFloat
        {
            get=> float.Parse(value);
        }
        public int RawInt
        {
            get => (int)float.Parse(value);
        }
        public override string ToString()
        {
            return value;
        }
    }
}

