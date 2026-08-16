using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>数据编辑器的 Tab 类型枚举</summary>
public enum TabType
{
    Mission,
    Camp,
    Airdrop,
    Map,
    Update,
    WeaponModule,
    WeaponUpgrade,
    AboState,
    Weapon,
    Role,
    Booster,
}

/// <summary>数据编辑器各 Tab 页的统一契约</summary>
public interface IDataTab
{
    TabType TabType { get; }
    string DisplayName { get; }
    int Count { get; }
    bool HasSelection { get; }
    string SearchFilter { get; set; }
    Vector2 LeftScroll { get; set; }
    Vector2 RightScroll { get; set; }

    void Refresh();
    void DrawLeftPanel(Action drawFooter);
    void DrawRightPanel();
    void MoveSelection(int direction);
    string GetSelectedAssetPath();
    Object GetSelectedData();
}
