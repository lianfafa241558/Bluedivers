using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 通过阶段进行一些操作(没办法用任务来控制的比如地狱火才这么搞)
/// </summary>
public class KeyScreenContGo : MonoBehaviour
{
    KeyScreen m_keyScreen;
    [SerializeField]
    List<ContGoInfo> contGoInfos;

    private void OnEnable()
    {
        m_keyScreen = GetComponent<KeyScreen>();
        m_keyScreen.OnUpdateStage += OnUpdateStage;
    }

    private void OnDisable()
    {
        m_keyScreen.OnUpdateStage -= OnUpdateStage;
        m_keyScreen = null;
    }

    void OnUpdateStage(int stage)
    {
        foreach (var item in contGoInfos)
        {
            if(item.stage == stage)
            {
                if (item.go) item.go.SetActive(item.state);
                //Debug.LogError("尝试触发语音" + item.speech + (item.speech != SpeechTypeEnum.Supply));
                if (item.speech != SpeechTypeEnum.Supply) GlobalEventManager.PlayMeetSoeech(m_keyScreen.owner, SpeechTypeEnum.HaloBomb);
            }

        }
    }


    [System.Serializable]
    struct ContGoInfo{
        public int stage;
        public GameObject go;
        public bool state;
        public SpeechTypeEnum speech;
        //以后用到了再加
    }
}
