using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using PEMaths;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace FPSGame.GameContract
{
    /// <summary>
    /// <see cref="ServiceLocator"/> 各槽的**空对象默认实现**（2026-09-30 为 P5 批量替换加入）。
    ///
    /// <para>▍为什么需要它：契约里写明"服务未就绪时取中性值、不要抛"。有了空对象，
    /// `ServiceLocator.Battle.FindUnits(...)` 这类调用**永远安全**，调用点不必到处写 `?.` 或 null 三目，
    /// 批量替换才能机械进行；而且以后新增调用点天然带兜底。</para>
    ///
    /// <para>▍中性值约定：布尔 = false、数值 = 0（`PlayerCount` 例外取 1，按单人算）、
    /// 集合 = 空（**每次返回新实例**，避免调用方 `Remove` 污染共享列表）、对象 = null、事件 = 不触发。</para>
    /// </summary>
    internal static class NullServices
    {
        public static readonly ITaskService Task = new NullTaskService();
        public static readonly IBattleService Battle = new NullBattleService();
        public static readonly IFlowService Flow = new NullFlowService();
        public static readonly IVfxService Vfx = new NullVfxService();
        public static readonly INetService Net = new NullNetService();
        public static readonly IResService Res = new NullResService();
        public static readonly IArchiveService Archive = new NullArchiveService();
        public static readonly IWindowService Wnd = new NullWindowService();
        public static readonly IRoomService Room = new NullRoomService();
        public static readonly IPathService Path = new NullPathService();
    }

    internal sealed class NullTaskService : ITaskService
    {
        private static readonly int[] NeutralExtra = new int[4];
        public int[] ExtraDifficulty => NeutralExtra;
        public DifficultyEnum Difficulty => DifficultyEnum.Normal;
        public Dictionary<OOPartEnum, int> CollectProperty => new Dictionary<OOPartEnum, int>();
        public EnemyVarietyType EnemyVarietyType => default;
        public bool HasTask => false;
        public int Countdown { get { return 0; } set { } }
        public void EnterTransition() { }
    }

    internal sealed class NullBattleService : IBattleService
    {
        private static readonly System.Random Rng = new System.Random(0);
        public bool HaveBooster(BoosterType type) => false;
        public List<I_Actor> FindUnits(IPERange range, TargetCfg targetCfg, Func<I_Actor, bool> customFilter = null) => new List<I_Actor>();
        public List<I_Actor> FindUnits(TargetCfg targetCfg, Func<I_Actor, bool> customFilter = null) => new List<I_Actor>();
        public bool IsStartBattle => false;
        /// <summary>空对象 ⇒ 服务未就位（对应老写法 `BattleManager.Instance != null` 为 false）。</summary>
        public bool IsPresent => false;
        public bool IsTeamWiped => false;
        public int ReinforcementCount => 0;
        public System.Random BattleRandom => Rng;
        public void EnqueueInit(Action action) { }
        public GameObject CreatUnit(UnitTier tier, Vector3 pos, float range, bool isFixed = true) => null;
        public bool CreatWave(WaveCreateParams param) => false;
        public void ReleaseAirdrop(Vector3 point, int id, Action<GameObject> action = default) { }
        public void ReleaseAirdrop(Vector3 point, float angle, int id, Action<GameObject> action = default) { }
        public void Authorize(int id, bool state) { }
        public void AddBattleDataItem(int playerIndex, string name) { }
        public void EndGame(int delay, GameResult result = GameResult.Unknow) { }
        public void SubmitOOPart(GameObject user, OOPartEnum type, int count) { }
        public List<GameObject> CreatPatrol(Vector3 pos) => new List<GameObject>();
        public int WaveCount => 0;
        public void RevealAllMissions() { }
    }

    internal sealed class NullFlowService : IFlowService
    {
        public GameStateEnum GameState => GameStateEnum.Front;
        public bool IsMainStage => false;
        public bool IsLocal => false;
        public Coroutine RunCoroutine(IEnumerator routine) => null;
        public void SetGameState(GameStateEnum state) { }
        public LogicTimer CreateTimer(Action cb, float waitTime, int counter = 1, Action endcb = null) => null;
        public LogicTimer CreateTimer(Action<int> cb, float waitTime, int counter = 1, Action endcb = null) => null;
        public LogicTimer CreatePerTimer(Action percb, float waitTime, Action endcb = null) => null;
    }

    internal sealed class NullVfxService : IVfxService
    {
        public GameObject Creat(GameObject tmp, Vector3 pos = default, Quaternion rotation = default, Transform parent = default) => null;
        public void Release(GameObject go) { }
        public T Creat<T>(T template, Vector3 pos, Quaternion rotation) where T : Component => null;
        public void Release(Component instance) { }
    }

    internal sealed class NullNetService : INetService
    {
        public void Add(I_Login obj) { }
        public void Remove(I_Login obj) { }
    }

    internal sealed class NullResService : IResService
    {
        public GameObject CreatPrefab(string path, bool cache = false, Vector3 pos = default(Vector3)) => null;
        public Sprite LoadSprite(string path, bool cache = false) => null;
        public void AsyncLoadScene(string mapName, Action loaded, bool showLoadWnd = false, bool waitExtra = false, bool allowSkip = false) { }
        public void AsyncContinueLoadScene() { }
    }

    internal sealed class NullArchiveService : IArchiveService
    {
        public float GetSetting(string name) => 0f;
    }

    internal sealed class NullWindowService : IWindowService
    {
        public WindowStateEnum WindowState => WindowStateEnum.Game;
        public event UnityAction<WindowStateEnum, WindowStateEnum> OnWindowStateChange { add { } remove { } }
        public void CreatNotice(string role, string type, Func<bool> func = default, float vaildTime = -1) { }
        public void SetWndState(WndTypeEnum type, bool isActive = true) { }
    }

    internal sealed class NullRoomService : IRoomService
    {
        public int MasterIndex => 0;
        /// <summary>按"单人"兜底（与 MissionOilRefining 里用的默认值一致）。</summary>
        public int PlayerCount => 1;
    }

    internal sealed class NullPathService : IPathService
    {
        public void RequestPath(NavMeshAgent agent, Vector3 destination, bool log = false) { }
    }
}
