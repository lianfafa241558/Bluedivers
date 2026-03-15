using System.Collections.Generic;
using System.Linq;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

namespace Unity.FPS.UI
{
    /// <summary>
    /// 制作图标连到锁定目标上(这个是ui，连线是另一个组件)
    /// </summary>
    public class SightLockToTarget : MonoBehaviour
    {
        [SerializeField]
        [CustomLabel("使用连线")]
        bool UseLine;
        [SerializeField]
        [CustomLabel("1层时显示文本")]
        bool OnceDisplyNumber;
        [SerializeField]
        GameObject targetPrefab;

        [SerializeField]
        [CustomLabel("使用填充计数器")]
        Transform FillImage;
        [SerializeField]
        [CustomLabel("每层对应的填充值")]
        float EachValue;

        CrosshairManager manager;
        protected WeaponPlayerController m_Weapons;

        Dictionary<I_Actor, (Transform,int)> dic;
        ObjectPool<Transform> pool;

        void Start()
        {
            dic = new();
            pool = new(_Add, _Pop, _Push,1);
            if (transform.TryGetComponentInParent(out manager)){
                manager.OnLockUpdate += LockUpdate;
                manager.OnLock += LockStateChange;
                manager.OnSwitchWeapon += Clear;
                m_Weapons = manager.m_Weapons;
            }
        }
        private void OnDestroy()
        {
            if (manager)
            {
                manager.OnLockUpdate -= LockUpdate;
                manager.OnLock -= LockStateChange;
                manager.OnSwitchWeapon -= Clear;
            }
            dic = null;
        }

        private void Update()
        {
            var keys = dic.Keys.ToArray();
            for (int i = 0; i < keys.Length; ++i)
            {
                var actor = keys[i];
                dic[actor].Item1.position = Camera.main.WorldToScreenPoint(actor.CenterPos);
                if (UseLine)
                {
                    UpdateLine(dic[actor].Item1.GetComponent<LineRenderer>(), actor.CenterPos);
                }
            }
        }

        protected void LockUpdate(I_Actor actor,bool state)
        {
            if (state)//添加锁定
            {
                //Debug.LogError("为" + actor + "添加锁定");
                if (dic.TryGetValue(actor, out var group))
                {
                    dic[actor] = (group.Item1, ++group.Item2);
                    if (group.Item2 > 1|| OnceDisplyNumber)
                    {
                        SetActive(group.Item1.GetChild(0), true);
                        SetText(group.Item1.GetChild(0), group.Item2);
                    }
                    //Debug.LogError(actor + "锁定"+ group.Item2+"层");
                }
                else
                {
                    var tr = pool.Get();
                    dic.Add(actor, (tr, 1));
                }
                if (FillImage.IsValid()) SetFill(FillImage, GetFill(FillImage)+EachValue);
            }
            else//移除锁定
            {
                //Debug.LogError("为" + actor + "移除锁定");
                if (dic.TryGetValue(actor, out var group))
                {
                    --group.Item2;
                    if (group.Item2 <=0)
                    {
                        pool.Release(group.Item1);
                        dic.Remove(actor);
                    }
                    else if (group.Item2 == 1&& !OnceDisplyNumber)
                    {
                        SetActive(group.Item1.GetChild(0), false);
                    }
                }
                if (FillImage.IsValid()) SetFill(FillImage, GetFill(FillImage) - EachValue);
            }

        }

        void Clear()
        {
            foreach (var item in dic)
            {
                pool.Release(item.Value.Item1);
            }
            dic.Clear();
            if (FillImage.IsValid()) SetFill(FillImage,0);
        }

        void LockStateChange(bool state)
        {
            Clear();
        }

        Transform _Add()
        {
            var tr = Instantiate(targetPrefab, transform).transform;
            SetActive(tr,false);
            return tr;
        }
        void _Push(Transform tr)
        {
            if (UseLine)
            {
                tr.GetComponent<LineRenderer>().positionCount = 0;
            }
            SetActive(tr,false);
        }
        void _Pop(Transform tr)
        {
            SetActive(tr, true);
            SetActive(tr.GetChild(0), OnceDisplyNumber);//显示叠层的文本
            SetText(tr.GetChild(0), 1);
        }

        Vector3[] posList = new Vector3[51];
        void UpdateLine(LineRenderer line, Vector3 end)
        {

            var start = m_Weapons.WeaponMuzzle;

            float speed = 20;
            float rotationSpeed =5;
            float paragraph = 1 / speed;//相当于飞行一米所需的时间
            int count = 1;
            posList[0] = start.position;
            Vector3 velocity = start.forward * speed;
            for (int i = 1; i < 50; ++i)
            {
                velocity = Vector3.Lerp(velocity,(end - posList[i - 1]).normalized * speed, rotationSpeed* paragraph);
                posList[i] = posList[i - 1] + velocity * paragraph;
                ++count;
                if (Vector3.Distance(posList[i],end)<1)
                {
                    //Debug.LogError("从"+ posList[i]+"到"+end+"距离"+ Vector3.Distance(posList[i], end));
                    ++count;
                    posList[i + 1] = end;
                    break;
                }
            }
            line.positionCount = count;
            //写入数据
            for (int i = 0; i < count; ++i)
            {
                line.SetPosition(i, posList[i]);
            }

        }
    }
}