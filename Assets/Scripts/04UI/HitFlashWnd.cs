using System.Collections;
using System.Collections.Generic;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.UI;
using Utils;

public partial class PlayerWnd
{
    #region 受击

    [Foldout("受击", true)]

    [CustomLabel("护盾受击闪光")]
    public CanvasGroup FlashCanvasGroupShield;
    [CustomLabel("血量受击闪光")]
    public CanvasGroup FlashCanvasGroupHp;

    [CustomLabel("预警闪光")]
    public CanvasGroup FlashCanvasGroupWarning;

    public AnimationCurve WarningCurve;


    [CustomLabel("血量边缘效果")]
    public CanvasGroup VignetteCanvasGroup;

    [CustomLabel("濒死心跳声")]
    public AudioSource DyingAudio;

    [CustomLabel("边缘效果的最大透明度")]
    public float CriticaHealthVignetteMaxAlpha = .8f;

    [CustomLabel("濒死边缘效果脉动频率")]
    public float PulsatingVignetteFrequency = 4f;

    [CustomLabel("受击闪光的持续时间")]
    public float DamageFlashDuration;

    [CustomLabel("受击闪光的最大透明度")]
    public float DamageFlashMaxAlpha = 1f;

    #endregion

    #region 受击方向
    [Foldout("受击方向", true)]
    [CustomLabel("受击方向的显示持续时间")]
    public float HitDirDuration = 4f;

    HitData[] m_hitGroup;
    #endregion


    void InitHitFlash()
    {

        var Images = transform.Find("HitDirGroup").GetComponentsInChildren<Image>();
        m_hitGroup = new HitData[Images.Length];
        for (int i=0;i< Images.Length;++i)
        {
            m_hitGroup[i] = new() {
                image= Images[i]
            };
        }
    }

    /// <summary>
    /// 更新受击闪光
    /// </summary>
    void UpdateFeedback()
    {
        //血低变暗
        if (m_Health.GetHpRatio() < 0.3f)
        {
            VignetteCanvasGroup.gameObject.SetActive(true);
            float vignetteAlpha =
                (1 - ((m_Health.CurrentHealth / m_Health.MaxHealth).RawFloat /
                      0.3f)) * CriticaHealthVignetteMaxAlpha;

            VignetteCanvasGroup.alpha =
                ((Mathf.Sin(Time.time * PulsatingVignetteFrequency) / 4) + 0.75f) * vignetteAlpha;

            DyingAudio.volume = vignetteAlpha * 0.6f;
        }
        else
        {
            VignetteCanvasGroup.gameObject.SetActive(false);
        }

        //受击闪光
        if (m_FlashActive)
        {

            float normalizedTimeSinceDamage = (Time.time - m_LastTimeFlashStarted) / DamageFlashDuration;

            if (normalizedTimeSinceDamage < 1f)
            {
                float flashAmount = DamageFlashMaxAlpha * (1f - normalizedTimeSinceDamage);
                FlashCanvasGroupShield.alpha = flashAmount * m_Health.GetShieldRatio();
                if (m_Health.GetShieldRatio() <= 0)
                {
                    FlashCanvasGroupHp.alpha = flashAmount * (1.2f - m_Health.GetHpRatio());
                }
            }
            else
            {
                FlashCanvasGroupShield.alpha = 0f;
                FlashCanvasGroupHp.alpha = 0f;
                //FlashCanvasGroupShield.gameObject.SetActive(false);
                //FlashCanvasGroupHp.gameObject.SetActive(false);
                m_FlashActive = false;
            }
        }

        //攻击预警
        if (m_WarningActive)
        {
            float normalizedTimeSinceDamage = (Time.time - m_LastTimeWarningStarted) / DamageFlashDuration;
            if (normalizedTimeSinceDamage < 1f)
            {
                float flashAmount = DamageFlashMaxAlpha * (1f - normalizedTimeSinceDamage);
                FlashCanvasGroupWarning.alpha = flashAmount;
            }
            else
            {
                m_WarningActive = false;
            }
        }
        //受击方向
        for (int i = 0; i < m_hitGroup.Length; ++i)
        {
            if (m_hitGroup[i].image.fillAmount > 0.01f)
            {
                Follow(m_hitGroup[i]);
            }
        }
    }


