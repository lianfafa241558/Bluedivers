#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Tool = Utils.Tool;
public static class CopyUtils
{

    [MenuItem("CONTEXT/MonoBehaviour/粘贴组件到同物体子类", priority = -1)]
    static void ResetComponentConfig(MenuCommand command)
    {
        MonoBehaviour source = (MonoBehaviour)command.context;
        //Debug.LogError("组件的类型"+ source.GetType());
        var arr = source.GetComponents(source.GetType());

        if (arr.Length==1)
        {
            Debug.LogError("目标组件不存在");
            return;
        }
        foreach (var target in arr)
        {
            if (target == source) continue;

            SerializedObject sourceSerialized = new SerializedObject(source);
            SerializedObject targetSerialized = new SerializedObject(target);
            SerializedProperty iterator = sourceSerialized.GetIterator();

            while (iterator.NextVisible(true))
            {
                if (iterator.name == "m_Script") continue;
                SerializedProperty targetProp = targetSerialized.FindProperty(iterator.name);
                if (targetProp != null && targetProp.propertyType == iterator.propertyType)
                {
                    targetSerialized.CopyFromSerializedProperty(iterator);
                }
            }
            targetSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

    }


    static GameObject source;


    [MenuItem("GameObject/辅助功能/选择复制源", priority = -100)]
    static void CopyComponents()
    {
        source = Selection.activeGameObject;
    }

    [MenuItem("GameObject/辅助功能/粘贴全部组件", priority = -100)]
    static void PasteComponents()
    {
        var copiedComponents = source.GetComponents<Component>();
        //foreach (var target in Selection.gameObjects)
        //{
        var target = Selection.activeGameObject;
        foreach (var comp in copiedComponents)
        {
            if (comp is Transform) continue;
            ComponentUtility.CopyComponent(comp);
            ComponentUtility.PasteComponentAsNew(target);
        }
        //}
    }
     

    [MenuItem("GameObject/辅助功能/粘贴碰撞箱和伤害组件",false, priority = -100)]
    static void CopyCollider()
    {
        PasteCompForInterface<GameContract.I_Damagable>();
        PasteComp<Collider>();
    }

    [MenuItem("GameObject/辅助功能/生成到下一级的碰撞箱",priority = -100)]
    static void AutoCollider()
    {
        for (int u = 0; u < Selection.gameObjects.Length; ++u)
        {
            var item = Selection.gameObjects[u].transform;
            for (int i = 0; i < item.childCount; ++i)
            {
                var tar = item.GetChild(i);
                var pos = tar.localPosition;
                pos.x = Tool.Round(pos.x, 2);
                pos.y = Tool.Round(pos.y, 2);
                pos.z = Tool.Round(pos.z, 2);
                var box = item.gameObject.AddComponent<BoxCollider>();
                box.size = new(Mathf.Max(Mathf.Abs(pos.x), 0.2f), Mathf.Max(Mathf.Abs(pos.y), 0.2f), Mathf.Max(Mathf.Abs(pos.z), 0.2f));
                box.center = pos / 2;

            }
        }
    }



    static void PasteComp<T>() where T:Component
    {
        var target = Selection.activeGameObject;
        var list = source.GetComponentsInChildren<T>();
        foreach (var item in list)
        {
            Transform targetChildren = target.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == item.transform.name);
            if (targetChildren==null)
            {
                Transform parTrans = target.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == item.transform.parent.name);
                targetChildren = new GameObject(item.transform.name).transform;
                targetChildren.parent = parTrans;
                targetChildren.localPosition = item.transform.localPosition;
                targetChildren.localRotation = item.transform.localRotation;
                targetChildren.localScale = item.transform.localScale;

            }
            T targetComp = (T)targetChildren.gameObject.AddComponent(item.GetType());
            EditorUtility.CopySerialized(item, targetComp);
        }
    }
    // 修改点2：新增专门处理接口的方法（泛型约束改为 class + 接口）
    static void PasteCompForInterface<T>() where T : class
    {
        var target = Selection.activeGameObject;
        // 修改点3：获取所有实现了 T 接口的 Component
        var list = source.GetComponentsInChildren<Component>()
                         .OfType<T>() // 筛选实现了 I_Damagable 接口的组件
                         .ToList();

        foreach (var item in list)
        {
            // 确保 item 是 Component（因为只有 Component 能挂载到 GameObject）
            if (item is not Component itemComp) continue;

            Transform targetChildren = target.GetComponentsInChildren<Transform>()
                                             .FirstOrDefault(x => x.name == itemComp.transform.name);
            if (targetChildren == null)
            {
                Transform parTrans = target.GetComponentsInChildren<Transform>()
                                           .FirstOrDefault(x => x.name == itemComp.transform.parent.name);
                targetChildren = new GameObject(itemComp.transform.name).transform;
                targetChildren.parent = parTrans;
                targetChildren.localPosition = itemComp.transform.localPosition;
                targetChildren.localRotation = itemComp.transform.localRotation;
                targetChildren.localScale = itemComp.transform.localScale;
            }

            // 修改点4：添加组件时使用实际类型，而非接口
            Component targetComp = targetChildren.gameObject.AddComponent(itemComp.GetType());
            // 复制序列化数据（保留原有逻辑）
            EditorUtility.CopySerialized(itemComp, targetComp);
        }
    }
}
#endif