using Unity.FPS.Game;
using UnityEngine;
using System.Collections.Generic;

using AirdropState = AirdropController.AirdropState;
using Unity.FPS.Gameplay;

using Core;
using Utils;
using GameContract;

public class VFXAirdropEffect : MonoBehaviour, IVfxEffect 
{

    public System.Action<GameObject> OnCreatObject;

    [SerializeField]
    private GameObject normalPod;
    [SerializeField]
    private GameObject eagle;
    [SerializeField]
    private GameObject neoNimbusVehicle;

    //[SerializeField]
    //private List<NoticeData_SO> warning;
    //[Display(true,false,true)]
    //[HideInInspector]
    public AirdropController.AirdropData data;

    [SerializeField]
    private Transform effectRangeCube,effectRangeCircle;
    [SerializeField]
    private new Light light;

    [SerializeField]
    private Transform m_creatObject;
    /// <summary>预计的降落时间</summary>
    private float m_ExpectedDuration;

    public float showTime = 0;

    private LimitedLife m_Lift;
    private GameObject m_owner;
    private WeaponBaseController m_weapon;

    public float m_lastWarnTime = 0;
    public void SetOwner(GameObject owner, GameObject weaponRoot, Collider collider, Vector3 point) {
        //其实这里有个bug，如果连续放，就会变成同时落地，但是实际上拍空投有Cd，所以直接不管！
        data = AirdropController.WaitRelease;
        //同时,锁定的点总是会是实际的脚下而不是单位头顶
        if (Physics.Raycast(point+Vector3.up*10,Vector3.down, out var hit,50, LayerDefinition.GroundLayers))
        {
            transform.position = point = hit.point;
        }
        BattleEventSub.Airdrop(owner, gameObject, point, data);
        
        transform.parent = null;
        transform.eulerAngles = new(0, owner.transform.eulerAngles.y, 0);
        m_owner = owner;
        m_weapon = weaponRoot.GetComponent<WeaponBaseController>();
        Init();
    }
    public void TmpAirdrop(Vector3 point, AirdropData_SO data, System.Action<GameObject> action)
    {
        this.data = new(data);

        if (Physics.Raycast(point + Vector3.up * 100, Vector3.down, out var hit, 150, LayerDefinition.GroundLayers))
        {
            transform.position = point = hit.point;
        }
        BattleEventSub.Airdrop(null, gameObject, point, this.data);

        transform.parent = null;
        //transform.eulerAngles = Vector3.zero;
        m_owner = ActorsManager.Player.gameObject;
        m_weapon = m_owner.GetComponent<PlayerWeaponsManager>().GetWeaponAtSlotIndex((int)WeaponTypeEnum.FlareGun);
        if(action.IsValid()) OnCreatObject += action;
        Init();
    }

    private void Init()
    {

        m_creatObject = null;
        SetDisplay();
        switch (data.cfg.deliveryType)
        {
            case AirdropDeliveryEnum.Pod:
                StartPod();
                break;
            case AirdropDeliveryEnum.Bomb:
                StartBomb();
                break;
            case AirdropDeliveryEnum.Jet:
                StartJet();
                break;
            case AirdropDeliveryEnum.Medivac:
                StartMedivac();
                break;
        }
    }

    private void Update()
    {
        if (data.isTmp) data.Update();
        showTime += Time.deltaTime;
        switch (data.cfg.deliveryType)
        {
            case AirdropDeliveryEnum.Pod:
                UpdatePod();
                break;
            case AirdropDeliveryEnum.Bomb:
                UpdateBomb();
                break;
            case AirdropDeliveryEnum.Jet:
                UpdateJet();
                break;
            case AirdropDeliveryEnum.Medivac:
                UpdateMedivac();
                break;
        }
        TryWarning();
    }

    private void OnDisable()
    {
        if (!m_creatObject.IsValid()) return;

        switch (data.cfg.deliveryType)
        {
            case AirdropDeliveryEnum.Pod:
                EndPod();
                break;
            case AirdropDeliveryEnum.Bomb:
                EndBomb();
                break;
            case AirdropDeliveryEnum.Jet:
                EndJet();
                break;
            case AirdropDeliveryEnum.Medivac:
                EndMedivac();
                break;
        }
        if (m_creatObject&&m_creatObject.TryGetComponent(out ProjectileBase pro))
        {
            pro.OnHit -= PodHit;
        }
        OnCreatObject = null;
        data = null;
        m_creatObject = null;
        //m_particle.Stop(true);
        m_Lift.SetLift(1);
        
    }

