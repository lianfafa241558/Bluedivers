using FPSGame.Core;
using FPSGame.Core.Interface;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using FPSGame.Gameplay;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.Managers
{



/// <summary>
/// 特效与子弹的对象池管理，提供生成与回收入口。
/// </summary>
[AddComponentMenu("管理/特效管理器")]
public class VFXManager : Singleton<VFXManager> , I_GlobaManager, IVfxSink
{
    /// <summary><see cref="IVfxSink"/> 实现：转发到本类的静态入口，供 05_UnitCore/06_Gameplay 等下层经
    /// <see cref="VfxPool"/> 调用（2026-10-01 取代原 IVfxService + ServiceLocator.Vfx 槽）。</summary>
    GameObject IVfxSink.Creat(GameObject tmp, Vector3 pos, Quaternion rotation, Transform parent) => Creat(tmp, pos, rotation, parent);
    void IVfxSink.Release(GameObject go) => Release(go);

    /// <summary>按组件模板池化（目前只支持 ProjectileBase —— 武器抛射物池）。</summary>
    T IVfxSink.Creat<T>(T template, Vector3 pos, Quaternion rotation)
    {
        if (template is ProjectileBase pb) return Creat(pb, pos, rotation) as T;
        Debug.LogError("IVfxSink.Creat<T> 只支持 ProjectileBase 模板：" + template);
        return null;
    }

    /// <summary>回收组件实例：抛射物回抛射物池，其余回普通池（避免进错池）。</summary>
    void IVfxSink.Release(Component instance)
    {
        if (instance is ProjectileBase pb) Release(pb);
        else if (instance != null) Release(instance.gameObject);
    }


    private static AutoDicPool<GameObject, GameObject> pool;
    private static DicObjectPool<ProjectileBase, ProjectileBase> bulletPool;

    public void Init()
    {
        VfxPool.Sink = this;//接管池入口原语：供 05_UnitCore/06_Gameplay 等下层调用（见 00Core/VfxPool.cs）
        pool = new(ItemUpdate, ItemAdd, ItemEnqueue, 60);//没人用的60秒销毁这个类(60秒未使用销毁这个项)
        bulletPool = new(BulletAdd, BulletPop,BulletPush);
        // 在场景卸载前清空池，此时池中对象尚未被销毁，清理是安全的
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    public void UnInit()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>宿主下线：归还池入口（未接管 = Creat 返回 null / Release 空操作，语义同原空对象）；若已被新实例占用则不动。
    /// <para>⚠ 本管理器随 <c>GameRoot</c> <c>DontDestroyOnLoad</c> 常驻 ⇒ 此处通常只在整个 Play 结束时触发。</para></summary>
    private void OnDestroy()
    {
        if (ReferenceEquals(VfxPool.Sink, this))
            VfxPool.Sink = null;
    }

    /// <summary>场景卸载前清空对象池，避免残留已销毁对象的引用</summary>
    private void OnSceneUnloaded(Scene scene)
    {
        ClearPool(scene.name);
    }


    void Update() {
        pool.Update();
    }

    private void ClearPool(string name)
    {
        if (bulletPool != null)
        {
            bulletPool.Clear();
        }
        if (pool != null)
        {
            pool.Clear();
        }
    }

    #region 特效
    public static GameObject Creat(GameObject tmp, Vector3 pos = default,Quaternion roation=default,Transform parent=default) {
        if (tmp == null)
        {
            Debug.LogError("尝试添加的Vfx为空");
            return null;
        }
        GameObject ps=default;

        ps = pool.Get(tmp);
        if (!ps)
        {
            Debug.LogError("管理器对象池返回了null对象1",ps);
        }
        if (!ps.gameObject)
        {
            Debug.LogError("管理器对象池返回了null对象2"+ ps,ps);
        }

        //ps.gameObject.SetActive(false);
        ps.transform.position = pos;
        ps.transform.rotation = roation;
        ps.transform.localScale = Vector3.one;
        ps.SetActive(true);
        while (parent != default && (parent.gameObject.activeSelf == false || parent.localScale == Vector3.zero))
        {
            parent = parent.parent;
            //Debug.LogError(ps+"上升父级到" + parent,ps);
        }
        MoveBackToCurrentScene(ps);
        ps.transform.SetParent(parent);

        foreach (var item in ps.GetComponents<IRecyclable>())
        {
            item.OnShow();
        }
        /*
        if(ps.TryGetComponent(out ParticleSystem partice))
        {
            partice.Play();
        }*/

        return ps;
    }
    /// <summary>释放特效</summary>
    public static void Release(GameObject go)
    {
        if (!go.IsValid()) return;

        if (go.TryGetComponent(out ParticleSystem ps))
        {
            ps.Stop(true);
        }
        else if (go.TryGetComponent(out LimitedLife ll))
        {
            ll.allowRelease=true;
        }
    }
    private bool ItemUpdate(GameObject go) 
    {
        if (go.TryGetComponent(out LimitedLife ll))
        {
            return ll.IsAlive();
        }
        else if (go.TryGetComponent(out ParticleSystem ps))
        {
            return ps.IsAlive(false);
        }

        return false;
    }

    private GameObject ItemAdd(GameObject tmp) {
        //Debug.LogError("长度"+ pool.Find(tmp).Count);
        GameObject ps = pool.Find(tmp, PreRelease);
        if (!ps.IsValid())
        {
            ps = Instantiate(tmp, transform);
        }
        //ps.SetActive(false);
        return ps;
    }



    /// <summary>尝试提前释放</summary>
    private bool PreRelease(GameObject item)
    {
        if (!item.IsValid()) return false;
        if (item.TryGetComponent(out LimitedLife ll))
        {
            return ll.AllowPreRelease();
        }
        return false;
    }

    /// <summary>
    /// 回收
    /// </summary>
    private void ItemEnqueue(GameObject ps) {
        if (!ps) return;
        foreach (var item in ps.GetComponents<IRecyclable>())
        {
            item.OnHide();
        }
        ps.SetActive(false);
        ps.transform.SetParent(transform);
    }
    #endregion


    #region 子弹
    private ProjectileBase BulletAdd(ProjectileBase tmp) 
    {
        var bullet=Instantiate(tmp, transform);
        bullet.Template = tmp;
        return bullet;
    }
    private void BulletPop (ProjectileBase ps)
    {
        ps.gameObject.SetActive(true);
        ps.transform.SetParent(null);
    }
    private void BulletPush(ProjectileBase ps)
    {
        ps.gameObject.SetActive(false);
        ps.transform.SetParent(transform);
    }

    /// <summary>创建子弹</summary>
    public static ProjectileBase Creat(ProjectileBase tmp, Vector3 pos = default, Quaternion roation = default)
    {
        if (tmp == null)
        {
            Debug.LogError("尝试添加的Vfx为空");
            return null;
        }
        ProjectileBase bullet = bulletPool.Get(tmp);

        if (!bullet) Debug.LogError("管理器对象池返回了null对象1", bullet);
        else if (!bullet.gameObject) Debug.LogError("管理器对象池返回了null对象2" + bullet, bullet);
        MoveBackToCurrentScene(bullet.gameObject);
        bullet.transform.position = pos;
        bullet.transform.rotation = roation;
        bullet.transform.localScale = Vector3.one;

        return bullet;
    }
    /// <summary>释放特效</summary>
    public static void Release(ProjectileBase go)
    {
        if (!go.IsValid()) return;
        if (go.Template.IsValid()) bulletPool.Release(go.Template, go);
        else Destroy(go.gameObject);
    }

    public static void MoveBackToCurrentScene(GameObject objectToMove)
    {
        // 获取当前活动场景作为目标
        Scene currentActiveScene = SceneManager.GetActiveScene();

        // 确保目标场景有效且已加载
        if (currentActiveScene.IsValid() && currentActiveScene.isLoaded)
        {
            objectToMove.transform.parent = null;
            SceneManager.MoveGameObjectToScene(objectToMove, currentActiveScene);
            //Debug.Log($"{objectToMove.name} 已移回当前活动场景 {currentActiveScene.name}");
        }
    }
    #endregion
}

}
