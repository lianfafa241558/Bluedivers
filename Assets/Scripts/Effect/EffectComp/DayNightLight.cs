using UnityEngine;

public class DayNightLight : MonoBehaviour
{
    const float fullDayDuration = Constants.FullDayDuration;
    static float[] dayStageTime = Constants.DayStageTime;

    float currentTime = 0f; // 当前时间进度，范围为0-1
    [SerializeField]
    GameObject[] goArr;
    bool isNoon = false;


    void Awake()
    {
        // 获取当前本地时间（分钟 + 秒）
        System.DateTime now = System.DateTime.Now;
        
        float minuteSecond = 60 * now.Minute + now.Second;
        // 86400取模，作为初始时间
        currentTime = (minuteSecond % fullDayDuration) / fullDayDuration;

        InvokeRepeating(nameof(UpdateTimeState), 0f, 1);
    }



    void UpdateTimeState()
    {
        // 更新时间进度
        currentTime += 1 / fullDayDuration;
        currentTime = Mathf.Repeat(currentTime, 1f);

        if (isNoon&&(currentTime < dayStageTime[2] || currentTime > dayStageTime[5]))
        {
            isNoon = false;
            foreach (GameObject go in goArr)
            {
                go.SetActive(true);
            }
        } 
        else if (!isNoon&&(currentTime >= dayStageTime[2] && currentTime <= dayStageTime[5]))
        {
            isNoon = true;
            foreach(GameObject go in goArr)
            {
                go.SetActive(false);
            }
        }
    }

}
