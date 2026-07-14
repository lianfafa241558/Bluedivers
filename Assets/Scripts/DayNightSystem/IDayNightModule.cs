namespace FPSGame.DayNightSystem
{
    public interface IDayNightModule
    {
        void Initialize(DayNightState state);
        void Tick(DayNightState state, float deltaTime);
        void Dispose();
    }
}