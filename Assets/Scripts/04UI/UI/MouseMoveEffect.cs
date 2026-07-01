using Core;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class MouseMoveEffect : MonoBehaviour
{
    //public Camera UICamera;
    public TrailRenderer trail;
    public ParticleSystem particle;
    public ParticleSystem clickA, clickB;
    //public SpriteRenderer sprite;
    //public Material baseMaterial;
    private float resetEmitting;

    public Vector2 size,input;
    void Awake()
    {
        //WndManager.OnWindowStateChange += OnWindowStateChange;
        GlobalEventSub.OnTimeScaleChange += OnTimeScaleChange;
        //GlobalEventManager.OnFakeBg += OnFakeBG;
    }

    private void OnDestroy()
    {
        //WndManager.OnWindowStateChange -= OnWindowStateChange;
        GlobalEventSub.OnTimeScaleChange -= OnTimeScaleChange;
        //GlobalEventManager.OnFakeBg -= OnFakeBG;
    }

    void LateUpdate()
    {
        float cameraScale = Tool.ScreenSize2D.y / 1080f;
        size = Tool.ScreenSize*0.5f;
        input = Input.mousePosition;
        if (resetEmitting!=0)
        {
            var emiss = particle.emission;
            emiss.rateOverDistance = 0.01f;
            resetEmitting = 0;
        }
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = (Input.mousePosition - 0.5f * Tool.ScreenSize)/ cameraScale;
            transform.localPosition = new Vector3(pos.x, pos.y, 5f);
            trail.emitting = true;
            trail.Clear(); // 清除轨迹
            var emiss = particle.emission;
            emiss.enabled = true;
            resetEmitting = emiss.rateOverDistance.constant;
            emiss.rateOverDistance = 0f;


            emiss = clickA.emission;
            emiss.enabled = false;
            emiss = clickB.emission;
            emiss.enabled = false;
        }
        else if(Input.GetMouseButton(0))
        {
            Vector2 pos = (Input.mousePosition - 0.5f * Tool.ScreenSize)/ cameraScale;
            transform.localPosition = new Vector3(pos.x, pos.y, 5f);
        }
        else if(Input.GetMouseButtonUp(0))
        {
            Stop();
        }


    }
    private void OnDisable()
    {
        Stop();
    }

    /*
    private void OnFakeBG(Transform trans)
    {
        //Debug.LogWarning("执行");
        if (trans == null)
        {
            //Debug.LogWarning("关闭");
            //不能直接关是因为还有鼠标点击的特
            //UICamera.gameObject.SetActive(false);
            SetActive(sprite.transform,false);
            UICamera.depth = -1;
            return;
        }
        UICamera.depth = 1;
        float cameraScale = Tool.ScreenAspect;
        SetActive(sprite.transform, true);
        var image = trans.GetComponent<UnityEngine.UI.Image>();
        sprite.sprite = image.sprite;

        Debug.LogWarning("目标缩放"+ trans.localScale+"图片缩放" + sprite.sprite.rect.size+"镜头缩放"+ cameraScale);
        //(3440/1.79)=1920 *100/960 *1.1=220;
        //(1440/1.33)=1080 *100/540 *1.1=220;

        sprite.transform.localScale = new Vector3(1080* cameraScale, 1080,1) / sprite.sprite.rect.size * 100 * trans.localScale;
        //sprite.transform.localScale =new Vector3(Tool.ScreenSize2D.x / cameraScale.x  * trans.localScale.x * 100 / sprite.sprite.rect.size.x , Tool.ScreenSize2D.y / cameraScale.y * trans.localScale.y *100/ sprite.sprite.rect.size.y,1);

        sprite.material = image.material ?? baseMaterial;
        sprite.color = image.color;

        //复制镜头位移
        var fromComp= trans.GetComponent<FollowMouseMovement>();
        var toComp = sprite.GetComponent<FollowMouseMovement>();
        sprite.transform.localPosition = Vector3.forward * 998;
        if (fromComp)
        {
            toComp.enabled = true;
            toComp.Speed = fromComp.Speed;
            toComp.Offest = fromComp.Offest;
        }
        else
        {
            toComp.enabled = false;
        }
    }*/
    /*
    private void OnWindowStateChange(WindowStateEnum oldSstate, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                //Debug.LogError("UICamera:"+ UICamera,gameObject);
                UICamera.gameObject.SetActive(false);
                break;
            case WindowStateEnum.UI:
                UICamera.gameObject.SetActive(true);
                Stop();
                break;
            case WindowStateEnum.Airdrop:

                break;
        }
    }
    */
    private void OnTimeScaleChange(float oldScale,float newScale)
    {
        trail.time *= (newScale / oldScale);
    }

    private void Stop()
    {
        trail.emitting = false;
        var emiss = particle.emission;
        emiss.enabled = false;
        emiss = clickA.emission;
        emiss.enabled = true;
        emiss = clickB.emission;
        emiss.enabled = true;
        clickB.Play(false);
        clickA.Play(false);
    }
 
}
