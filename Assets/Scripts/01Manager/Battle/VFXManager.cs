using Core.Interface;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class VFXManager : Singleton<VFXManager> , I_GlobaManager
{

    private static AutoDicPool<GameObject, GameObject> pool;
    private static DicObjectPool<ProjectileBase, ProjectileBase> bulletPool;

    public void Init()
    {
        pool = new(ItemUpdate, ItemAdd, ItemEnqueue, 60);//没人用的池60秒销毁这个类型(每20秒未使用销毁1个项)
        bulletPool = new(BulletAdd, BulletPop,BulletPush);
        GlobalEventManager.OnSceneChange += ClearPool;
    }
    public void UnInit()
    {
        GlobalEventManager.OnSceneChange -= ClearPool;
    }


    void Update() {
        pool.Update();
    }

    private void ClearPool(string name)
    {
        bulletPool.Clear();
        pool.Clear();
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
        ps.gameObject.SetActive(true);
        ps.transform.position = pos;
        ps.transform.rotation = roation;
        ps.transform.localScale = Vector3.one;
        while (parent!=default && (parent.gameObject.activeSelf == false || parent.localScale == Vector3.zero)) parent = parent.parent;
        ps.transform.SetParent(parent);
        
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

    private void ItemEnqueue(GameObject ps) {
        if (!ps) return;
        if (ps.TryGetComponent(out LimitedLife ll))
        {
            ll.OnEnd?.Invoke();
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

        bullet.transform.position = pos;
        bullet.transform.rotation = roation;
        bullet.transform.localScale = Vector3.one;

        return bullet;
    }
    /// <summary>释放特效</summary>
    public static void Release(ProjectileBase go)
    {
        if (!go.IsValid()) return;
        bulletPool.Release(go.Template, go);
    }
    #endregion
}

