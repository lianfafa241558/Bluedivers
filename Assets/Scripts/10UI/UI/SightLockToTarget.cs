using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;

using FPSGame.Game;
using FPSGame.Gameplay;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Weapon;
using static FPSGame.WndTools.WndRootTool;

namespace FPSGame.UI
{
    /// <summary>
    /// 制作图标连到锁定目标,这个是ui，连线是另一个组件
    /// </summary>
    [AddComponentMenu("UI/HUD/锁定标记")]
    public class SightLockToTarget : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("使用连线")]
        bool UseLine;
        [SerializeField]
        [InspectorName("1层时显示文本")]
        bool OnceDisplyNumber;
        [SerializeField]
        GameObject targetPrefab;

        [SerializeField]
        [InspectorName("使用填充计数器")]
        Transform FillImage;
        [SerializeField]
        [InspectorName("每层对应的填充值")]
        float EachValue;

        CrosshairManager manager;
        protected WeaponPlayerController m_Weapons;

        Dictionary<IActor, (Transform,int)> dic;
        ObjectPool<Transform> pool;

        void Start()
        {
            EnsureInit();
        }

        /// <summary>
        /// 容器只建一次。<c>Bind</c> 会早于本组件 <c>Start</c>（CrosshairManager 在 Instantiate 后立刻绑定，
        /// 而 Start 要等本帧末尾）⇒ 期间任何事件回调直接用 <see cref="dic"/> 都是 NRE，必须先建好。
        /// </summary>
        void EnsureInit()
        {
            if (dic != null) return;
            dic = new();
            pool = new(_Add, _Pop, _Push,1);
        }

        /// <summary>
        /// 由 <see cref="CrosshairManager"/> 创建/切枪准星时显式调用。
        /// <para>⚠ 别改回在 Start 里 GetComponentInParent：准星实例化后同一帧就会被 SetParent 到 ExchangeArea
        /// （y=-1000 的**活跃**隐藏区），Start 在那里执行 ⇒ 父链上没有管理器、事件一个都订阅不到，
        /// 表现就是"锁定照常生效但图标和连线全没了"（2026-11-23 实测 manager 为 CLR null、订阅者 0）。</para>
        /// </summary>
        public void Bind(CrosshairManager owner)
        {
            if (!owner) return;
            EnsureInit();       // 订阅后随时可能收到 OnSwitchWeapon → Clear，不能等 Start
            if (owner != manager)
            {
                Unbind();
                manager = owner;
                manager.OnLockUpdate += LockUpdate;
                manager.OnLock += LockStateChange;
                manager.OnSwitchWeapon += Clear;
            }
            // 换枪后管理器手上已是新武器，旧引用留着会让连线从上一把枪的枪口出发
            m_Weapons = manager.m_Weapons;
        }

        void Unbind()
        {
            if (!manager) return;
            manager.OnLockUpdate -= LockUpdate;
            manager.OnLock -= LockStateChange;
            manager.OnSwitchWeapon -= Clear;
            manager = null;
        }

        private void OnDestroy()
        {
            Unbind();
            dic = null;
        }

        private void Update()
        {
            if (dic.Count == 0) return;

            // ⚠ 两处老写法都换掉：`Camera.main` 挑到哪台不确定；`Keys.ToArray()` 每帧一份数组（锁定数个就多大）
            Camera cam = GameplayCamera.Current;
            if (cam == null) return;

            foreach (var kv in dic)
            {
                IActor actor = kv.Key;
                kv.Value.Item1.position = cam.WorldToScreenPoint(actor.CenterPos);
                if (UseLine)
                {
                    UpdateLine(kv.Value.Item1.GetComponent<LineRenderer>(), actor.CenterPos);
                }
            }
        }

        protected void LockUpdate(IActor actor,bool state)
        {
            if (state)//添加锁定
            {
                //Debug.LogError("给 " + actor + "添加锁定");
                if (dic.TryGetValue(actor, out var group))
                {
                    dic[actor] = (group.Item1, ++group.Item2);
                    if (group.Item2 > 1|| OnceDisplyNumber)
                    {
                        SetActive(group.Item1.GetChild(0), true);
                        SetText(group.Item1.GetChild(0), group.Item2);
                        if (group.Item1.gameObject.activeInHierarchy&&group.Item1.TryGetComponent(out Animator anim)) anim.SetTrigger(Constants.k_AnimEntry);
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
                //Debug.LogError("给 " + actor + "移除锁定");
                if (dic.TryGetValue(actor, out var group))
                {
                    // ⚠ 元组是值类型：旧的 `--group.Item2` 只减了局部副本、从不写回字典 ⇒ 层数只增不减
                    int left = group.Item2 - 1;
                    if (left <= 0)
                    {
                        pool.Release(group.Item1);
                        dic.Remove(actor);
                    }
                    else
                    {
                        dic[actor] = (group.Item1, left);
                        if (left == 1 && !OnceDisplyNumber) SetActive(group.Item1.GetChild(0), false);
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
            if (manager) m_Weapons = manager.m_Weapons;
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
            if (line == null) return;
            // 切枪瞬间可能还没拿到新武器（Clear 只在事件里刷新）⇒ 这一帧不画线，别 NRE
            if (!m_Weapons.IsValid()) return;

            var start = m_Weapons.GetMuzzle(0);
            if (!start) return;

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
                    //Debug.LogError("从 "+ posList[i]+"到 "+end+"距离"+ Vector3.Distance(posList[i], end));
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
