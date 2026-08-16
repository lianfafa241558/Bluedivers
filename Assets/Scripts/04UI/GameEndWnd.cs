using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;
/// <summary>
/// 战后结算界面
/// </summary>
public class GameEndWnd : Window
{
    public const float _Dim = 0.3f;

    public Transform CreatActorPont;
    public Transform UIRoot;
    public Transform VictoryRoot,FailRoot,OtherRoot;
    public GameObject prefab, prefab2;

    public RectTransform topRoot, leftRoot, leftRoot2, rightRoot, middleRoot, button;

    Animator[] actors;

    bool IsExpend;
    float[] expendState;



    private void Start()
    {
        SetWndState(false);
        GameRoot.CreateTimer(() => {
            SetWndState(true);
        }, Time.deltaTime);

    }

    protected override void FirstShowWnd()
    {


        var task = taskManager.nowTask;
        var players = roomManager.players;
        int count = players.Count;
        AudioSvc.PlayMusic(task.result == GameResult.Victory ? AudioSvc.MusicGroup.End : AudioSvc.MusicGroup.Fail, 0.5f);
        switch (task.result)
        {
            case GameResult.Victory:
                SetActive(VictoryRoot, true);
                break;
            case GameResult.Failure:
                SetActive(FailRoot, true);
                break;
            default:
                SetActive(OtherRoot, true);
                break;
        }

        expendState = new float[count];
        actors = new Animator[count];

        for (int i = 0; i < count; i++)
        {
            float offset;
            if (count % 2 == 1) // 奇数情况
            {
                offset = count / 2;
            }
            else // 偶数情况
            {
                offset = (count / 2f) - 0.5f;
            }

            var showModle = resManager.CreatPrefab("Prefabs/StudentModle/" + roomManager.players[i].roleName, false);
            showModle.transform.parent = CreatActorPont;
            var scripts = showModle.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)//关闭注视等组件
            {
                script.enabled = false;
            }
            actors[i] = showModle.transform.GetComponent<Animator>();
            
            if(i==roomManager.SelfIndex) AudioSvc.PlaySound(Resources.Load<RoleData_SO>("GameData/Role/RD_" + roomManager.players[i].roleName).SpeechGroup(taskManager.nowTask.main.complete? SpeechTypeEnum.Victory: SpeechTypeEnum.Defeat).Get());

            var go = Instantiate(prefab, UIRoot).transform;
            GameRoot.CreateTimer(() => {
                //Debug.DrawRay(Camera.main.transform.position, Camera.main.ScreenPointToRay(RectTransformUtility.WorldToScreenPoint(null, go.position)).direction * 1,Color.red,5);
                //Debug.LogError("位置" + go.position + "坐标"+ RectTransformUtility.WorldToScreenPoint(null, go.position)+"方向"+ Camera.main.ScreenPointToRay(RectTransformUtility.WorldToScreenPoint(null, go.position)).direction);
                showModle.transform.position = Camera.main.transform.position+Camera.main.ScreenPointToRay(RectTransformUtility.WorldToScreenPoint(null, go.position)).direction * 6;
                showModle.transform.localPosition = showModle.transform.localPosition.Mult(new(1,0,1));
                showModle.transform.LookAt(Camera.main.transform.position);
                showModle.transform.localEulerAngles = new(0, showModle.transform.localEulerAngles.y, 0);
            }, Time.deltaTime);
            SetText(go.GetChild(0, 0, 1), task.BattleData[i]["击杀敌人"]);
            SetText(go.GetChild(0, 1, 1), task.BattleData[i]["开火次数"]);
            SetText(go.GetChild(0, 2, 1), task.BattleData[i]["死亡次数"]);
            SetText(go.GetChild(0, 3, 1), task.BattleData[i]["救援次数"]);

            SetSprite(go.GetChild(1, 1, 1), showModle.GetComponent<BaseObject>().Portrait);
            SetText(go.GetChild(1, 0, 0), showModle.GetComponent<BaseObject>().ShowName);
            SetText(go.GetChild(1, 0, 1), players[i].roleLevel);
            SetFill(go.GetChild(1, 2, 0, 0), players[i].roleExp);
            int a = i;
            SetCilck(go.GetChild(1,3),() => {
                PlaySount(5);
                for (int u = 0; u < UIRoot.childCount; ++u)
                {
                    SetPlayerInfoState(u, true,false);
                }
            });


            SetButtonEnter(go.GetChild(1, 2), (e) => {
                PlaySount(5);
                SetPlayerInfoState(a, true,true);
            });

            var infoRoot = go.GetChild(2);
            var kvs = task.BattleData[i].ToList();
            for (int u=0;u< kvs.Count; ++u)
            {
                var item = Instantiate(prefab2, infoRoot).transform;
                SetText(item.GetChild(0), kvs[u].Key);
                SetText(item.GetChild(1), kvs[u].Value);
                if (u % 2 == 0) SetColor(item,new(0,0,0,0.3f));

            }
            SetButtonExit(infoRoot, (e) => {
                PlaySount(6);
                SetPlayerInfoState(a, false,true);
            });
            //将折叠按钮挪到最后一个
            SetCilck(infoRoot.GetChild(0), () => {
                PlaySount(6);
                for (int u = 0; u < UIRoot.childCount; ++u)
                {
                    SetPlayerInfoState(u, false,false);
                }
            });
            infoRoot.GetChild(0).SetAsLastSibling();
            SetSizeDelta(infoRoot, GetSizeDelta(infoRoot).x,0);
        }
        InitOther();
        InitLeft();
        InitRight();
        InitMiddle();
        SaveRewards();
    }

    /// <summary>
    /// 结算保存：把本次任务采集的道具（欧帕兹）和货币（青辉石）累加进存档。
    /// 不限胜负，仅在结算界面首次显示时执行一次。
    /// </summary>
    private void SaveRewards()
    {
        var task = taskManager.nowTask;

        // 1. 保存采集的道具（OOPartEnum 资源）
        if (task.collectProperty != null)
        {
            foreach (var kvp in task.collectProperty)
            {
                propertyManager.SetCount(kvp.Key, kvp.Value);
            }
        }

        // 2. 保存货币（青辉石）：奖励分数 × (1 + 最终难度系数)
        float diffScale = taskManager.FinalDiffScale();
        int totalReward = task.MainReward + task.ExtraReward + task.NestReward;
        int money = (int)(totalReward * (1 + diffScale));
        if (money > 0)
        {
            propertyManager.SetCount(OOPartEnum.Pyroxene, money);
        }

        // 3. 增加本地玩家角色经验：任务奖励总和 / 5
        var self = roomManager.Self;
        if (self != null && !string.IsNullOrEmpty(self.roleName))
        {
            int exp = totalReward / 5;
            if (exp > 0)
            {
                ArchiveSvc.Archive.GainRoleExp(self.roleName, exp, out int newLevel, out var _);
                self.roleLevel = newLevel;
            }
        }

        ArchiveSvc.Archive.Save();
    }

    protected override void ShowWnd()
    {

        /*

        for (int i = 0; i < UIRoot.childCount; ++i)
        {
            var item = UIRoot.GetChild(i);
            if (i < roomManager.players.Count)
            {
                
                SetActive(item, true);
               

                SetText(item.GetChild(3, 0, 0), animators[i].GetComponent<BaseObject>().ShowName);
                SetText(item.GetChild(3, 0, 1), roomManager.players[i].roleLevel);
                SetSprite(item.GetChild(3, 1, 1), animators[i].GetComponent<BaseObject>().Portrait);
                for (int u = 0; u < 4; ++u)
                {
                    SetSprite(item.GetChild(5, u, 0), wndManager.empty);
                }
            }
            else
            {
                SetActive(item, false);
            }
                
        }*/

    }

    protected override void HideWnd()
    {

    }


    void SetPlayerInfoState(int index,bool state,bool hideButton)
    {

        if (Time.time < expendState[index] + 0.3f) return;
        expendState[index] = Time.time;
        //在全部展开的情况下，移出窗口不会收起
        if (IsExpend && hideButton) return;
        if (!hideButton)
        {
            IsExpend = state;
        }
        var root=UIRoot.GetChild(index);
        int width = (int)GetSizeDelta(root).x;
        int extraHeight = 10 + 40 * UIRoot.GetChild(index, 2).childCount;
        if (hideButton) extraHeight -= 50;
        int startY = !state ? 110 + extraHeight : 110;
        int targetY = state ? 110 + extraHeight : 110;
        SetSizeDelta(root,width, startY, width, targetY,200);
        int startX2 = (int)GetSizeDelta(root.GetChild(2)).x;
        SetSizeDelta(root.GetChild(2), startX2,state? 0 : extraHeight,startX2, !state ? 0 : extraHeight, 200);
        SetActive(root.GetChild(1,3),!state);
        LayoutRebuilder.ForceRebuildLayoutImmediate(UIRoot.GetRect());
    }



    void InitOther()
    {
        var task = taskManager.nowTask;
        var info = task.taskCfg;
        SetColor(topRoot.GetChild(0, 0), info.Color);
        SetSprite(topRoot.GetChild(0, 0), info.Sprite);
        SetText(topRoot.GetChild(1), task.MainCfg.name);
        SetText(topRoot.GetChild(2), info.name);
        SetCilck(button, () => {
            SetButtonInteractable(button, false);
            PlaySount(10);
            SetText(button.GetChild(0), "继续[0" + 5 + "]");
            GameRoot.CreateTimer((count) => {
                PlaySount(10);
                SetText(button.GetChild(0), "继续[0" + (5-count) + "]");
            },
            1,5,
            ()=>{
                GameState = GameStateEnum.Load;
                SetWndState(false);
                ResSvc.Instance.AsyncLoadScene("Utnapishitim", () => {
                    //GameState = GameStateEnum.Bridge;
                    //WindowState = WindowStateEnum.Game;
                    //GlobalEventManager.OnFakeBg(null);
                }, true);
            });
            //GameRoot.CreateTimer(ResManager.Instance.AsyncContinueLoadScene,8);
        });
    }

    void InitLeft()
    {
        var task = taskManager.nowTask;
        var info = task.taskCfg;

        SetColor(leftRoot.GetChild(1, 0, 0), info.Color);
        SetSprite(leftRoot.GetChild(1, 0, 0), info.Sprite);
        SetAlpha(leftRoot.GetChild(1, 0, 0), _Dim);

        for (int i = 0; i < leftRoot.GetChild(3).childCount; ++i)
        {
            if (i < info.extra.Length)
            {
                SetActive(leftRoot.GetChild(3, i), true);
                SetAlpha(leftRoot.GetChild(3, i), _Dim);
                SetSprite(leftRoot.GetChild(3, i), task.extras[i].cfg.sprite);
            }
            else
            {
                SetActive(leftRoot.GetChild(3, i), false);
            }
        }
        for (int i = 0; i < leftRoot.GetChild(5).childCount; ++i)
        {
            SetAlpha(leftRoot.GetChild(5, i, 0), _Dim);
            SetAlpha(leftRoot.GetChild(5, i, 1), _Dim);
            SetText(leftRoot.GetChild(5, i, 1), "");
        }

        for (int i = 0; i < 3; ++i)
        {
            SetAlpha(leftRoot2.GetChild(i), 0);
        }

        SetText(leftRoot2.GetChild(0, 0), task.MainReward);
        SetText(leftRoot2.GetChild(0, 1), task.MainReward / 5);
        SetText(leftRoot2.GetChild(1, 0), task.ExtraReward);
        SetText(leftRoot2.GetChild(1, 1), task.ExtraReward / 5);
        SetText(leftRoot2.GetChild(2, 0), task.NestReward);
        SetText(leftRoot2.GetChild(2, 1), task.NestReward / 5);
    }

    void InitRight()
    {
        var task = taskManager.nowTask;
        var info = task.taskCfg;

        SetActive(rightRoot.GetChild(1, 2), task.NestReward > 0);
        SetActive(rightRoot.GetChild(2, 2), task.NestReward > 0);
        SetActive(rightRoot.GetChild(1, 1), task.ExtraReward > 0);
        SetActive(rightRoot.GetChild(2, 1), task.ExtraReward > 0);

        SetText(rightRoot.GetChild(1, 0,1), 0);
        SetText(rightRoot.GetChild(1, 0, 1), 0);
        SetText(rightRoot.GetChild(1, 1, 1), 0);
        SetText(rightRoot.GetChild(2, 1, 1), 0);
        SetText(rightRoot.GetChild(2, 2, 1), 0);
        SetText(rightRoot.GetChild(2, 2, 1), 0);


        SetText(rightRoot.GetChild(0, 0), "");
        SetText(rightRoot.GetChild(0, 1), 0);
        for(int i = 0; i < 4; ++i)
        {
            SetAlpha(rightRoot.GetChild(0, 2, i),_Dim);
            SetText(rightRoot.GetChild(0, 2,i,0), "");
        }
       

    }


    void InitMiddle()
    {
        var task = taskManager.nowTask;
        var keys = task.collectProperty.Keys.ToList();
        for (int i=0;i<middleRoot.childCount;++i)
        {
            if (i < task.collectProperty.Count)
            {
                SetSprite(middleRoot.GetChild(i, 0), propertyManager.GetIcon(keys[i]));
                SetText(middleRoot.GetChild(i, 1), propertyManager.GetName(keys[i]));
                SetText(middleRoot.GetChild(i, 2), 0);
            }
            SetActive(middleRoot.GetChild(i),false);
        }


    }

    void SetTextGlowValue(Transform trans,int start,int value,int timeMs)
    {
        timeMs/=10;
        GameRoot.CreateTimer((count) => SetText(trans, (int)(start+(value- start) * (count/ (timeMs+0f)))),0.01f, timeMs, ()=> SetText(trans, value));
    }
    void PlaySount(int index)
    {
        AudioSvc.PlaySound(new(index switch{
            1=> "UI/UI_Bubble",
            2=> "UI/CleanUIB_1",
            3 => "UI/CleanUIC_1",
            4 => "UI/UI_TransmissionText",
            5 => "UI/UI_Notice",
            6 => "UI/UI_Button_Back",
            10=> "UI/UI_CountDown1",
            _ => "UI/UI_Bubble",
        }, AudioGroups.General, 1) { cache = true });
    }

    public IEnumerator DisplyLeft()
    {


        var task = taskManager.nowTask;
        var info = task.taskCfg;

        
        yield return new WaitForSeconds(0.6f);
        PlaySount(1);
        if (task.main.complete) SetAlpha(leftRoot.GetChild(1, 0, 0),1);
        

        yield return new WaitForSeconds(0.4f);
        PlaySount(1);
        for (int i = 0; i < info.extra.Length; ++i)
        {
            if (task.extras[i].complete) SetAlpha(leftRoot.GetChild(3, i),1);
            yield return null; 
        }

        yield return new WaitForSeconds(0.4f);
        PlaySount(1);
        for (int i = 0; i < task.nests.Length; ++i)
        {
            if (task.nests[i].Length > 0)
            {
                int count = task.nests[i].Count(item => item.complete);
                if (count == task.nests[i].Length)
                {
                    SetAlpha(leftRoot.GetChild(5, i, 0), 1);
                    SetAlpha(leftRoot.GetChild(5, i, 1), 1);
                }
                SetText(leftRoot.GetChild(5, i, 1), count + "/" + task.nests[i].Length);
            }
            else
            {
                SetActive(leftRoot.GetChild(5, i), false);
            }
            
        }
       
        yield return new WaitForSeconds(0.6f);
        PlaySount(3);

        float baseAlpha = task.main.complete ? 1 : _Dim;
        float[] baseAlpha2 = new float[info.extra.Length];
        for (int i = 0; i < task.extras.Length; ++i)
        {
            baseAlpha2[i]= task.extras[i].complete?1:_Dim;
            yield return null;
        }
        float[] baseAlpha3 = new float[task.nests.Length];
        for (int i = 0; i < task.nests.Length; ++i)
        {
            baseAlpha3[i] = task.nests[i].Count(item=>item.complete)== task.nests[i].Length ? 1 : _Dim;
            yield return null;
        }
        
        
        //主要奖励
        SetAlpha(leftRoot.GetChild(1, 0, 0), baseAlpha, 0.1f,200);
        SetAlpha(leftRoot2.GetChild(0),0,1,200);
        yield return new WaitForSeconds(0.2f);
        PlaySount(4);
        SetText(rightRoot.GetChild(1, 0, 1), 0, task.MainReward, 500);
        SetText(rightRoot.GetChild(2, 0, 1), 0, task.MainReward/5, 500);

        yield return new WaitForSeconds(0.2f);
        //额外奖励
        PlaySount(3);
        SetAlpha(leftRoot2.GetChild(1), 0,1,200);
        for (int i = 0; i < info.extra.Length; ++i)
        {
            SetAlpha(leftRoot.GetChild(3, i), baseAlpha2[i], 0.1f, 200);
        }
        yield return new WaitForSeconds(0.2f);
        PlaySount(4);
        SetText(rightRoot.GetChild(1, 1, 1), 0, task.ExtraReward, 500);
        SetText(rightRoot.GetChild(2, 1, 1), 0, task.ExtraReward / 5, 500);

        yield return new WaitForSeconds(0.2f);
        //巢穴奖励
        PlaySount(3);
        SetAlpha(leftRoot2.GetChild(2),0,1,200);
        for (int i = 0; i < task.nests.Length; ++i)
        {
            SetAlpha(leftRoot.GetChild(5, i, 0), baseAlpha3[i], 0.1f,200);
            SetAlpha(leftRoot.GetChild(5, i, 1), baseAlpha3[i], 0.1f ,200);
        }
        yield return new WaitForSeconds(0.2f);
        PlaySount(4);

        SetText(rightRoot.GetChild(1, 2, 1), 0, task.NestReward, 500);
        SetText(rightRoot.GetChild(2, 2, 1), 0, task.NestReward / 5, 500);

        yield return new WaitForSeconds(0.5f);
        PlaySount(2);
        //重置主要奖励
        SetAlpha(leftRoot.GetChild(1, 0, 0), 0.1f, baseAlpha, 200);
        SetAlpha(leftRoot2.GetChild(0), 1, 0, 200);

        

        yield return new WaitForSeconds(0.2f);
        PlaySount(2);
        //重置额外奖励
        SetAlpha(leftRoot2.GetChild(1), 1, 0, 200);
        for (int i = 0; i < info.extra.Length; ++i)
        {
            SetAlpha(leftRoot.GetChild(3, i), 0.1f,baseAlpha2[i], 200);
        }

        yield return new WaitForSeconds(0.2f);
        PlaySount(2);
        //重置巢穴奖励
        SetAlpha(leftRoot2.GetChild(2), 1, 0, 200);
        for (int i = 0; i < task.nests.Length; ++i)
        {
            SetAlpha(leftRoot.GetChild(5, i, 0), 0.1f, baseAlpha3[i], 200);
            SetAlpha(leftRoot.GetChild(5, i, 1), 0.1f, baseAlpha3[i], 200);
        }

    }


    public IEnumerator DisplyRight()
    {

        var task = taskManager.nowTask;
        var baseScale = (int)(taskManager.DiffScale(task.difficulty) * 100);
        var finScale = (int)(taskManager.FinalDiffScale() * 100);
        yield return new WaitForSeconds(0.6f);
        //难度奖励
        PlaySount(1);
        SetText(rightRoot.GetChild(0, 0), task.difficulty.ToString());
        PlaySount(4);
        SetTextGlowValue(rightRoot.GetChild(0, 1), 0, baseScale, 500);
        
        yield return new WaitForSeconds(0.7f);
        //额外难度奖励
        if (baseScale!=finScale)
        {
            PlaySount(1);
            PlaySount(4);
            SetTextGlowValue(rightRoot.GetChild(0, 1), baseScale, finScale, 500);
            for (int i = 0; i < 4; ++i)
            {
                if (task.ExtraDifficulty[i] > 0)
                {
                    SetAlpha(rightRoot.GetChild(0, 2, i), 1);
                    SetText(rightRoot.GetChild(0, 2, i, 0), Tool.IntToRoman(task.ExtraDifficulty[i]));
                }
                yield return new WaitForSeconds(0.15f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.6f);
        }
       
        
        yield return new WaitForSeconds(0.1f);
        PlaySount(1);
        //计算最终奖??
        for (int i = 0; i < 4; ++i)
        {
            if (task.ExtraDifficulty[i] > 0)
            {
                SetAlpha(rightRoot.GetChild(0, 2, i), 1);
                SetText(rightRoot.GetChild(0, 2, i, 0), Tool.IntToRoman(task.ExtraDifficulty[i]));
            }
        }


        var scale = 1+taskManager.FinalDiffScale();
        SetText(rightRoot.GetChild(1, 0, 1),  (int)(task.MainReward * scale));
        SetText(rightRoot.GetChild(2, 0, 1),  (int)(task.MainReward / 5 * scale));
        SetText(rightRoot.GetChild(1, 1, 1),  (int)(task.ExtraReward * scale));
        SetText(rightRoot.GetChild(2, 1, 1),  (int)(task.ExtraReward / 5 * scale));
        SetText(rightRoot.GetChild(1, 2, 1),  (int)(task.NestReward * scale));
        SetText(rightRoot.GetChild(2, 2, 1),  (int)(task.NestReward / 5 * scale));

    }


    public IEnumerator DisplyMiddle()
    {
        var task = taskManager.nowTask;
        var values = task.collectProperty.Values.ToList();
        for (int i = 0; i < task.collectProperty.Count; ++i)
        {
            SetActive(middleRoot.GetChild(i), true) ;
            SetText(middleRoot.GetChild(i, 2), 0, values[i], 400);
            yield return new WaitForSeconds(0.25f);
        }

    }

}
