using System.Collections.Generic;
using System.Linq;
using Core;
using FpsGame.Mission;
using FPSGame.AI;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.Game
{
    /// <summary>
    /// 巡逻队生成器
    /// 生成巡逻队需要的热度和阵营有关
    /// 每级难度会降低5%热度需求(乘算)(最高的Lunatic降低75%)
    /// 每个玩家独立记录热度，每名额外的玩家会降低所有玩家10%的热度需求(乘算)
    /// 本身增加1/秒的热度
    /// 靠近"有影响力"的点时，会额外根据距离[50,150]米，产生[0.5,0]/秒的热量，影响区域不叠加
    /// 摧毁"有影响力"的点会提高(所有玩家)5%的产热速度
    /// 完成主线任务会降低(所有玩家)20%的热度需求(乘算)，并且将撤离点设置为"有影响力"的点
    /// 倒地的玩家会额外产生0.5/秒的热度，呼叫增援会立即产生20%的热度
    /// 创建巡逻队有10秒的公共冷却
    /// 50米内，每有一名其他玩家，热度产生速度变为[1/(1+其他玩家人数)](多人分摊)
    /// 
    /// 例如:4个玩家在虫族的Lunatic清完了全图后靠近撤离点
    /// 为每名玩家刷出巡逻队的热度需求为:
    /// [180(基础)*65%(难度)*70%(人数)*80%(主线)]=66
    /// 产热速度为{[1+0.25(巢穴)+0.5(撤离点)]}/4= 0.4375 /s =150s
    /// 也就是说在撤离点时，每名玩家150s(等效全队38s)就可以产生一个巡逻队
    /// 
    /// 再例如:1个玩家在虫族Extreme清完了全图后靠近撤离点
    /// [180(基础)*80%(难度)*80%(主线)]=115.2
    /// 产热速度为{[1+0.25(巢穴)+0.5(撤离点)]}= 1.75 /s = 65s
    /// 65秒就可以产生一个巡逻队
    /// 
    /// 当地图上的单位超过一个阈值时，将停止生成巡逻队，但热度会继续上升
    /// 定位混淆将提高25%的热度需求(乘算)
    /// 
    /// 每名玩家具有85米的安全区，巡逻队不会在任何一名玩家的安全区内生成
    /// 巡逻队将会随机选择一个巢穴作为起始点，尝试在距离玩家125米处尝试刷新(具有±25米的偏差)
    /// 在安全区内的巢穴不会作为起始点
    /// 如果没有可用的巢穴，将会尝试从距离玩家最近的地图边缘作为起始点
    /// 理论上，如果玩家在摧毁了全部的巢穴后，在靠近地图边缘85内撤离，那么将不会生产巡逻队
    /// 因为地图边缘在安全区内，但是实际会从反方向的90度的弧度范围内尝试刷新
    /// </summary>
    public class PatrolContriller : TickBehaviour
    {
        #region 常量
        /// <summary>每级难度5%减成</summary>
        private const float DIFFICULTY_REDUCTION_PER_LEVEL = 0.05f;
        /// <summary>每额外玩家10%减成</summary>
        private const float PLAYER_REDUCTION_PER_EXTRA = 0.1f;
        /// <summary>基础每秒产热</summary>
        private const float BASE_HEAT_PER_SECOND = 1f;
        /// <summary>倒地玩家额外产热</summary>
        private const float DOWNED_HEAT_PER_SECOND = 0.5f;
        /// <summary>影响力点最大额外产热</summary>
        private const float HEATPOINT_BONUS = 0.5f;
        /// <summary>影响力点最小距离</summary>
        private const float HEATPOINT_MIN_DIST = 50f;
        /// <summary>影响力点最大距离</summary>
        private const float HEATPOINT_MAX_DIST = 150f;
        /// <summary>摧毁巢穴+5%产热</summary>
        private const float DESTROY_HEATPOINT_BONUS = 0.05f;
        /// <summary>主线完成-20%需求</summary>
        private const float MAIN_TASK_REDUCTION = 0.8f;
        /// <summary>呼叫增援+20%热度</summary>
        private const float REINFORCEMENT_INSTANT_HEAT = 0.2f;
        /// <summary>公共冷却10s</summary>
        private const float PATROL_COOLDOWN = 10f;
        /// <summary>地图最大单位数</summary>
        private const int MAX_MAP_UNITS = 300;
        /// <summary>玩家安全区85m</summary>
        private const float SAFE_AREA_RADIUS = 85f;
        /// <summary>生成目标距离125m</summary>
        private const float SPAWN_TARGET_DISTANCE = 125f;
        /// <summary>生成距离偏差±25m</summary>
        private const float SPAWN_DISTANCE_OFFSET = 25f;
        #endregion

        // 玩家独立热度数据结构
        [System.Serializable]
        private struct PlayerHeatData
        {
            public I_Actor player;
            public float currentHeat;   // 当前累计热度
            public float requiredHeat; // 生成巡逻队所需热度
        }

        // 核心数据
        [SerializeField]
        private List<PlayerHeatData> _playerHeatList;
        [SerializeField]
        private List<MissionBase> _influencePoints = new List<MissionBase>(); // 影响力点（巢穴）
        private float _basePatrolHeat;  // 基础生成热度（Start已计算）
        [SerializeField]
        private float _patrolCooldownTimer;
        private bool _isMainTaskCompleted;
        private int _destroyedInfluencePointsCount;
        /// <summary>定位混淆强化：提高生成巡逻队的所需热度（加长生成间隔）</summary>
        private bool _isPositionConfusion;

        // 外部引用
        private List<I_Actor> Players => ActorsManager.Players;
        private int CurrentMapUnits => ActorsManager.Enemys.Count;





        Vector2 mapCenter;
        float mapRadius;

        System.Random random;
        BattleManager manager;

        #region 生命周期

        protected override void Start()
        {
            base.Start();
            TickTime = 1; // 每秒执行一次
            manager = BattleManager.Instance;
            random = manager.BattleRandom;

            mapCenter = (Vector2.one * TaskManager.Instance.nowTask.MapSize / 2);
            mapRadius = TaskManager.Instance.nowTask.CameraSize / 2;

            // 读取基础热度（你已在Start实现）
            _basePatrolHeat = TaskManager.Instance.nowTask.campData.PatrolCreatValue;
            _basePatrolHeat *= 1 - DIFFICULTY_REDUCTION_PER_LEVEL * (int)TaskManager.Instance.nowTask.difficulty - PLAYER_REDUCTION_PER_EXTRA * (ActorsManager.Players.Count - 1);

            // 订阅全局事件
            BattleEventSub.OnMissionStart += OnMissionCreated;
            BattleEventSub.OnMissionCompleted += OnMissionCompleted;

            GlobalEventSub.OnPlayerCreate += OnPlayerJoin;
            GlobalEventSub.OnFriendCreate += OnPlayerJoin;

            // 初始化玩家热度数据
            InitHeatData();
        }

        private void OnDestroy()
        {
            // 取消事件订阅
            BattleEventSub.OnMissionStart -= OnMissionCreated;
            BattleEventSub.OnMissionCompleted -= OnMissionCompleted;

            GlobalEventSub.OnPlayerCreate -= OnPlayerJoin;
            GlobalEventSub.OnFriendCreate -= OnPlayerJoin;
        }

        public override bool Tick()
        {
            if (Players == null || Players.Count == 0) return true;

            // 刷新玩家列表（防止玩家进出）
            //RefreshPlayerHeatData();

            // 公共冷却
            if (_patrolCooldownTimer > 0) _patrolCooldownTimer -= TickTime;

            // 地图单位超限，只涨热度不生成
            bool canSpawnPatrol = CurrentMapUnits < MAX_MAP_UNITS && _patrolCooldownTimer <= 0;

            // 遍历所有玩家，计算产热 + 生成巡逻队
            for (int i = 0; i < _playerHeatList.Count; i++)
            {
                var playerData = _playerHeatList[i];
                float heatPerSecond = CalculateTotalHeatPerSecond(playerData.player);

                // 累计热度
                playerData.currentHeat += heatPerSecond * TickTime;
                // 满足条件，生成巡逻队
                if (canSpawnPatrol && playerData.currentHeat >= playerData.requiredHeat)
                {
                    SpawnPatrol(playerData.player);
                    // 重置热度 + 启动冷却
                    playerData.currentHeat -= playerData.requiredHeat;
                    _patrolCooldownTimer = PATROL_COOLDOWN;
                }

                _playerHeatList[i] = playerData;
            }

            return true;
        }
        #endregion

        #region 核心计算逻辑
        /// <summary>
        /// 计算单个玩家每秒总产热速度
        /// </summary>
        private float CalculateTotalHeatPerSecond(I_Actor player)
        {

            // 倒地额外产热
            //if (player.IsDowned) heatRate += DOWNED_HEAT_PER_SECOND;
            // 影响力点额外产热（取最近一个，不叠加）
            float influenceBonus = GetInfluencePointHeatBonus(player);
            // 摧毁巢穴产热加成
            float destroyBonus = _destroyedInfluencePointsCount * DESTROY_HEATPOINT_BONUS;

            // 最终产热
            float heatRate = BASE_HEAT_PER_SECOND + influenceBonus + destroyBonus;

            // 玩家分摊：50米内其他玩家数量
            int nearbyPlayers = Players.Count(item => Vector3.Distance(player.Pos, item.Pos) <= 50f) - 1;
            if (nearbyPlayers > 0) heatRate /= (1 + nearbyPlayers);

            return heatRate;
        }

        /// <summary>
        /// 计算单个玩家最终所需热度
        /// </summary>
        private float GetFinalHeat()
        {
            float required = _basePatrolHeat;

            int playerCount = Players.Count;
            required *= (1.1f - playerCount * 0.1f);

            // 主线任务减成
            if (_isMainTaskCompleted) required *= MAIN_TASK_REDUCTION;

            // 定位混淆：提高热度需求（加长巡逻队生成间隔）
            if (_isPositionConfusion) required *= 1.25f;
            return required;
        }



        /// <summary>
        /// 获取影响力点产热加成（距离50-150m，0.5-0）
        /// </summary>
        private float GetInfluencePointHeatBonus(I_Actor player)
        {
            float maxBonus = 0;
            Vector3 playerPos = player.Pos;

            foreach (var point in _influencePoints)
            {
                float dist = Vector3.Distance(playerPos, point.pos);
                if (dist < HEATPOINT_MIN_DIST)
                {
                    maxBonus = HEATPOINT_BONUS;
                    break;
                }
                if (dist > HEATPOINT_MAX_DIST)
                    continue;

                // 线性插值计算产热
                float t = 1 - (dist - HEATPOINT_MIN_DIST) / (HEATPOINT_MAX_DIST - HEATPOINT_MIN_DIST);
                float bonus = HEATPOINT_BONUS * t;
                if (bonus > maxBonus)
                    maxBonus = bonus;
            }

            return maxBonus;
        }


        #endregion

        #region 游戏事件
        /// <summary>
        /// 监听任务创建，收集影响力点
        /// </summary>
        private void OnMissionCreated(MissionBase mission)
        {
            if (mission.HasTag(MissionTag.HeatPoint) && !_influencePoints.Contains(mission))
            {
                _influencePoints.Add(mission);
            }
        }

        /// <summary>
        /// 监听任务完成，视为影响力点被摧毁
        /// </summary>
        private void OnMissionCompleted(MissionBase mission)
        {
            if (mission.HasTag(MissionTag.HeatPoint))
            {
                _destroyedInfluencePointsCount++;
            }

            //如果是主线
            if (mission.missionType == MissionType.Main && mission.parent == null)
            {
                _isMainTaskCompleted = true;

            }
            RefreshAllPlayerRequiredHeat();


        }

        /// <summary>
        /// 呼叫增援（预留方法，手动调用）
        /// </summary>
        public void OnPlayerCallReinforcement(I_Actor player)
        {
            for (int i = 0; i < _playerHeatList.Count; i++)
            {
                if (_playerHeatList[i].player == player)
                {
                    var data = _playerHeatList[i];
                    data.currentHeat += data.requiredHeat * REINFORCEMENT_INSTANT_HEAT;
                    _playerHeatList[i] = data;
                    break;
                }
            }
        }
        /// <summary>
        /// 玩家加入（先填着）
        /// </summary>
        private void OnPlayerJoin(I_Actor player)
        {
            float requiredHeat = GetFinalHeat();
            _playerHeatList.Add(InitRequiredHeat(player, requiredHeat));
            RefreshAllPlayerRequiredHeat();
        }
        /// <summary>
        /// 玩家离开（先填着）
        /// </summary>
        private void OnPlayerLeave(I_Actor player)
        {
            _playerHeatList.RemoveAll(item => item.player == player);
            RefreshAllPlayerRequiredHeat();
        }


        #endregion

        #region 工具方法
        /// <summary>
        /// 初始化玩家热度数据
        /// </summary>
        private void InitHeatData()
        {
            _playerHeatList = new();
            float requiredHeat = GetFinalHeat();
            foreach (var player in Players)
            {
                _playerHeatList.Add(InitRequiredHeat(player, requiredHeat));
            }
            if (manager.HaveBooster(BoosterType.PositionConfusion))
            {
                _isPositionConfusion = true;
                RefreshAllPlayerRequiredHeat();
            }
        }

        /// <summary>
        /// 刷新所有玩家所需热度
        /// </summary>
        private void RefreshAllPlayerRequiredHeat()
        {
            float requiredHeat = GetFinalHeat();
            for (int i = 0; i < _playerHeatList.Count; i++)
            {
                SetRequiredHeat(i, requiredHeat);
            }
        }
        private void SetRequiredHeat(int index, float value)
        {
            var data = _playerHeatList[index];
            data.requiredHeat = value;
            _playerHeatList[index] = data;
        }
        private PlayerHeatData InitRequiredHeat(I_Actor player, float value)
        {
            return new PlayerHeatData {
                player = player,
                currentHeat = 0,
                requiredHeat = value
            };
        }
        #endregion

        #region 巡逻队生成（核心逻辑，无地图生成器）
        /// <summary>
        /// 生成巡逻队（安全区判断 + 生成点规则）
        /// </summary>
        private void SpawnPatrol(I_Actor targetPlayer)
        {
            Debug.LogWarning($"尝试为玩家{targetPlayer.gameObject.name} 生成巡逻队");

            if (GetValidSpawnPosition(targetPlayer.Pos, out Vector3 spawnPos))
            {
                var list = manager.CreatPatrol(spawnPos);
                var targetPos = Tool.GetCircleIntersection(mapCenter, mapRadius + 30, spawnPos.ToVector2(), (targetPlayer.Pos - spawnPos).ToVector2()).ToVector3();
                list.ForEach(item => item.GetComponent<EnemyController>().PatrolPos = targetPos + (spawnPos - item.transform.position));

            }
        }

        /// <summary>
        /// 获取合法生成位置
        /// </summary>
        /// <param name="playerPos">目标玩家位置</param>
        /// <param name="spawnPoint">输出的合法生成点</param>
        /// <returns>是否找到有效生成点</returns>
        private bool GetValidSpawnPosition(Vector3 playerPos, out Vector3 spawnPoint)
        {
            // ==============================
            // 1. 玩家85米安全区不生成
            // 2. 优先选择非安全区巢穴
            // 3. 无巢穴则使用地图边缘
            // 4. 生成距离125±25米
            // ==============================
            spawnPoint = Vector3.zero;
            Vector2 logicPos = playerPos.ToVector2();

            // 遍历所有影响力点，筛选不在任何玩家安全区内的点
            List<MissionBase> validInfluencePoints = _influencePoints
                .Where(point => !Players
                    .Any(player => Vector2.Distance(point.pos.ToVector2(), player.LogicPos.RawVector2) < SAFE_AREA_RADIUS))
                .ToList();

            // 存在有效影响力点，随机选择并计算生成位置
            if (validInfluencePoints.Count > 0)
            {
                MissionBase randomPoint = validInfluencePoints.RandomTake(random);
                Vector2 dir = (randomPoint.pos.ToVector2() - logicPos).normalized;
                spawnPoint = (logicPos + dir * SPAWN_TARGET_DISTANCE).ToVector3();
                Debug.LogWarning("有效影响力点" + spawnPoint);
            }
            else
            {
                Vector2 dir = (mapCenter - logicPos).normalized;
                //如果已经在安全范围了
                if (Players.Any(player => Vector2.Distance(mapCenter + dir * mapRadius, player.LogicPos.RawVector2) < SAFE_AREA_RADIUS))
                {
                    dir *= -1;
                }
                float theta = Mathf.Atan2(dir.y, dir.x);
                var dx = random.Range(-0.785f, 0.785f);//±45度
                spawnPoint = logicPos.ToVector3() + new Vector3(Mathf.Cos(theta + dx), 0, Mathf.Sin(theta + dx)) * SPAWN_TARGET_DISTANCE;
                Debug.LogWarning($"地图边界点{spawnPoint},x{Mathf.Cos(theta + dx)},y{Mathf.Sin(theta + dx)}");
            }

            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPoint, out var hit, SPAWN_DISTANCE_OFFSET, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPoint = hit.position;
            }
            //Debug.LogError($"玩家位置{playerPos},最终位置{spawnPoint}");
            return spawnPoint != default;
        }


        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            foreach (var play in Players)
            {
                Gizmos.DrawWireSphere(play.Pos, SAFE_AREA_RADIUS);
            }

            foreach (var point in _influencePoints)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(point.pos, HEATPOINT_MIN_DIST);
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(point.pos, HEATPOINT_MAX_DIST);
            }
        }
    }
}