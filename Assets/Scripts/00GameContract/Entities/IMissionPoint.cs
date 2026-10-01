using FPSGame.Core.Interface;

namespace FPSGame.GameContract
{
    public interface IMissionPoint : IEntity
    {
        public bool HaveTag(MissionTag tag);

        public float IconSizeScale { get; }
        //public bool IsMain { get;}
        public float AreaRange { get; set; }

    }
}
