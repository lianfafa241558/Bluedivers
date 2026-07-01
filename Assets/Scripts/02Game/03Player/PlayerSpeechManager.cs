using System.Collections;
using System.Collections.Generic;
using Core;
using PEMaths;
using Unity.FPS.Game;
using UnityEngine;

public class PlayerSpeechManager : MonoBehaviour
{
    public RoleData_SO Cfg=>GetComponent<PlayerController>().Cfg;

    HealthPlayer m_health;
    float lastSpeechTime, speechShowTime;
    SpeechTypeEnum lastSpeechType;


    private void Start()
    {
        m_health = GetComponent<HealthPlayer>();
        m_health.OnDamaged += OnDamage;
        GlobalEventSub.OnMark += OnMark;
        BattleEventSub.OnAirdrop += OnAirdrop;
        //GlobalEventManager.OnCallKai += CallKai;
        //GlobalEventManager.OnFurnitureOperate += OnFurnitureOperate;
        GlobalEventSub.OnPlayMeetSpeech += OnMeetSpeech;
    }
    private void OnDestroy()
    {
        m_health.OnDamaged -= OnDamage;
        GlobalEventSub.OnMark -= OnMark;
        BattleEventSub.OnAirdrop -= OnAirdrop;
        //GlobalEventManager.OnCallKai -= CallKai;
        //GlobalEventManager.OnFurnitureOperate -= OnFurnitureOperate;
        GlobalEventSub.OnPlayMeetSpeech -= OnMeetSpeech;
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
            GlobalEventSub.ActorSpeech(gameObject, item);
        }
    }


    void OnMark(GameObject owner, GameObject target, Vector3 point)
    {
        if (owner != gameObject) return;
        Speech(SpeechTypeEnum.EnemySpotted);
    }
    void OnAirdrop(GameObject source, GameObject _, Vector3 point, AirdropController.AirdropData data)
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

        GlobalEventSub.ActorSpeech(source, Cfg.SpeechGroup(state).Get(transform.position));

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
            WndManager.Instance.CreatNotice(name, type, vaildTime: data.arriveTime);
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