    #region 空投舱系
    void StartPod()
    {
        //重力加速度g=10,公式h=1/2*g*t^2=5*t^2;
        //反转取时间就是t=sqrt(s/5)开根号
        m_ExpectedDuration = Mathf.Sqrt(data.cfg.arriveHeight * 0.1f)+0.5f;
        if (m_ExpectedDuration > data.arriveTime) { Debug.LogError(data.cfg.showName + "设置的高度不足以使其在限时内自由落体落地"+"预计需要的时间"+m_ExpectedDuration); }
    }
    void UpdatePod()
    {
        if (data.State== AirdropState.Arrive && !m_creatObject.IsValid())
        {
            
            if (data.time < m_ExpectedDuration)
            {
                AudioSvc.PlaySound(new("AirDrop/PodIntA_1", transform.position + 10 * Vector3.up, 50, AudioGroups.Weapon,0.8f));
                //使用标准空投舱
                if (data.cfg.useNormalPod)
                {
                    m_creatObject = VFXManager.Creat(normalPod, transform.position + Vector3.up * data.cfg.arriveHeight, transform.rotation, null).transform;
                    if (m_creatObject.TryGetComponent(out Animator anim))
                    {
                        anim.enabled = false;
                    }
                    if (m_creatObject.TryGetComponent(out ProjectileBase pro))//补给舱
                    {
                        pro.Shoot(m_weapon,2);
                        pro.OnHit += PodHit;
                    }
                }
                //直接使用自定义物??
                else
                {
                    m_creatObject = Instantiate(data.cfg.creatObect, transform.position + Vector3.up * data.cfg.arriveHeight, transform.rotation).transform;
                    DontDestroyOnLoad(this);
                    if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
                    {
                        weapon.Owner = m_owner;
                    }
                    if (m_creatObject.TryGetComponent(out Animator anim))
                    {
                        anim.enabled = false;
                    }
                    if (m_creatObject.TryGetComponent(out ProjectileBase pro))//补给舱
                    {
                        pro.Shoot(m_weapon, 2);
                        pro.OnHit += PodHit;
                    }
                    if (m_creatObject.TryGetComponent(out Actor actor))
                    {
                        actor.Team = m_owner.GetComponent<I_Actor>().Team;
                        actor.Owner = m_owner.GetComponent<I_Actor>();
                    }
                }

            }
        }
        else if(data.State == AirdropState.Sustain && !m_creatObject.IsValid()&& data.time> 0.5f)
        {
            data.time = 0.5f;
            m_Lift.ResetLift(0.5f);
        }

    }


    void PodHit(ProjectileHitData hitData)
    {
        if (hitData.collider&&!LayerDefinition.GroundLayers.Contains(1<<hitData.collider.gameObject.layer))
        {
            //Debug.LogError("截获撞击的物体 "+ hitData.collider.gameObject + " 层级 "+hitData.collider.gameObject.layer +"  "+ System.Convert.ToString((1<<hitData.collider.gameObject.layer),2)+" 地面层级 " + System.Convert.ToString(LayerDefinition.GroundLayers.value,2), hitData.collider.gameObject);
            return;
        }
        AudioSvc.Stop("PodIntA_1");

        //AudioManager.PlaySound(new("AirDrop/PodDoor_Stop_1", transform.position, 50, AudioGroups.WeaponShoot));
        AudioSvc.PlaySound(new("AirDrop/SupplyPod/SupplyPodSpawnImpactCombinedA_1", hitData.pos, 50, AudioGroups.Weapon,0.25f));
            //直接在落地的时候就该创建而不是结束
            //Debug.LogError("落地位置 "+ m_creatObject.position+"标记位置"+ transform.position+"碰撞位置"+hitData.pos);
        m_creatObject.position = transform.position;
        
        if (m_creatObject.TryGetComponent(out Animator anim))
        {
            anim.enabled = true;
        }
        if (data.cfg.sustainHideBeacon)
        {
            for (int i = 1; i < 5; ++i)
            {
                transform.GetChild(i).gameObject.SetActive(false);//部分关闭
            }
        }
        if (data.cfg.useNormalPod)
        {
            if (data.cfg.permanentPod)
            {
                m_creatObject.GetComponent<LimitedLife>().ResetLift(9999);
            }
            if (m_creatObject.TryGetComponent(out ProjectileBase pro))
            {
                pro.OnHit -= PodHit;
            }
            //Debug.LogError("创建位置" + transform.position);
            //创建实际物体
            var go = m_creatObject=Instantiate(data.cfg.creatObect, transform.position + 0.2f * Vector3.up, transform.rotation).transform;
            if (go.TryGetComponentInChildren(out WeaponBaseController weapon))
            {
                weapon.Owner = m_owner;
            }
            if (go.TryGetComponent(out I_Actor actor))
            {
                actor.Team = m_owner.GetComponent<I_Actor>().Team;
                actor.Owner = m_owner.GetComponent<I_Actor>();
            }
        }
        else
        {
            if (m_creatObject.TryGetComponent(out ProjectileBase pro))
            {
                pro.OnHit -= PodHit;
            }
        }

        OnCreatObject?.Invoke(m_creatObject.gameObject);
    }

