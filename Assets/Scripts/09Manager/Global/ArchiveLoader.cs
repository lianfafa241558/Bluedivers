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

        /// <summary>补齐默认设置时是否产生了新项（决定要不要回写存档）。</summary>
        private bool _haveNewSetting;

        public void Init()
        {
            showArchive = (ArchivesData_SO)ArchivesData_SO.Load();
            ArchivesData_SO.Current = showArchive;//数据自持：上层写入，玩法层经 ArchivesData_SO.Current 读取（见该类注释）

            // ⚠ 补齐必须**同步、在本帧任何读取方之前**完成：
            //   默认资产后来新增过设置项（沉浸模式 / 默认操作视角），旧存档的 settingDic 里没有这些键，
            //   而缺键读取只会拿到 DisplayDic 的兜底模板（value 为空串）⇒ 打包版启动即 FormatException。
            //   本帧更早 Awake 的读取方（CanvasController / BridgeRoleManager → WndManager）拿不到修补后的数据，
            //   所以这里不能留到协程的 yield return null 之后（广播本身仍留到下一帧，见 SyncDefaultSettings）。
            _haveNewSetting = showArchive.settingDic.Synchronize(defaultArchive.settingDic);
            _haveNewSetting |= showArchive.roleDataDic.Synchronize(defaultArchive.roleDataDic);
            _haveNewSetting |= showArchive.propertys.Synchronize(defaultArchive.propertys);

            StartCoroutine(SyncDefaultSettings());
        }

        public void UnInit() { }

        /// <summary>
        /// 把补齐后的设置广播给订阅方（音量 / UI 缩放 / 显示模式…），并在确实补齐过时回写存档。
        /// <para>⚠ 只做广播和落盘：数据的同步已提前到 <see cref="Init"/>，见那里的注释。</para>
        /// </summary>
        IEnumerator SyncDefaultSettings()
        {
            yield return null;//等管理器与窗口 Awake 完（订阅 OnSettingCange）再广播
            showArchive.settingDic.ForEach((key, item) => GlobalEventBus.SettingCange(key, item.value.RawInt));

            if (_haveNewSetting)
                showArchive.Save();
        }

    }
}
