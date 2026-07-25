using Core;
using Core.Interface;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;



public class VFXManager : Singleton<VFXManager> , I_GlobaManager
{

    private static AutoDicPool<GameObject, GameObject> pool;
    private static DicObjectPool<ProjectileBase, ProjectileBase> bulletPool;

    public void Init()
    {
        pool = new(ItemUpdate, ItemAdd, ItemEnqueue, 60);//没人用的60秒销毁这个类(60秒未使用销毁这个项)
        bulletPool = new(BulletAdd, BulletPop,BulletPush);
        // 在场景卸载前清空池，此时池中对象尚未被销毁，清理是安全的
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    public void UnInit()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
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
        while (parent!=default && (parent.gameObject.activeSelf == false || parent.localScale == Vector3.zero)) parent = parent.parent;
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
        bulletPool.Release(go.Template, go);
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

