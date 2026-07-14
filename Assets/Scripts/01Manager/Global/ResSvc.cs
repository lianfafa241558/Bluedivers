using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Interface;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResSvc: Singleton<ResSvc>, I_GlobaManager
{
    private static Dictionary<string, AudioClip> adDic = new();//缓存音频资源
    private static Dictionary<string, GameObject> goDic = new();//缓存游戏物体
    private static Dictionary<string, Sprite> sprDic = new();//缓存纹理
    public static Dictionary<int, AirdropData_SO> airdropDic;//缓存空投
    public static Dictionary<string, NoticeTree_SO> voiceDic;//缓存台词
    public static Dictionary<DamageTypeEnum, AboStateData_SO> aboStateDic;//缓存异常状态


    public void Init()
    {
        Awake();
        airdropDic = LoadObjects<AirdropData_SO>("GameData/Airdrop").ToDictionary(item => item.ID);
        var list = LoadObjects<NoticeTree_SO>("GameData/NoticeTree").Where(item=>item.UseResLoad);
        voiceDic = new();
        foreach (var role in list)
        {
            voiceDic.Add(role.ID, role);
        }
        aboStateDic = LoadObjects<AboStateData_SO>("GameData/AboState").ToDictionary(item => item.typeEnum);
    }
    public void UnInit()
    {
        
    }

    //private static Action prgCB = null;
    public static AsyncOperation AsyncOp = null;
    //private static Action prgCB = null;

    /// <summary>创建游戏物体</summary>
    public GameObject CreatPrefab(string path, bool cache = false, Vector3 pos = default(Vector3))
    {
        var prefab = LoadRes(path, goDic, cache);
        GameObject go = null;
        if (prefab != null)
        {
            go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
        }
        return go;
    }

    /// <summary>创建UI</summary>
    public GameObject CreatPrefabUI(string path, Transform root, bool cache = false)
    {
        var prefab = LoadRes(path, goDic, cache);
        GameObject go = null;
        if (prefab != null)
        {
            go = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
        }
        return go;
    }


    /// <summary>加载游戏物体(Prefabs/)</summary>
    public GameObject LoadPrefab(string path, bool cache = false) => LoadRes("Prefabs/" + path, goDic, cache);

    /// <summary>加载纹理(Image/)</summary>
    public Texture2D LoadTexture2D(string path, bool cache = false) => LoadRes<Texture2D>("Image/" + path);
    /// <summary>加载纹理(Image/)</summary>
    public Sprite LoadSprite(string path, bool cache = false) => LoadRes("Images/" + path, sprDic, cache);
    /// <summary>加载音频(Audio/)</summary>
    public AudioClip LoadAudio(string path, bool cache = false) => LoadRes("Audio/" + path, adDic, cache);

    public T LoadRes<T>(string path, Dictionary<string, T> dic = null, bool cache = false) where T : UnityEngine.Object
    {
        T res;
        if (dic != null)
        {
            if (!dic.TryGetValue(path, out res))
            {
                res = Resources.Load<T>(path);
                if (cache) dic.Add(path, res);
                if (!res)
                {
                    Debug.LogError("没有找到"+ path);
                }
                return res;
            }
            else return res;
        }
        return Resources.Load<T>(path);
    }

    private bool allowSkip;
    private bool useExtraProgress;
    private float extraProgress;//[0-1f]
    private bool sceneLoadedFlag;
    private Action sceneLoadedCallback;
    /// <summary>异步加载场景</summary>
    /// <param name="mapName">场景名/param>
    /// <param name="loaded">加载完成回调</param>
    public void AsyncLoadScene(string mapName, Action loaded, bool showLoadWnd = false,bool waitExtra=false, bool allowSkip=false)
    {
        AsyncOp = SceneManager.LoadSceneAsync(mapName, LoadSceneMode.Single);
        useExtraProgress = showLoadWnd;
        this.allowSkip = allowSkip;
        extraProgress = waitExtra?0:100;
        sceneLoadedFlag = false;
        sceneLoadedCallback = loaded;
        if (showLoadWnd)
        {
            AsyncOp.allowSceneActivation = false;
        }
        // 在 completed 回调中只标记完成，实际初始化延后到下一帧
        AsyncOp.completed += _ => {
            sceneLoadedFlag = true;
        };
    }

    private void Update()
    {
        if (!sceneLoadedFlag) return;
        sceneLoadedFlag = false;

        // 通知场景变更，触发各系统初始化
        GlobalEventSub.SceneChange(SceneManager.GetActiveScene().name);

        // 将回调延后到下一帧执行，让场景中所有 Awake/Start 先完成
        StartCoroutine(DelayedInvoke(sceneLoadedCallback));
        sceneLoadedCallback = null;
        AsyncOp = null;
    }

    private System.Collections.IEnumerator DelayedInvoke(Action callback)
    {
        yield return null;
        callback?.Invoke();
    }

    public void SetLoadSceneExtraProgress(float progress)
    {
        extraProgress = progress;
    }

    public void AsyncContinueLoadScene()
    {
        if (AsyncOp!=null) AsyncOp.allowSceneActivation = true;
    }
    public bool AsyncAllowSkip()
    {
        return AsyncOp!=null && allowSkip;
    }

    public int AsyncLoadSceneProgress()
    {
        if (AsyncOp != null)
        {
            if (useExtraProgress) return ((int)(AsyncOp.progress * 124) + (int)(extraProgress *100)) / 2;
            else return (int)(AsyncOp.progress * 124);
        }
        return 0;
    }
    public AsyncOperation UnLoadScene()
    {
       return SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene().name);
    }
    /// <summary>
    /// 无前缀
    /// </summary>
    public List<T> LoadObjects<T>(string path)
        where T : UnityEngine.Object
    {
        return Resources.LoadAll<T>(path).ToList();
    }
    /// <summary>
    /// 无前缀
    /// </summary>
    public T LoadObject<T>(string path)
        where T : UnityEngine.Object
    {
        return Resources.Load<T>(path);
    }

    /// <summary>
    /// 无前缀
    /// </summary>
    public static List<T> StaticLoadObjects<T>(string path)
        where T : UnityEngine.Object
    {
        return Resources.LoadAll<T>(path).ToList();
    }

    public AirdropData_SO GetAirdrop(int id)
    {
        if(airdropDic.TryGetValue(id,out var re))
        {
            return re;
        }
        else
        {
            Debug.LogError("找不到ID"+id+"战备");
            return null;
        }
    }
    public void GetVoice(string role,string type,out SoundGroup_SO data,out string sourceName,out Sprite portrait)
    {
        if (voiceDic.TryGetValue(role, out var re))
        {
            data = re.sounds.Find(item => item.groupName == type);
            sourceName = re.SourceName;
            portrait = re.Portrait;
        }
        else
        {
            Debug.LogError("找不到角色为" + role + "台词");
            data = null;
            sourceName = null;
            portrait = null;
        }
    }
    /*
    public CampData_SO GetCamp(EnemyVarietyType type)
    {
        if (campDic.TryGetValue(type, out var re))
        {
            return re;
        }
        else
        {
            Debug.LogError("找不到ID为" + type + "阵营");
            return null;
        }
    }*/
    /*
    public static List<AudioClip> GetStreamAudios(string path)
    {
        List<AudioClip> list = new();
        string folderPath = Path.Combine(Application.streamingAssetsPath, path);

        Debug.LogWarning("A "+ folderPath);
        //string filePathB = "file:///" + Application.streamingAssetsPath + "/" + path;
        //Debug.LogWarning("B " + filePathB);
        string[] files = Directory.GetFiles(folderPath);
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            // 检查文件名是否以.meta 结尾
            if (!fileName.EndsWith(".meta"))
            {
                //Debug.LogWarning(fileName);
                list.Add(GetStreamAudio(path+"/"+fileName));
            }
        }
        return list;

    }

    public static AudioClip GetStreamAudio(string path)
    {
        string filePath = "file:///" + Application.streamingAssetsPath + "/" + path;
        UnityWebRequest requrest = UnityWebRequest.Get(filePath);
        Debug.LogWarning(filePath + "开始www" + requrest);
        var operation = requrest.SendWebRequest();
        while (!operation.isDone) ;
        if (requrest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(requrest.error);
            return null;
        }
        return DownloadHandlerAudioClip.GetContent(requrest);
    }
    
    IEnumerator DownloadAudio(string url)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = audioClip;
                audioSource.Play();
            }
        }
    }*/


}
