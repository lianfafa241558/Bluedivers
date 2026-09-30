using System.Collections;
using FPSGame.Core.Interface;
using UnityEngine;
using FPSGame.Game;
using FPSGame.Gameplay;

namespace FPSGame.Managers
{

/// <summary>
/// 存档读写与默认设置同步。
/// </summary>
[AddComponentMenu("管理/存档服务")]
public class ArchiveSvc : MonoBehaviour, I_GlobaManager, FPSGame.GameContract.IArchiveService
{
    public static ArchiveSvc Instance { get; private set; }
    public static ArchivesData_SO Archive => Instance.showArchive;

    [SerializeField]
    private ArchivesData_SO showArchive;
    [SerializeField]
    protected ArchivesData_SO defaultArchive;

    public void Init()
    {
        Instance = this;
        FPSGame.GameContract.ServiceLocator.Archive = this;//注册存档服务：供玩法层/Effect 等下层访问（见 ServiceLocator.cs）
        showArchive = (ArchivesData_SO)ArchivesData_SO.Load();
        ArchivesData_SO.Current = showArchive;//数据自持：上层写入，玩法层经 ArchivesData_SO.Current 读取（见该类注释）
        StartCoroutine(nameof(SyncDefaultSettings));
    }

    public void UnInit() { }

    IEnumerator SyncDefaultSettings()
    {
        yield return null;
        bool haveNewSetting = false;
        haveNewSetting |= Archive.settingDic.Synchronize(defaultArchive.settingDic);
        haveNewSetting |= Archive.roleDataDic.Synchronize(defaultArchive.roleDataDic);
        haveNewSetting |= Archive.propertys.Synchronize(defaultArchive.propertys);

        Archive.settingDic.ForEach((key, item) => GlobalEventSub.SettingCange(key, item.value.RawInt));

        if (haveNewSetting)
            Archive.Save();
    }

    /// <summary>FPSGame.GameContract.IArchiveService 实现（原方法是静态，故显式转发）。</summary>
    float FPSGame.GameContract.IArchiveService.GetSetting(string name) => GetSetting(name);

    public static float GetSetting(string name) => Archive.settingDic[name].value.RawFloat;
}
}
