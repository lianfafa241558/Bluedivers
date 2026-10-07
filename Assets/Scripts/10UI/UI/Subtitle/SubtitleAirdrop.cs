using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 空投位置的字幕标记。
/// </summary>
[AddComponentMenu("UI/字幕/空投标记")]
public class SubtitleAirdrop : SubtitleBase
{
    [SerializeField]
    private Transform stateText;
    private AirdropData data;
    private int lastTime;
    private LimitedLife m_particle;
    public override SubtitleBase Creat(IActor owner, GameObject target, Transform parent, bool alwaysShow)
    {
        base.Creat(owner, target, parent,alwaysShow);
        SetActive(gameObject, false);
        return this;
    }


    protected override void Update()
    {
        if (!m_particle.IsAlive())
        {
            SetActive(gameObject, false);
            data = null;
            return;
        }
        Follow(targetPoint);
        if (lastTime != (int)data.time)
        {
            lastTime = (int)data.time;
            SetText(stateText,(data.State== AirdropState.Arrive?"即将抵达":"正在进行")+": "+ Tool.FloatToTime(data.time));
        }
        
    }

    public override void TryActive(bool state)
    {
        //不受影响
    }
    /// <summary>
    /// 收到空投事件。<paramref name="airdropData"/> 由 `BattleEventSub.OnAirdrop` 直接带来
    /// （Java：原先这里写 `target.GetComponent&lt;VFXAirdropEffect&gt;().data` ⇒ UI 反向依赖 `10_Effect`，
    /// 而 Effect 在 UI **之上** ⇒ `10_UI` 切不出来。数据本来就在事件里，透传即可）。
    /// </summary>
    public void OnAirdrop(GameObject owner,GameObject target, Vector3 point, AirdropData airdropData)
    {
        // ⚠ 联机：**盟友呼叫的战备**在本机是"远端复现副本"，那条路 `BattleManager.ReleaseAirdrop
        //   → VFXAirdropEffect.TmpAirdrop` 发出的 `BattleEventSub.Airdrop` 里 **owner = null**
        //   ⇒ 老实现第一行就 return，盟友叫的空投在这边**完全没有字幕/标记**（2026-10-07 实测）。
        //   判据用 `isTmp`：远端复现副本是 `new AirdropData(SOn)`（isTmp = true），
        //   而本机自己那份 useAd 里的项 isTmp = false（仍按"owner 必须是我"筛）。
        bool remote = owner == null || (airdropData != null && airdropData.isTmp);
        if (!remote && owner != this.owner.gameObject) return;

        this.target = target;
        this.targetPoint = point;
        var ownerObj = owner != null ? owner.GetComponent<Actor>() : null;
        data = airdropData;
        m_particle = target.GetComponent<LimitedLife>();
        SetText(desc, data.cfg.showName);

        // ⚠ 远端那条没有 owner ⇒ 拿不到"是谁叫的"（要显示的话得把呼叫者 sid 传进来，见 AirdropCallMsg.Sid）。
        //   宁可空着也别显示错的名字（这里原先取的是本机玩家名）。
        SetText(title, ownerObj != null ? ownerObj.ShowName : string.Empty);
        SetSprite(halo, data.cfg.icon);
        SetColor(halo,data.cfg.IconColor);
        SetActive(gameObject, true);
    }
    protected override void Follow(Vector3 point)
    {
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(point);
        screenPosition *= Mathf.Sign(screenPosition.z);
        screenPosition.z = 0;
        base.Follow(point + Vector3.up * (Mathf.Sqrt(2*Vector3.Distance(point,owner.Pos))));
    }

}
}
