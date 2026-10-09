using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Data;
using FPSGame.Game;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{

/// <summary>
/// 与 NPC 对话并播放语音的家具。
/// </summary>
[AddComponentMenu("交互/NPC 语音对话")]
public class Furniture_NPCChat : Furniture_Attached
{
    private const float AUTO_CHAT_COOLDOWN = 5f;
    private const float DECAY_SPEED = 1f;
    private const float GLOBAL_AUTO_CHAT_COOLDOWN = 20f;

    [Foldout("配置", true)]
    [SerializeField]
    [InspectorName("NPC语音组")]
    private SoundGroup_SO _soundGroup;

    [SerializeField]
    [InspectorName("散步组件")]
    private NPCWalk NPCWalk;

    [Foldout("自动搭话", true)]
    [SerializeField]
    [InspectorName("自动搭话距离")]
    [Tooltip("玩家进入此距离内开始积攒搭话进度")]
    private float _autoChatDistance = 3f;

    [SerializeField]
    [InspectorName("开启自动搭话")]
    private bool _enableAutoChat = true;

    /// <summary>
    /// 全局自动搭话公共CD，所有NPC共享，防止密集NPC同时搭话
    /// </summary>
    private static float _globalAutoChatCD;

    [DisplayField]
    [SerializeField]
    [InspectorName("积蓄进度")]
    [Tooltip("当前搭话积蓄进度（0~冷却时间）")]
    private float _chatImpulse;

    [DisplayField]
    [SerializeField]
    [InspectorName("冷却剩余")]
    private float _cooldownRemain;

    [DisplayField]
    [SerializeField]
    private float _nextAvailableTime;

    private Transform _playerTransform;
    private string _playerId;
    private bool _isSpeaking;

    protected override void OnEnable()
    {
        base.OnEnable();
        UnitEventBus.OnPlayerCreate -= OnPlayerCreated;
        UnitEventBus.OnPlayerCreate += OnPlayerCreated;
        UnitEventBus.OnFriendRoleChanged -= OnFriendRoleChanged;
        UnitEventBus.OnFriendRoleChanged += OnFriendRoleChanged;
        UnitEventBus.OnFriendLeave -= OnFriendLeave;
        UnitEventBus.OnFriendLeave += OnFriendLeave;
        GlobalEventBus.OnSwitchRole -= OnSwitchRole;
        GlobalEventBus.OnSwitchRole += OnSwitchRole;
    }

    private void OnPlayerCreated(IActor player)
    {
        if (this == null) return;

        _playerTransform = player.transform;
        _playerId = player.Id;

        RefreshVisibility();
    }

    private void OnSwitchRole(PlayerController newPlayer)
    {
        if (this == null) return;
        if (newPlayer == null) return;

        IActor actor = newPlayer.GetComponent<IActor>();
        if (actor != null)
        {
            _playerTransform = newPlayer.transform;
            _playerId = actor.Id;
        }

        RefreshVisibility();
    }

    /// <summary>联机：别人的角色确定了 ⇒ 也要重新判定（他选了这个角色，NPC 版就该消失）。</summary>
    private void OnFriendRoleChanged(Actor friend)
    {
        if (this == null) return;
        RefreshVisibility();
    }

    /// <summary>联机：别人离场 ⇒ 角色空出来了，NPC 要回来。</summary>
    private void OnFriendLeave(Actor friend)
    {
        if (this == null) return;
        RefreshVisibility();
    }

    /// <summary>
    /// 该 NPC 是否"已被玩家占用"：Id 与**本机玩家**或**任一盟友**的角色相同 ⇒ 隐藏。
    ///
    /// <para>▍为什么：场景里给每个可选角色都摆了一个 NPC 版本；玩家一旦用了该角色，
    /// 场上就不该再出现"同一个人的 NPC"（单机只有自己，联机时别人选的角色同理）。</para>
    ///
    /// <para>▍自动恢复：改成"每次重新判定"（而不是单向隐藏）⇒ 盟友离场后该角色空出来，NPC 会自己回来。
    /// ⚠ 本物体被隐藏后 <c>Update</c> 不再跑，但**事件回调照旧会到**（C# 事件与 active 无关）⇒ 这条路可行。</para>
    /// </summary>
    private void RefreshVisibility()
    {
        if (!TryGetComponent(out IActor selfActor)) return;
        string myId = selfActor.Id;
        if (string.IsNullOrEmpty(myId)) return;

        bool hide = false;

        // ① 本机玩家
        if (!string.IsNullOrEmpty(_playerId) && myId == _playerId) hide = true;

        // ② 联机盟友（ActorsManager.Players 里除本机玩家外的单位；Id 由 FriendController 从角色模型写入）
        if (!hide)
        {
            var players = ActorsManager.Players;
            for (int i = 0; i < players.Count; ++i)
            {
                var p = players[i];
                if (!p.IsValidMono() || ReferenceEquals(p, ActorsManager.Player)) continue;
                if (p.Id == myId) { hide = true; break; }
            }
        }

        gameObject.SetActive(!hide);
    }

    /// <summary>
    /// 冷却中或没有语音组时不可交互
    /// </summary>
    public override bool CanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        if (_soundGroup == null || Time.time < _nextAvailableTime) return false;
        return true;
    }

    /// <summary>
    /// 交互时播放NPC语音，语音时长+1秒后冷却结束才能再次交互
    /// </summary>
    public override void Operate()
    {
        base.Operate();
        DoChat();
    }

    protected override void Update()
    {
        base.Update();
        _cooldownRemain = Mathf.Max(0f, _nextAvailableTime - Time.time);

        if (_enableAutoChat && !_isSpeaking)
        {
            TryAutoChat();
        }
    }

    /// <summary>
    /// 玩家靠近时积攒搭话冲动，攒满后触发自动搭话；远离时消退
    /// </summary>
    private void TryAutoChat()
    {
        if (_playerTransform == null) return;
        if (Time.time < _globalAutoChatCD) return;
        if (_soundGroup == null) return;

        float sqrDistance = (_playerTransform.position - transform.position).sqrMagnitude;
        bool isInRange = sqrDistance <= _autoChatDistance * _autoChatDistance;

        if (isInRange)
        {
            // 玩家在范围内：积攒进度
            _chatImpulse += Time.deltaTime;

            if (_chatImpulse >= AUTO_CHAT_COOLDOWN)
            {
                // 攒满了，搭话
                _globalAutoChatCD = Time.time + GLOBAL_AUTO_CHAT_COOLDOWN;
                _chatImpulse = 0f;
                DoChat();
            }
        }
        else
        {
            // 玩家离开范围：消退
            if (_chatImpulse > 0f)
            {
                _chatImpulse = Mathf.Max(0f, _chatImpulse - DECAY_SPEED * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// 播放NPC语音并暂停游荡，面向玩家
    /// </summary>
    private void DoChat()
    {
        RuntimeSoundData soundData = _soundGroup.Get(transform.position);
        GlobalEventBus.ActorSpeech(gameObject, soundData);

        // 停下脚步，面向玩家
        NPCWalk?.PauseWandering();
        _isSpeaking = true;

        // 面向玩家（如果有 owner 则面向 owner，否则面向缓存的玩家）
        Transform lookTarget = null;
        if (owner != null)
        {
            lookTarget = owner.transform;
        }
        else if (_playerTransform != null)
        {
            lookTarget = _playerTransform;
        }

        if (lookTarget != null)
        {
            Vector3 targetPos = lookTarget.position;
            targetPos.y = transform.position.y;
            transform.LookAt(targetPos);
        }

        float clipLength = soundData.Clip != null ? soundData.Clip.length : 0f;
        float waitTime = clipLength + 1f;
        _nextAvailableTime = Time.time + waitTime;

        // 说话结束后恢复游荡
        StartCoroutine(ResumeWanderingAfter(waitTime));
    }

    /// <summary>
    /// 等待指定秒数后恢复游荡状态
    /// </summary>
    private System.Collections.IEnumerator ResumeWanderingAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isSpeaking = false;
        NPCWalk?.StartWandering();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        _nextAvailableTime = 0f;
        _cooldownRemain = 0f;
        _isSpeaking = false;

        if (NPCWalk != null)
        {
            NPCWalk.StopWandering();
        }
    }

    private void OnDestroy()
    {
        UnitEventBus.OnPlayerCreate -= OnPlayerCreated;
        UnitEventBus.OnFriendRoleChanged -= OnFriendRoleChanged;
        UnitEventBus.OnFriendLeave -= OnFriendLeave;
        GlobalEventBus.OnSwitchRole -= OnSwitchRole;
    }
}
}
