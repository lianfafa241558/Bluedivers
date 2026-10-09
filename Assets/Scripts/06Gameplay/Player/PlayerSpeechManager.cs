using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Data;
using FPSGame.Audio;
using FPSGame.Gameplay;
using FPSGame.AI;
using FPSGame.Utils;
using FPSGame.GameData;

namespace FPSGame.Gameplay
{

/// <summary>
/// 玩家语音播报（受击、空投、标记等）。
/// </summary>
[AddComponentMenu("玩家/语音播报")]
public class PlayerSpeechManager : MonoBehaviour
{
    public RoleData_SO Cfg=>GetComponent<PlayerController>().Cfg;

    IHealth m_health;
    float lastSpeechTime, speechShowTime;
    SpeechTypeEnum lastSpeechType;


    private void Start()
    {
        m_health = GetComponent<IHealth>();
        m_health.OnDamaged += OnDamage;
        GlobalEventBus.OnMark += OnMark;
        BattleEventBus.OnAirdrop += OnAirdrop;
        //GlobalEventManager.OnCallKai += CallKai;
        //GlobalEventManager.OnFurnitureOperate += OnFurnitureOperate;
        GlobalEventBus.OnPlayMeetSpeech += OnMeetSpeech;
    }
    private void OnDestroy()
    {
        m_health.OnDamaged -= OnDamage;
        GlobalEventBus.OnMark -= OnMark;
        BattleEventBus.OnAirdrop -= OnAirdrop;
        //GlobalEventManager.OnCallKai -= CallKai;
        //GlobalEventManager.OnFurnitureOperate -= OnFurnitureOperate;
        GlobalEventBus.OnPlayMeetSpeech -= OnMeetSpeech;
    }

    bool CanSpeech(SpeechTypeEnum type)
    {
        return Time.time > speechShowTime + lastSpeechTime || lastSpeechType != type;
    }
    public void Speech(SpeechTypeEnum type)
    {
        if (CanSpeech(type))
        {
            lastSpeechTime = Time.time;
            lastSpeechType = type;
            var item = Cfg.SpeechGroup(type).Get(transform.position);
            //Debug.LogError("找到的语音"+item,item);
            speechShowTime = item.Clip.length;
            GlobalEventBus.ActorSpeech(gameObject, item);

            // 联机：告诉联机桥"我喊了一句"，由它上行 / 转发（本机表现已经播完，不参与回环）
            BattleEventBus.PlayerSpeech(gameObject, type);
        }
    }


    void OnMark(GameObject owner, GameObject target, Vector3 point)
    {
        if (owner != gameObject) return;
        Speech(SpeechTypeEnum.EnemySpotted);
    }
    void OnAirdrop(GameObject source, GameObject _, Vector3 point, AirdropData data)
    {

        if (!source.IsValid()) return;

        SpeechTypeEnum state = SpeechTypeEnum.Airdrop;
        if (data.cfg.type == AirdropData_SO.AirdropType.Greed)
        {
            state = SpeechTypeEnum.Turret;
        }
        else if (data.cfg.type == AirdropData_SO.AirdropType.Red)
        {
            if (data.cfg.deliveryType == AirdropDeliveryEnum.Jet)
            {
                state = SpeechTypeEnum.Airstrike;
            }
            else
            {
                state = SpeechTypeEnum.Bombing;
            }
        }
        else if (data.cfg.type == AirdropData_SO.AirdropType.Orange)
        {
            state = SpeechTypeEnum.Vehicle;
        }
        else if (data.cfg.ID == 10)
        {
            state = SpeechTypeEnum.Supply;
        }

        GlobalEventBus.ActorSpeech(source, Cfg.SpeechGroup(state).Get(transform.position));
        // ⚠ 这条没有走 Speech()（空投那套文案要按空投类型挑），所以单独上报一次
        BattleEventBus.PlayerSpeech(source, state);

        if (data.cfg.type == AirdropData_SO.AirdropType.Greed)
        {
            Notice("Kotama", "AirdropGreen");
        }
        else if (data.cfg.type == AirdropData_SO.AirdropType.Red)
        {
            if (data.cfg.deliveryType == AirdropDeliveryEnum.Jet)
            {
                Notice("Moe", "Attack");
            }
            else
            {
                Notice("Kotama", "AirdropRed");
            }
        }
        else if (data.cfg.type == AirdropData_SO.AirdropType.Orange)
        {
            Notice("Ayane", "Vehicle");
        }
        else if (data.cfg.ID == Constants.SupplyId)
        {
            Notice("Kotama", "Supply");
        }
        else if (data.cfg.ID == Constants.EagleReloadId)
        {
            Notice("Moe", "Reload");
        }
        void Notice(string name,string type)
        {
            FPSGame.Gameplay.GlobalEventBus.Notice(name, type, null, data.arriveTime);
        }
    }


    void OnMeetSpeech(GameObject user, SpeechTypeEnum state)
    {
        //Debug.LogError("收到事件目标玩家 "+user+" 本地玩家"+gameObject+"尝试"+ state);
        if (user == gameObject)
        {
            Speech(state);
        }
    }

    //因为要进来结算上传呼叫啥的防止重复喊话，所以要放这里
    void OnDamage(PEInt dmg, GameObject damageSource, Collider collider, bool noSource)
    {
        //if (m_health.GetShieldRatio() > 0) return;
        var type = SpeechTypeEnum.Damage;
        if (CanSpeech(type))
        {
            lastSpeechTime = Time.time;
            lastSpeechType = type;
            var item = Cfg.SpeechGroup(type);
            if (item!=null)
            {
                var re = item.Get(transform.position);
                speechShowTime = re.Clip.length;
                AudioSvc.PlaySound(re);
            }
        }
 
    }

}
}