    void EndPod()
    {
        
    }

    #endregion

    #region 轰炸区

    void StartBomb()
    {
        //Destroy(m_creatObject.gameObject);

    }
    void UpdateBomb()
    {
        if (!m_creatObject)
        {
            if (data.State == AirdropState.Sustain)
            {
                m_creatObject = Instantiate(data.cfg.creatObect,transform.position, transform.rotation).transform;
                if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
                {
                    weapon.Owner = m_owner;
                }
                if (data.cfg.sustainHideBeacon)
                {
                    transform.ForEach(item => item.gameObject.SetActive(false));
                }
            }
        }
    }
    void EndBomb()
    {
        Tool.Destroy(m_creatObject.gameObject);
        transform.ForEach(item => item.gameObject.SetActive(true));

    }

    #endregion

    #region 飞鹰区

    void StartJet()
    {
        var size = data.cfg.showRange;
        Quaternion rotation= transform.rotation;
        if (size.y > 0&& size.x > size.y)//横向
        {
            rotation*=Quaternion.Euler(0, 90, 0);
        }
        //Debug.LogError("创建位置"+ transform.position);
        var go= VFXManager.Creat(eagle, transform.position, rotation, null).transform;
        m_creatObject = Instantiate(data.cfg.creatObect, go.TransformPoint(0,-5,-2), rotation,go).transform;
        if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
        {
            weapon.Owner = m_owner;
        }
        //重新设置引导物体的位置
        foreach (var item in m_creatObject.GetComponentsInChildren<GuidedShelling>())
        {
            item.transform.position = transform.position;
        }
        
        if (data.cfg.sustainHideBeacon)
        {
            transform.ForEach(item => item.gameObject.SetActive(false));
        }
        
        
    }
    void UpdateJet()
    {

    }
    void EndJet()
    {
        Tool.Destroy(m_creatObject.gameObject,2);
        transform.ForEach(item => item.gameObject.SetActive(true));
    }

    #endregion

    #region 运输机系

    void StartMedivac()
    {
        var size = data.cfg.showRange;
        Quaternion rotation = transform.rotation;
        var go = VFXManager.Creat(neoNimbusVehicle, transform.position, rotation, null).transform;
        var comp = data.cfg.creatObect.GetComponent<CharacterController> ();
        m_creatObject = Instantiate(data.cfg.creatObect, go.TransformPoint(0, -2.5f+comp.center.y-comp.height, 1.5f), rotation, go).transform;
        m_creatObject.GetComponent<CharacterController>().enabled = false;
        m_creatObject.GetComponent<BaseSelfController>().enabled = false;
        


        if (m_creatObject.TryGetComponentInChildren(out WeaponBaseController weapon))
        {
            weapon.Owner = m_owner;
        }

        if (data.cfg.sustainHideBeacon)
        {
            transform.ForEach(item => item.gameObject.SetActive(false));
        }


    }
    void UpdateMedivac()
    {
        //Debug.LogWarning("状态 "+ data.State+"父级 "+ m_creatObject.transform.parent);
        if (data.State == AirdropState.Sustain && m_creatObject.transform.parent!=null)
        {
            Debug.Log("卸载");
            m_creatObject.transform.parent = null;
            m_creatObject.GetComponent<CharacterController>().enabled = true;
            m_creatObject.GetComponent<BaseSelfController>().enabled = true;
        }
    }
    void EndMedivac()
    {
        transform.ForEach(item => item.gameObject.SetActive(true));
    }

