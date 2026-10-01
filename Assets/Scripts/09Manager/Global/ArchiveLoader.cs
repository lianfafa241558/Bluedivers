using System.Collections;
using FPSGame.Core.Interface;
using UnityEngine;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Attributes;
using FPSGame.GameData;

namespace FPSGame.Managers
{

    /// <summary>
    /// 存档读写与默认设置同步。
    /// </summary>

    public class ArchiveLoader : MonoBehaviour, I_GlobaManager
    {

        [DisplayField]
        [SerializeField]
        private ArchivesData_SO showArchive;
        [SerializeField]
        private ArchivesData_SO defaultArchive;

        public void Init()
        {
            showArchive = (ArchivesData_SO)ArchivesData_SO.Load();
            ArchivesData_SO.Current = showArchive;//数据自持：上层写入，玩法层经 ArchivesData_SO.Current 读取（见该类注释）
            StartCoroutine(nameof(SyncDefaultSettings));
        }

        public void UnInit() { }

        IEnumerator SyncDefaultSettings()
        {
            yield return null;
            bool haveNewSetting = false;
            haveNewSetting |= showArchive.settingDic.Synchronize(defaultArchive.settingDic);
            haveNewSetting |= showArchive.roleDataDic.Synchronize(defaultArchive.roleDataDic);
            haveNewSetting |= showArchive.propertys.Synchronize(defaultArchive.propertys);

            showArchive.settingDic.ForEach((key, item) => GlobalEventSub.SettingCange(key, item.value.RawInt));

            if (haveNewSetting)
                showArchive.Save();
        }

    }
}
