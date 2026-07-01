using System.Collections;
using Core.Interface;
using UnityEngine;

public class ArchiveSvc : MonoBehaviour, I_GlobaManager
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
        showArchive = (ArchivesData_SO)ArchivesData_SO.Load();
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

    public static float GetSetting(string name) => Archive.settingDic[name].value.RawFloat;
}