    #endregion

    void SetDisplay()
    {
        AudioSvc.PlaySound(new ("AirDrop/superbeacon_impact",transform.position,60, AudioGroups.Weapon,0.5f));
        m_Lift = GetComponent<LimitedLife>();
        //Debug.LogError("持续时间"+ (data.cfg.arriveTime + data.cfg.sustainTime));
        m_Lift.SetLift(data.arriveTime + data.cfg.sustainTime);
        //var main = m_particle.main;
        //main.startLifetime = new(data.cfg.arriveTime + data.cfg.sustainTime);
        //main.duration = data.cfg.arriveTime + data.cfg.sustainTime;

        Color color = Color.LerpUnclamped(Color.white * data.cfg.Color.GetValue(), data.cfg.Color,1.7f);
        for (int i = 1; i < 5; ++i)
        {
            transform.GetChild(i).gameObject.SetActive(data.arriveTime>0);
        }
        transform.ForEach(item => SetColor(item, color));
        light.color = color;

        var size = data.cfg.showRange;
        //Debug.LogError("空袭的显示范??+size);
        if (size.x > 0 && size.y > 0)
        {//矩形
            if (size.y > size.x)
            {
                effectRangeCube.localEulerAngles = new(90, 0, 90);
                float tmp = size.y;
                size.y = size.x;
                size.x = tmp;
                effectRangeCube.localPosition = new(0, 0, size.x * 0.9f);
            }
            else
            {
                effectRangeCube.localEulerAngles = new(-90, 0, 0);
                effectRangeCube.localPosition = new(0, 0, 0);
            }
            effectRangeCube.gameObject.SetActive(true);
            effectRangeCircle.gameObject.SetActive(false);
            effectRangeCube.localScale = new(2 * size.x, 2 * size.y, size.x+size.y);
            //ebug.LogError("尺寸数据"+size+"实际数据"+ effectRangeCube.localScale);
        }
        else if (size.x > 0)
        {//圆形
            effectRangeCube.gameObject.SetActive(false);
            effectRangeCircle.gameObject.SetActive(true);
            effectRangeCircle.localScale = Vector3.one * size.x * 2;
        }
        else
        {
            effectRangeCube.gameObject.SetActive(false);
            effectRangeCircle.gameObject.SetActive(false);
        }
    }

    protected void TryWarning()
    {
        if (!data.cfg.useWarning||m_lastWarnTime + 10 > Time.time) return;
        bool meetWarn = InRange();
        if (meetWarn)
        {
            m_lastWarnTime = Time.time;
            WndManager.Instance.CreatNotice("Yuuka","Warning", InRange,vaildTime:5);
        }
    }
    private bool InRange()
    {
        if (GameRoot.GameState != GameStateEnum.Game||!ActorsManager.Player.IsValid()||!data.IsValid()) return false;
         Vector3 pos = ActorsManager.Player.transform.position;
        bool meetWarn = false;
        var size = data.cfg.showRange;
        if (size.x > 0 && size.y > 0)
        {
            Vector3 relativePos = transform.InverseTransformPoint(pos);
            if (Mathf.Abs(relativePos.x) < size.x && Mathf.Abs(relativePos.z) < size.y)
            {
                meetWarn = true;
            }
        }
        else if (size.x > 0)
        {//圆形
            if (Vector3.Distance(pos, transform.position) < size.x)
            {
                meetWarn = true;
            }
        }
        return meetWarn;
    }


    /// <summary>设置信标组件的颜色</summary>
    protected void SetColor(Transform transform,Color color) {
        if (transform.TryGetComponent(out ParticleSystem ps)) {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }
        else if (transform.TryGetComponent(out MeshRenderer mr)) {
            mr.SetColor(color);
        }
        else if (transform.TryGetComponent(out LineRenderer lr)) {
            Color.RGBToHSV(color,out float h, out float s, out float v);
            color = Color.HSVToRGB(h,1,1);
            lr.startColor = lr.endColor = color;
        }
    }
}
