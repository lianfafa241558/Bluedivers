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
        GlobalEventManager.OnMark += OnMark;
        GlobalEventManager.OnAirdrop += OnAirdrop;
        //GlobalEventManager.OnCallKai += CallKai;
        //GlobalEventManager.OnFurnitureOperate += OnFurnitureOperate;
        GlobalEventManager.OnPlayMeetSoeech += OnMeetSpeech;
    }
    private void OnDestroy()
    {
        m_health.OnDamaged -= OnDamage;
        GlobalEventManager.OnMark -= OnMark;
        GlobalEventManager.OnAirdrop -= OnAirdrop;
        //GlobalEventManager.OnCallKai -= CallKai;
        //GlobalEventManager.OnFurnitureOperate -= OnFurnitureOperate;
        GlobalEventManager.OnPlayMeetSoeech -= OnMeetSpeech;
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
            var item = Cfg.Speech(type);
            if (item)
            {
                speechShowTime = item.Clip.length;
                GlobalEventManager.ActorSpeech(gameObject, item);
            }
        }
    }


    void OnMark(GameObject owner, GameObject target, Vector3 point)
    {
        if (owner != gameObject) return;
        Speech(SpeechTypeEnum.EnemySpotted);
    }
    void OnAirdrop(GameObject source, GameObject beacon, Vector3 point, AirdropController.AirdropData data)
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
        GlobalEventManager.ActorSpeech(source, Cfg.Speech(state));

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
            Notice("Kotama", "Airdrop");
        }
        else if (data.cfg.ID == 10)
        {
            Notice("Kotama", "Supply");
        }

        void Notice(string name,string type)
        {
            WndManager.Instance.CreatNotice(name, type, delay: 2, vaildTime: data.cfg.arriveTime);
        }
    }


    void OnMeetSpeech(GameObject user, SpeechTypeEnum state)
    {
        //Debug.LogError("收到事件目标玩家 "+user+" 本地玩家"+gameObject);
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
            var item = Cfg.Speech(type);
            if (item)
            {
                speechShowTime = item.Clip.length;
                AudioManager.PlaySound(new(Cfg.Speech(type).Clip, transform.position, 40, AudioGroups.Player,1));
            }
        }
 
    }

}
