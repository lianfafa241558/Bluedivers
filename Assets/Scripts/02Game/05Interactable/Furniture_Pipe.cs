using System.Collections.Generic;
using System.Linq;
using GameContract;
using RootMotion.FinalIK;
using Unity.BaseTool;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

//4个状态:Pipe 投影状态-PipeLink 等待连接状态-PipeWait 等待安装状态-PipeComplete 完成状态



/// <summary>
/// 管道
/// </summary>
public class Furniture_Pipe : Furniture_Base
{
    public static List<Furniture_Base> targets=new();

    [Foldout("配置", true)]
    [SerializeField]
    GameObject root;

    [SerializeField]
    GameObject Temp;

    [SerializeField]
    Renderer pipeSkin;
    [SerializeField]
    Renderer pipeSkeleton;

    [SerializeField]
    /// <summary>上一根管道</summary>
    Furniture_Pipe lastPipeline;
    [SerializeField]
    /// <summary>下一根管道</summary>
    Furniture_Pipe nextPipeline;

    private Furniture_Base target;
    public override void Operate()
    {
        base.Operate();
        /*
        var user = owner;
        if (HaveFlag(FurnitureFlag.Speech))
        {
            GlobalEventManager.PlayMeetSoeech(user, SpeechTypeEnum.Responded);
        }
        if (audioOper) PlaySound(audioOper);
        lastOperatetime = Time.time;
        GlobalEventManager.FurnitureOperate(user, this);
        */
        switch (Id)
        {
            case "Pipe":
                Place();
                break;
            case "PipeLink":
                Link();
                break;
            case "PipeWait":
                Complete();
                break;
            case "PipeComplete":
                Link();//正常情况下是canoper已经关掉的，但是初始的那根除外
                break;
            case "PipeError":
                Complete();
                break;
        }
    }



    public override bool CanOperate(GameObject unit)
    {
        if (Id == "PipeWait")
        {
            if (!nextPipeline || (lastPipeline!=null&&lastPipeline.Id == "Pipe")) return false;//下一根还没放
            if(lastPipeline != null && lastPipeline.Id != "PipeComplete") return false;//上一根还没好

        }
        return canOperate;
    }

    protected override void InOperateUpdate()
    {
        if (!owner) return;

        var nearest = targets.Find(t =>t.IsValid() && Vector3.Distance(relatedTrans.position, t.relatedTrans.position) < 2);

        if (nearest.IsValid())
        {
            target = nearest;
            relatedTrans.position = nearest.relatedTrans.position;
        }

        else if (Physics.Raycast(owner.transform.TransformPoint(Vector3.forward * 1.1f) + Vector3.up * 10, Vector3.down, out var hitInfo, 20, LayerDefinition.GroundLayers))
        {
            relatedTrans.position = hitInfo.point+Vector3.up*0.5f;
        }

    }

    [ContextMenu("确定位置")]
    public void Place()
    {

        //初始管道一开始就是link；不用走这个阶段
        if (pipeSkeleton) pipeSkeleton.sharedMaterial = pipeSkin.sharedMaterial;
        inOperate = false;
        Id = "PipeLink";
        audioOper = null;
        if (pipeSkin) FpsHelper.UpdatePipeBounds(pipeSkin.GetComponent<SkinnedMeshRenderer>(),transform);
        if (pipeSkeleton) FpsHelper.UpdatePipeBounds(pipeSkeleton.GetComponent<SkinnedMeshRenderer>(),transform);
        if (root.TryGetComponent<CCDIK>(out var cCDIK))
        {
            cCDIK.enabled = false;
            foreach (var item in root.GetComponentsInChildren<Collider>())
            {
                item.enabled = true;
                item.gameObject.layer = 0;
                TerrainUtils.ModifyHeightMap(item.transform.position,0, 2f,0.5f, ShapeType.Circle, false,false);
            }
        }

        if (target)
        {
            Complete();
            target.relatedTrans = transform;
            target.Operate();
        }
        else if (!lastPipeline.lastPipeline.IsValid())
        {
            Complete();
            canOperate = true;
        }

    }
    
    public List<Furniture_Pipe> GetAllPipes()
    {
        var re = new List<Furniture_Pipe>();

        Furniture_Pipe item = this;
        re.Add(item);

        while (item.lastPipeline.IsValid())//往前取
        {
            item = item.lastPipeline;
            re.Add(item);
        }
        item = this;
        while (item.nextPipeline.IsValid())//往后取
        {
            item = item.nextPipeline;
            re.Add(item);
        }
        return re;
    }

    [ContextMenu("连接管线")]
    public void Link()
    {
        //Debug.LogError("连接",gameObject);
        nextPipeline = Instantiate(Temp, transform.position, transform.rotation, root.transform.parent).GetComponentInChildren<Furniture_Pipe>();
        nextPipeline.lastPipeline = this;
        nextPipeline.owner = owner;
        if (lastPipeline)
        {
            Id = "PipeWait";
            desc = "建造管线";
            inOperate = false;
            meetTime = 3;
        }
        else
        {
            Complete();
        }

    }
    [ContextMenu("完成")]
    public void Complete()
    {
        //Debug.LogError("完成", gameObject);
        Id = "PipeComplete";
        PlaySound(audioClose);
        canOperate = false;
        inOperate = false;
        meetTime = 3;
        Press = 3;
        relatedTrans2.gameObject.SetActive(false);
    }

    [ContextMenu("错误")]
    public void Error()
    {
        Id = "PipeError";
        canOperate = true;
        meetTime = 5;
        Press = 5;
        relatedTrans2.gameObject.SetActive(true);
    }

}