    bool m_FlashActive;
    float m_LastTimeFlashStarted = Mathf.NegativeInfinity;

    bool m_WarningActive;
    float m_LastTimeWarningStarted = Mathf.NegativeInfinity;

    void OnTakeDamage(GameObject damageSource, Vector3 pos)
    {
        ResetFlash();
        if(damageSource) SetHitDir(damageSource.transform.position);
        if (m_Health.GetHpRatio() < 0.3f)
        {
            DyingAudio.Play();
        }
    }

    void OnHealed(PEInt amount)
    {
        if (m_Health.GetHpRatio() >= 0.3f)
        {
            DyingAudio.Stop();
        }
    }
    void BulletHit(GameObject source, Vector3 pos)
    {
        if (source && source != m_Controller.gameObject && Vector3.Distance(pos, m_Controller.CenterPos) < 3) ResetWarning();
    }



    void ResetFlash()
    {
        m_LastTimeFlashStarted = Time.time;
        m_FlashActive = true;
        FlashCanvasGroupShield.alpha = 0f;
        //FlashCanvasGroupShield.gameObject.SetActive(true);
        FlashCanvasGroupHp.alpha = 0f;
        //FlashCanvasGroupHp.gameObject.SetActive(true);
    }

    void ResetWarning()
    {
        m_LastTimeWarningStarted = Time.time;
        //如果同一帧玩家挨揍了就不出
        if (m_LastTimeFlashStarted == m_LastTimeWarningStarted) return;
        m_WarningActive = true;
        FlashCanvasGroupWarning.alpha = 0f;
    }
    void SetHitDir(Vector3 pos)
    {
        int minIndex=-1;
        var basePos = m_Controller.PlayerCamera.transform.position;
        for (int i = 0; i < m_hitGroup.Length; ++i)
        {
            //角度接近的
            if (Vector3.Dot((pos- basePos).normalized, (m_hitGroup[i].pos - basePos).normalized) >0.9f)
            {
                minIndex = i;
                break;
            }
        }
        if (minIndex == -1)
        {
            for (int i = 0; i < m_hitGroup.Length; ++i)
            {
                //时间到了的
                if (m_hitGroup[i].time < Time.time - HitDirDuration)
                {
                    minIndex = i;
                    break;
                }
                else if (minIndex == -1 || m_hitGroup[i].time < m_hitGroup[minIndex].time)
                {
                    minIndex = i;
                }
            }
        }
        //不存在=-1的情况

        m_hitGroup[minIndex].Reset(pos);

    }


    void Follow(HitData data)
    {

        Vector3 point = data.pos;
        point.y = Mathf.Max(point.y, m_Controller.PlayerCamera.transform.position.y);
        var center = Tool.ScreenSize * 0.5f;
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPosition = m_Controller.PlayerCamera.WorldToScreenPoint(point);
        screenPosition *= Mathf.Sign(screenPosition.z);
        screenPosition.z = 0;

        //(从屏幕中点到目标的)差值
        Vector3 dir = (screenPosition - center).normalized;
        // 设置image的旋转方向与dir一致
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        data.image.transform.rotation = Quaternion.Euler(0, 0, angle+90);

        data.image.transform.position = center+dir*200;
        data.image.fillAmount= Mathf.Clamp01(2-2*(Time.time-data.time)/ HitDirDuration);
        data.image.color=new(1,1,1, 0.8f*Mathf.Clamp01(1.3f - (Time.time - data.time) / HitDirDuration));
    }


    class HitData
    {
        public Image image;
        public Vector3 pos;
        public float time;

        public void Reset(Vector3 pos)
        {
            image.fillAmount = 1;
            this.pos = pos;
            time = Time.time;
        }
    }
}
