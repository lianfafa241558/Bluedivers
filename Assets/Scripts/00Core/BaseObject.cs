using Core.Interface;
using FPSGame.Attribute;
using UnityEngine;
namespace Core
{
    public class BaseObject : BaseMono, I_Entity
    {
        [Foldout("信息", true)]
        [InspectorName("名称")]
        public string ShowName;

        public string Id;
        [InspectorName("头像")]
        public Sprite Portrait;
        [InspectorName("额外图标")]
        public Sprite ExtraPortrait;

        [InspectorName("颜色")]
        public Color Color = Color.white;

        public virtual float HalfRange => 1;

        string I_Entity.ShowName { get => ShowName; set => ShowName=value; }
        string I_Entity.Id { get => Id; set => Id=value; }
        Sprite I_Entity.Portrait { get => Portrait; set => Portrait = value; }
        Sprite I_Entity.ExtraPortrait { get => ExtraPortrait; set => ExtraPortrait = value; }
        Color I_Entity.Color { get => Color; set => Color = value; }

       
    }
}