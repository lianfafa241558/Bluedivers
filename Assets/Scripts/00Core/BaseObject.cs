using Core.Interface;
using Unity.BaseTool;
using UnityEngine;
namespace Core
{
    public class BaseObject : BaseMono, I_Entity
    {
        [Foldout("信息", true)]
        [CustomLabel("名称")]
        public string ShowName;

        public string Id;
        [CustomLabel("头像")]
        public Sprite Portrait;
        [CustomLabel("额外图标")]
        public Sprite ExtraPortrait;

        [CustomLabel("颜色")]
        public Color Color = Color.white;

        string I_Entity.ShowName { get => ShowName; set => ShowName=value; }
        string I_Entity.Id { get => Id; set => Id=value; }
        Sprite I_Entity.Portrait { get => Portrait; set => Portrait = value; }
        Sprite I_Entity.ExtraPortrait { get => ExtraPortrait; set => ExtraPortrait = value; }
        Color I_Entity.Color { get => Color; set => Color = value; }

       
    }
}