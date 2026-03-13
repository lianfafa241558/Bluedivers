using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
/// <summary>
/// CG控制器，总之就是很后悔没有用时间线插件
/// </summary>
public class PlotSubtitles : MonoBehaviour
{
    public List<SubtitlesInfo> subtitlesInfos=new();
    public List<Material> newSkybox;

    private TextMeshProUGUI subtitle;
    private CanvasGroup group;
    private RectTransform bg;
    private new AudioSource audio;

    private float time = 0;
    public float startTime=0.7f;
    public AudioSource Bgm;
    void Start()
    {
        group = GameObject.Find("SubtitleBG").GetComponent<CanvasGroup>();
        bg = GameObject.Find("Subtitle").GetComponent<RectTransform>();
        subtitle = bg.GetComponent<TextMeshProUGUI>();
        audio = gameObject.AddComponent<AudioSource>();
        GetComponent<Animator>().Play("Plot0",0, startTime);
        float halfLength = 91.65f * startTime;
        Bgm.time = halfLength;
        Bgm.Play();
    }


    void Update()
    {
        group.alpha = time -= (Time.deltaTime) / 1.5f;
    }



    private void SetSubtitle(int id) {
        var item = subtitlesInfos.Find(item=>item.id==id);
        
        if (item.desc.IsValid()) {
            time = 1.5f + item.desc.Length / 6f;
            subtitle.text = item.desc;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(bg);
        }
        audio.PlayOneShot(item.clip);
    }

    private void SetSkyBox(string name) {
        RenderSettings.skybox = newSkybox.Find(item=>item.name == name);
    }
    
    private void SetFog(int state) {
        RenderSettings.fog = state>0;
    }

    private Animator tmpAnim;
    private void SetAnimObj(string name) {
        tmpAnim = transform.Find(name).GetComponent<Animator>();
    }
    private void SetAnimName(string name) {
        tmpAnim.Play(name);
    }
    private void Destroy(string name) {
        //Debug.LogError("移除了"+ transform.Find(name).gameObject);
        Destroy(transform.Find(name).gameObject);
    }

    [System.Serializable]
    public class SubtitlesInfo {
        public int id;
        public string desc;
        public AudioClip clip;
    }
}
