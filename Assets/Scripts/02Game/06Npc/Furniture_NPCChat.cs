using Core.Interface;
using FPSGame.Attribute;
using FPSGame.Furn;
using GameContract;
using UnityEngine;

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
        GlobalEventSub.OnPlayerCreate -= OnPlayerCreated;
        GlobalEventSub.OnPlayerCreate += OnPlayerCreated;
        GlobalEventSub.OnSwitchRole -= OnSwitchRole;
        GlobalEventSub.OnSwitchRole += OnSwitchRole;
    }

    private void OnPlayerCreated(I_Actor player)
    {
        if (this == null) return;

        _playerTransform = player.transform;
        _playerId = player.Id;

        TryHideIfSameIdAsPlayer();
    }

    private void OnSwitchRole(PlayerController newPlayer)
    {
        if (this == null) return;
        if (newPlayer == null) return;

        I_Actor actor = newPlayer.GetComponent<I_Actor>();
        if (actor != null)
        {
            _playerTransform = newPlayer.transform;
            _playerId = actor.Id;
        }

        TryHideIfSameIdAsPlayer();
    }

    /// <summary>
    /// 若NPC的Id与玩家Id相同则隐藏，不同则显示
    /// </summary>
    private void TryHideIfSameIdAsPlayer()
    {
        if (string.IsNullOrEmpty(_playerId)) return;

        if (!TryGetComponent(out I_Actor selfActor)) return;

        bool sameId = selfActor.Id == _playerId;
        gameObject.SetActive(!sameId);
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
        GlobalEventSub.ActorSpeech(gameObject, soundData);

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
        GlobalEventSub.OnPlayerCreate -= OnPlayerCreated;
        GlobalEventSub.OnSwitchRole -= OnSwitchRole;
    }
}
