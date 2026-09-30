using FPSGame.Core.Interface;

namespace FPSGame.GameContract
{
    public interface I_MissionPoint : I_Entity
    {
        public bool HaveTag(MissionTag tag);

        public float IconSizeScale { get; }
        //public bool IsMain { get;}
        public float AreaRange { get; set; }

    }
}
